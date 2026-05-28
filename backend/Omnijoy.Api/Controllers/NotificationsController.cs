using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── GET /api/notifications ────────────────────────────────────────────────

    /// <summary>Paginated notification inbox (newest first).</summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var result = await _notifications.GetNotificationsAsync(userId, page, pageSize);
        return Ok(result);
    }

    // ── GET /api/notifications/unread/count ───────────────────────────────────

    [HttpGet("unread/count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        var count = await _notifications.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    // ── POST /api/notifications/{id}/read ─────────────────────────────────────

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        await _notifications.MarkReadAsync(id, userId);
        return NoContent();
    }

    // ── POST /api/notifications/read-all ──────────────────────────────────────

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();
        await _notifications.MarkAllReadAsync(userId);
        return NoContent();
    }
}
