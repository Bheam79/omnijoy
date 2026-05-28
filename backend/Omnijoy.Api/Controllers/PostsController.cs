using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/posts")]
[Authorize]
public class PostsController : ControllerBase
{
    private readonly IPostService _posts;
    private readonly IHubContext<FeedHub> _feedHub;

    public PostsController(IPostService posts, IHubContext<FeedHub> feedHub)
    {
        _posts = posts;
        _feedHub = feedHub;
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
                Content:           input.Content ?? string.Empty,
                PostType:          input.PostType ?? "Text",
                Privacy:           input.Privacy  ?? "Friends",
                BackgroundImageUrl: input.Background
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

            return Created($"/api/posts/{post.Id}", post);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
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
    public IFormFileCollection? Media { get; set; }
}
