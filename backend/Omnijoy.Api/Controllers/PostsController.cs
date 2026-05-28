using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
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

    public PostsController(
        IPostService posts,
        IReactionService reactions,
        IHubContext<FeedHub> feedHub,
        INotificationService notifications)
    {
        _posts         = posts;
        _reactions     = reactions;
        _feedHub       = feedHub;
        _notifications = notifications;
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
    public async Task<IActionResult> CreatePost([FromForm] CreatePostFormInput input)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

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
            var recipientIds = friendIds.Append(userId);
            foreach (var recipientId in recipientIds)
            {
                await _feedHub.Clients
                    .Group($"user:{recipientId}")
                    .SendAsync("NewPost", post);
            }

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

    // ── POST /api/posts/{id}/reactions ────────────────────────────────────────

    /// <summary>
    /// Add or change the current user's reaction to a post.
    /// Body: { "reactionType": "Like" | "Love" | "Haha" | "Wow" | "Sad" | "Angry" }
    /// </summary>
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
