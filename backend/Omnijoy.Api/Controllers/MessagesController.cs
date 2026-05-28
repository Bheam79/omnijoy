using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Chat;
using Omnijoy.Core.DTOs.Posts;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IChatService _chat;
    private readonly IHubContext<ChatHub> _hub;
    private readonly INotificationService _notifications;

    public MessagesController(
        IChatService chat,
        IHubContext<ChatHub> hub,
        INotificationService notifications)
    {
        _chat          = chat;
        _hub           = hub;
        _notifications = notifications;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/messages ────────────────────────────────────────────────────

    /// <summary>
    /// Send a message (text, image, video, or file).
    /// Send as multipart/form-data:
    ///   - conversationId  (string, required)
    ///   - content         (string, optional)
    ///   - file            (IFormFile, optional)
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(210 * 1024 * 1024)] // 210 MB to support large video attachments
    public async Task<IActionResult> SendMessage([FromForm] SendMessageFormInput input)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        if (!Guid.TryParse(input.ConversationId, out var conversationId))
            return BadRequest(new { error = "conversationId is required and must be a valid GUID." });

        try
        {
            List<MediaUploadItem>? mediaItems = null;
            if (input.File is { Length: > 0 })
            {
                var ms = new MemoryStream();
                await input.File.CopyToAsync(ms);
                ms.Position = 0;
                mediaItems = [new MediaUploadItem(ms, input.File.FileName, input.File.ContentType)];
            }

            var message = await _chat.SendMessageAsync(
                conversationId, userId, input.Content, mediaItems);

            // ── Push to all participants currently in the conversation group ──
            await _hub.Clients
                .Group($"conversation:{conversationId}")
                .SendAsync("ReceiveMessage", message);

            // ── Push MessageReceived badge update to participants *other* than
            //    the sender via NotificationHub so the bell/badge updates even
            //    when their chat window is closed.
            var participants = await _chat.GetParticipantUserIdsAsync(conversationId);
            var recipients   = participants.Where(id => id != userId).ToArray();
            if (recipients.Length > 0)
            {
                await _notifications.PushTransientToManyAsync(
                    recipients,
                    "MessageReceived",
                    new
                    {
                        conversationId = conversationId.ToString(),
                        messageId      = message.Id.ToString(),
                        senderId       = userId.ToString(),
                        sentAt         = message.CreatedAt,
                    });
            }

            return Created($"/api/messages/{message.Id}", message);
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

    // ── DELETE /api/messages/{id} ─────────────────────────────────────────────

    /// <summary>Soft-delete a message (only the sender may delete their own message).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        try
        {
            var message = await _chat.DeleteMessageAsync(id, userId);

            // ── Notify conversation group ──────────────────────────────────────
            await _hub.Clients
                .Group($"conversation:{message.ConversationId}")
                .SendAsync("MessageDeleted", new
                {
                    messageId      = id,
                    conversationId = message.ConversationId,
                });

            return Ok(message);
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

/// <summary>Bound from multipart/form-data for message sending.</summary>
public class SendMessageFormInput
{
    public string? ConversationId { get; set; }
    public string? Content        { get; set; }
    public IFormFile? File        { get; set; }
}
