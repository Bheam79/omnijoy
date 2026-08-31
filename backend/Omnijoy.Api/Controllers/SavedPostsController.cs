using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

/// <summary>Private bookmark endpoints for the authenticated user.</summary>
[ApiController]
[Route("api")]
[Authorize]
public class SavedPostsController : ControllerBase
{
    private readonly ISavedPostService _savedPosts;

    public SavedPostsController(ISavedPostService savedPosts)
    {
        _savedPosts = savedPosts;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/posts/{id}/save ───────────────────────────────────────────

    [HttpPost("posts/{id:guid}/save")]
    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    public async Task<IActionResult> SavePost(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var changed = await _savedPosts.SaveAsync(userId, id);
            return Ok(new SavedPostStateDto(IsSaved: true, Changed: changed));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or UnauthorizedAccessException)
        {
            // Missing and newly-invisible posts deliberately share the same
            // generic response so this endpoint cannot be used as an oracle.
            return PostNotFound();
        }
    }

    // ── DELETE /api/posts/{id}/save ─────────────────────────────────────────

    [HttpDelete("posts/{id:guid}/save")]
    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    public async Task<IActionResult> UnsavePost(Guid id)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var changed = await _savedPosts.UnsaveAsync(userId, id);
            return Ok(new SavedPostStateDto(IsSaved: false, Changed: changed));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or UnauthorizedAccessException)
        {
            return PostNotFound();
        }
    }

    // ── GET /api/users/me/saved-posts ───────────────────────────────────────

    [HttpGet("users/me/saved-posts")]
    public async Task<IActionResult> GetSavedPosts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 50) pageSize = 20;

        var saved = await _savedPosts.GetSavedAsync(userId, page, pageSize);
        var result = new PagedResult<PostDto>(
            saved.Items.Select(item => item.Post).ToArray(),
            saved.Page,
            saved.PageSize,
            saved.HasMore);

        return Ok(result);
    }

    private NotFoundObjectResult PostNotFound()
        => NotFound(new { error = "Post not found." });
}
