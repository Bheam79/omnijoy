using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IChatService _chat;
    private readonly IHubContext<ChatHub> _hub;

    public ConversationsController(IChatService chat, IHubContext<ChatHub> hub)
    {
        _chat = chat;
        _hub  = hub;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── GET /api/conversations ────────────────────────────────────────────────

    /// <summary>List the current user's conversations (most recent first).</summary>
    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await _chat.GetConversationsAsync(userId);
        return Ok(result);
    }

    // ── GET /api/conversations/{id}/messages ──────────────────────────────────

    /// <summary>
    /// Paginated message history.
    /// Pass <c>before</c> (ISO-8601 timestamp) for cursor-based older-page loading.
    /// </summary>
    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] DateTime? before,
        [FromQuery] int limit = 30)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        if (limit is < 1 or > 100) limit = 30;

        try
        {
            var messages = await _chat.GetMessagesAsync(id, userId, before, limit);
            return Ok(messages);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    // ── POST /api/conversations/direct/{userId} ───────────────────────────────

    /// <summary>Start (or return existing) 1:1 conversation with another user.</summary>
    [HttpPost("direct/{userId:guid}")]
    public async Task<IActionResult> GetOrCreateDirect(Guid userId)
    {
        if (CurrentUserId is not { } myId) return Unauthorized();

        try
        {
            var conv = await _chat.GetOrCreateDirectConversationAsync(myId, userId);

            // Notify the other user so their conversation list updates
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("ConversationCreated", conv);

            return Ok(conv);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── PUT /api/conversations/{id}/read ──────────────────────────────────────

    /// <summary>Mark all messages in a conversation as read (resets unread badge).</summary>
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        await _chat.MarkConversationReadAsync(id, userId);
        return NoContent();
    }
}
