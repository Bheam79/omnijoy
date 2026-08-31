using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs.Comments;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Authorize]
public class CommentsController : ControllerBase
{
    private readonly ICommentService _comments;
    private readonly ICommentReactionService _reactions;
    private readonly IHubContext<FeedHub> _feedHub;

    public CommentsController(
        ICommentService comments,
        ICommentReactionService reactions,
        IHubContext<FeedHub> feedHub)
    {
        _comments = comments;
        _reactions = reactions;
        _feedHub  = feedHub;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/posts/{postId}/comments ─────────────────────────────────────

    /// <summary>
    /// Creates a new comment (or reply) on a post.
    /// Body: { "content": "...", "parentCommentId": null | "&lt;guid&gt;" }
    /// Maximum depth: top-level comment + one reply level.
    /// </summary>
    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    [HttpPost("api/posts/{postId:guid}/comments")]
    public async Task<IActionResult> CreateComment(
        Guid postId,
        [FromBody] CreateCommentRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var dto = await _comments.CreateCommentAsync(postId, userId, request);

            // Push NewComment to all connections subscribed to this post
            await _feedHub.Clients
                .Group($"post:{postId}")
                .SendAsync("NewComment", dto);

            return Created($"/api/comments/{dto.Id}", dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── GET /api/posts/{postId}/comments ──────────────────────────────────────

    [HttpGet("api/posts/{postId:guid}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> GetComments(
        Guid postId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _comments.GetCommentsAsync(postId, page, pageSize, CurrentUserId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── GET /api/comments/{commentId}/replies ─────────────────────────────────

    [HttpGet("api/comments/{commentId:guid}/replies")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReplies(Guid commentId)
    {
        try
        {
            var replies = await _comments.GetRepliesAsync(commentId);
            return Ok(replies);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── GET /api/comments/{commentId}/reactions ─────────────────────────────

    [HttpGet("api/comments/{commentId:guid}/reactions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReactions(Guid commentId)
    {
        try
        {
            var dto = await _reactions.GetReactionsAsync(commentId, CurrentUserId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── GET /api/comments/{commentId}/reactions/who ───────────────────────────

    [HttpGet("api/comments/{commentId:guid}/reactions/who")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReactionWho(Guid commentId)
    {
        try
        {
            var dto = await _reactions.GetReactionWhoAsync(commentId, CurrentUserId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── POST /api/comments/{commentId}/reactions ────────────────────────────

    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    [HttpPost("api/comments/{commentId:guid}/reactions")]
    public async Task<IActionResult> AddOrUpdateReaction(
        Guid commentId,
        [FromBody] AddOrUpdateReactionRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var dto = await _reactions.AddOrUpdateReactionAsync(
                commentId,
                userId,
                request.ReactionType);
            await PushReactionUpdateAsync(commentId, dto);
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

    // ── DELETE /api/comments/{commentId}/reactions ───────────────────────────

    [EnableRateLimiting(RateLimitConstants.InteractionPolicy)]
    [HttpDelete("api/comments/{commentId:guid}/reactions")]
    public async Task<IActionResult> RemoveReaction(Guid commentId)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var dto = await _reactions.RemoveReactionAsync(commentId, userId);
            await PushReactionUpdateAsync(commentId, dto);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── PUT /api/comments/{commentId} ─────────────────────────────────────────

    [HttpPut("api/comments/{commentId:guid}")]
    public async Task<IActionResult> UpdateComment(
        Guid commentId,
        [FromBody] UpdateCommentRequest request)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var dto = await _comments.UpdateCommentAsync(commentId, userId, request);
            return Ok(dto);
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

    // ── DELETE /api/comments/{commentId} ──────────────────────────────────────

    [HttpDelete("api/comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            await _comments.DeleteCommentAsync(commentId, userId);
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

    private async Task PushReactionUpdateAsync(Guid commentId, PostReactionsDto dto)
    {
        var postId = await _reactions.GetOwningPostIdAsync(commentId);
        var evt = new CommentReactionCountsUpdatedEvent(
            commentId,
            postId,
            dto.Counts,
            dto.TotalCount);

        await _feedHub.Clients
            .Group($"post:{postId}")
            .SendAsync("CommentReactionCountsUpdated", evt);
    }
}
