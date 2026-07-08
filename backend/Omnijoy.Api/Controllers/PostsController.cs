using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly IPostService _posts;
    private readonly IReactionService _reactions;
    private readonly IHubContext<FeedHub> _feedHub;
    private readonly INotificationService _notifications;
    private readonly IShareService _shares;
    private readonly IFeedCache _feedCache;

    public PostsController(
        IPostService posts,
        IReactionService reactions,
        IHubContext<FeedHub> feedHub,
        INotificationService notifications,
        IShareService shares,
        IFeedCache feedCache)
    {
        _posts         = posts;
        _reactions     = reactions;
        _feedHub       = feedHub;
        _notifications = notifications;
        _shares        = shares;
        _feedCache     = feedCache;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/posts ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new post.
    /// Send as multipart/form-data:
    ///   - content      (string)
    ///   - postType     (Text | Image | Video | TextOnBackground)
    ///   - privacy      (Everyone | Friends | OnlyMe)
    ///   - background   (string, optional — URL or CSS value for TextOnBackground posts)
    ///   - media[]      (one or more files, optional)
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(210 * 1024 * 1024)] // 210 MB to cover large video uploads
    [EnableRateLimiting(RateLimitConstants.UploadPolicy)]
    public async Task<IActionResult> CreatePost([FromForm] CreatePostFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        // Text and Image posts must carry actual content — without this guard
        // the service would silently accept "" and persist a blank post.
        var postType = input.PostType ?? "Text";
        if ((postType.Equals("Text", StringComparison.OrdinalIgnoreCase)
             || postType.Equals("Image", StringComparison.OrdinalIgnoreCase))
            && string.IsNullOrWhiteSpace(input.Content))
        {
            return BadRequest(new { error = "Content is required for Text and Image posts." });
        }

        try
        {
            var mediaItems = new List<MediaUploadItem>();
            if (input.Media is { Count: > 0 })
            {
                foreach (var file in input.Media)
                {
                    if (file.Length == 0) continue;
                    var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;
                    mediaItems.Add(new MediaUploadItem(ms, file.FileName, file.ContentType));
                }
            }

            // Fallback: browsers and E2E tests send files under the bracket-notation
            // key 'media[]'. ASP.NET Core model binding does NOT map 'media[]' to the
            // 'Media' property, so we check Request.Form.Files directly.
            if (mediaItems.Count == 0)
            {
                foreach (var file in Request.Form.Files.GetFiles("media[]"))
                {
                    if (file.Length == 0) continue;
                    var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;
                    mediaItems.Add(new MediaUploadItem(ms, file.FileName, file.ContentType));
                }
            }

            var request = new CreatePostRequest(
                Content:            input.Content ?? string.Empty,
                PostType:           input.PostType ?? "Text",
                Privacy:            input.Privacy  ?? "Friends",
                BackgroundImageUrl: input.Background,
                CompanyPageId:      input.CompanyPageId,
                LinkUrl:            input.LinkUrl,
                LinkTitle:          input.LinkTitle,
                LinkDescription:    input.LinkDescription,
                LinkImageUrl:       input.LinkImageUrl,
                LinkSiteName:       input.LinkSiteName
            );

            var post = await _posts.CreatePostAsync(userId, request, mediaItems.Count > 0 ? mediaItems : null);

            // ── Push NewPost to author + friends via FeedHub ──────────────────
            var friendIds = await _posts.GetFriendIdsAsync(userId);
            var recipientIds = friendIds.Append(userId).ToList();
            foreach (var recipientId in recipientIds)
            {
                await _feedHub.Clients
                    .Group($"user:{recipientId}")
                    .SendAsync("NewPost", post);
            }

            // ── Cache invalidation (push, not pull) ───────────────────────────
            // A new post invalidates page-1 feed for the author *and* every
            // friend (recipients of NewPost). The TTL is a fallback for users
            // we miss here (e.g. company-page followers).
            await _feedCache.InvalidateUserFeedsAsync(recipientIds);

            // ── Persist + push NewPostFromFriend notifications to friends ─────
            // Skipped for OnlyMe posts (which only the author sees).
            if (friendIds.Count > 0 && post.Privacy != "OnlyMe")
            {
                await _notifications.CreateForManyAsync(
                    recipientUserIds: friendIds,
                    type:             NotificationType.NewPostFromFriend,
                    referenceId:      post.Id.ToString(),
                    actorUserId:      userId);
            }

            return Created($"/api/posts/{post.Id}", post);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    // ── GET /api/feed ─────────────────────────────────────────────────────────

    [HttpGet("/api/feed")]
    public async Task<IActionResult> GetFeed([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        var result = await _posts.GetFeedAsync(userId, page, pageSize);
        return Ok(result);
    }

    // ── GET /api/feed/trending ────────────────────────────────────────────────

    /// <summary>
    /// Returns the currently-trending public posts. Served from the cache
    /// maintained by <c>TrendingFeedRefreshService</c>; falls back to a live
    /// query on cache miss (e.g. first hit after deploy / Redis restart).
    /// </summary>
    [HttpGet("/api/feed/trending")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTrending(CancellationToken ct)
    {
        var cached = await _feedCache.GetTrendingPostsAsync(ct);
        if (cached is not null) return Ok(cached);

        // Cache miss → compute now AND prime the cache so subsequent callers
        // benefit until the next scheduled refresh.
        var live = await _posts.GetTrendingPostsAsync(20, ct);
        await _feedCache.SetTrendingPostsAsync(live, ct);
        return Ok(live);
    }

    // ── GET /api/posts/{id} ───────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPost(Guid id)
    {
        try
        {
            var post = await _posts.GetPostAsync(id, CurrentUserId);
            return Ok(post);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    // ── PUT /api/posts/{id} ───────────────────────────────────────────────────

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePost(Guid id, [FromBody] UpdatePostRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var post = await _posts.UpdatePostAsync(id, userId, request);
            return Ok(post);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── DELETE /api/posts/{id} ────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            await _posts.DeletePostAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    // ── GET /api/posts/{id}/reactions ─────────────────────────────────────────

    [HttpGet("{id:guid}/reactions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReactions(Guid id)
    {
        try
        {
            var dto = await _reactions.GetReactionsAsync(id, CurrentUserId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── GET /api/posts/{id}/reactions/who ────────────────────────────────────

    /// <summary>
    /// Returns up to 5 people who reacted to a post, prioritising friends of
    /// the current user, plus the count of remaining reactors not listed.
    /// </summary>
    [HttpGet("{id:guid}/reactions/who")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReactionWho(Guid id)
    {
        try
        {
            var dto = await _reactions.GetReactionWhoAsync(id, CurrentUserId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── POST /api/posts/{id}/reactions ────────────────────────────────────────

    /// <summary>
    /// Add or change the current user's reaction to a post.
    /// Body: { "reactionType": "Like" | "Love" | "Haha" | "Wow" | "Sad" | "Angry" }
    /// </summary>
    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    [HttpPost("{id:guid}/reactions")]
    public async Task<IActionResult> AddOrUpdateReaction(
        Guid id,
        [FromBody] AddOrUpdateReactionRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var dto = await _reactions.AddOrUpdateReactionAsync(id, userId, request.ReactionType);
            await PushReactionUpdateAsync(id, dto);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── DELETE /api/posts/{id}/reactions ──────────────────────────────────────

    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    [HttpDelete("{id:guid}/reactions")]
    public async Task<IActionResult> RemoveReaction(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var dto = await _reactions.RemoveReactionAsync(id, userId);
            await PushReactionUpdateAsync(id, dto);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── POST /api/posts/{id}/share ────────────────────────────────────────────

    /// <summary>
    /// Share an existing post to own wall, a friend's wall, or a company page.
    /// Body: { "targetType": "OwnWall" | "FriendWall" | "CompanyPage", "targetId": guid?, "message": string? }
    /// </summary>
    [HttpPost("{id:guid}/share")]
    public async Task<IActionResult> SharePost(Guid id, [FromBody] CreateShareRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var sharedPost = await _shares.CreateShareAsync(userId, id, request);

            // Push NewSharedPost to relevant users via FeedHub
            var recipientIds = await _shares.GetShareRecipientIdsAsync(userId, sharedPost);
            var feedItem = new FeedItemDto("SharedPost", null, sharedPost);

            foreach (var recipientId in recipientIds)
            {
                await _feedHub.Clients
                    .Group($"user:{recipientId}")
                    .SendAsync("NewSharedPost", feedItem);
            }

            // Notify the original post author that their post was shared (if not the sharer)
            if (sharedPost.OriginalPost.Author.Id != userId)
            {
                await _notifications.CreateAsync(
                    recipientUserId: sharedPost.OriginalPost.Author.Id,
                    type:            NotificationType.PostShare,
                    referenceId:     id.ToString(),
                    actorUserId:     userId);
            }

            // For FriendWall: notify the target user
            if (request.TargetType.Equals("FriendWall", StringComparison.OrdinalIgnoreCase) &&
                request.TargetId.HasValue &&
                request.TargetId.Value != userId)
            {
                await _notifications.CreateAsync(
                    recipientUserId: request.TargetId.Value,
                    type:            NotificationType.PostShare,
                    referenceId:     id.ToString(),
                    actorUserId:     userId);
            }

            return Created($"/api/posts/{id}/share", sharedPost);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── SignalR helper ────────────────────────────────────────────────────────

    private Task PushReactionUpdateAsync(Guid postId, PostReactionsDto dto)
    {
        var evt = new ReactionCountsUpdatedEvent(postId, dto.Counts, dto.TotalCount);
        return _feedHub.Clients
            .Group($"post:{postId}")
            .SendAsync("ReactionCountsUpdated", evt);
    }
}

// ── Form-data input model ─────────────────────────────────────────────────────

/// <summary>
/// Bound from multipart/form-data for post creation.
/// </summary>
public class CreatePostFormInput
{
    public string? Content    { get; set; }
    public string? PostType   { get; set; }
    public string? Privacy    { get; set; }
    /// <summary>Background color / image URL for TextOnBackground posts.</summary>
    public string? Background { get; set; }
    /// <summary>Optional: ID of a company page to post on behalf of.</summary>
    public Guid?   CompanyPageId { get; set; }
    public IFormFileCollection? Media { get; set; }

    // ── Optional embedded OG / link preview ──────────────────────────────────
    /// <summary>URL pasted by the user — triggers an OG card in the feed.</summary>
    public string? LinkUrl         { get; set; }
    public string? LinkTitle       { get; set; }
    public string? LinkDescription { get; set; }
    public string? LinkImageUrl    { get; set; }
    public string? LinkSiteName    { get; set; }
}
