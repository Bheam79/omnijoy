using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IFriendService _friends;
    private readonly INotificationService _notifications;

    public FriendsController(IFriendService friends, INotificationService notifications)
    {
        _friends       = friends;
        _notifications = notifications;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── POST /api/friends/request/{userId} ────────────────────────────────────

    [HttpPost("request/{userId:guid}")]
    public async Task<IActionResult> SendRequest(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            var dto = await _friends.SendRequestAsync(me, userId);

            // Persist + push notification (NotificationReceived event).
            await _notifications.CreateAsync(
                recipientUserId: userId,
                type:            NotificationType.FriendRequest,
                referenceId:     me.ToString(),
                actorUserId:     me);

            // Legacy event — kept so existing client handlers keep firing.
            await _notifications.PushTransientAsync(
                userId,
                "FriendRequestReceived",
                new { from = new { id = me.ToString(), displayName = User.FindFirstValue(ClaimTypes.Name) } });

            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── POST /api/friends/accept/{userId} ─────────────────────────────────────

    [HttpPost("accept/{userId:guid}")]
    public async Task<IActionResult> AcceptRequest(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            var dto = await _friends.AcceptRequestAsync(me, userId);

            await _notifications.CreateAsync(
                recipientUserId: userId,
                type:            NotificationType.FriendRequestAccepted,
                referenceId:     me.ToString(),
                actorUserId:     me);

            await _notifications.PushTransientAsync(
                userId,
                "FriendRequestAccepted",
                new { by = new { id = me.ToString(), displayName = User.FindFirstValue(ClaimTypes.Name) } });

            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── POST /api/friends/decline/{userId} ────────────────────────────────────

    [HttpPost("decline/{userId:guid}")]
    public async Task<IActionResult> DeclineRequest(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            await _friends.DeclineRequestAsync(me, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── DELETE /api/friends/request/{userId} ──────────────────────────────────

    [HttpDelete("request/{userId:guid}")]
    public async Task<IActionResult> CancelRequest(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            await _friends.CancelRequestAsync(me, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── DELETE /api/friends/{userId} ──────────────────────────────────────────

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> RemoveFriend(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            await _friends.RemoveFriendAsync(me, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── POST /api/friends/block/{userId} ──────────────────────────────────────

    [HttpPost("block/{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            await _friends.BlockUserAsync(me, userId);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ── DELETE /api/friends/block/{userId} ────────────────────────────────────

    [HttpDelete("block/{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            await _friends.UnblockUserAsync(me, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── GET /api/friends ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetFriends(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        var result = await _friends.GetFriendsAsync(me, page, pageSize);
        return Ok(result);
    }

    // ── GET /api/friends/requests ─────────────────────────────────────────────

    [HttpGet("requests")]
    public async Task<IActionResult> GetPendingRequests()
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        var requests = await _friends.GetPendingRequestsAsync(me);
        return Ok(requests);
    }

    // ── GET /api/friends/sent ─────────────────────────────────────────────────

    [HttpGet("sent")]
    public async Task<IActionResult> GetSentRequests()
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        var requests = await _friends.GetSentRequestsAsync(me);
        return Ok(requests);
    }

    // ── GET /api/friends/suggestions ─────────────────────────────────────────

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions([FromQuery] int limit = 10)
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        var suggestions = await _friends.GetSuggestionsAsync(me, limit);
        return Ok(suggestions);
    }

    // ── GET /api/friends/status/{userId} ──────────────────────────────────────

    [HttpGet("status/{userId:guid}")]
    public async Task<IActionResult> GetStatus(Guid userId)
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        var status = await _friends.GetFriendStatusAsync(me, userId);
        return Ok(status);
    }

    // ── GET /api/friends/requests/count ──────────────────────────────────────

    [HttpGet("requests/count")]
    public async Task<IActionResult> GetPendingCount()
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        var count = await _friends.GetPendingRequestCountAsync(me);
        return Ok(new { count });
    }

    // ── PUT /api/friends/{userId}/relation ────────────────────────────────────

    [HttpPut("{userId:guid}/relation")]
    public async Task<IActionResult> SetFamilyRelation(
        Guid userId,
        [FromBody] SetFamilyRelationRequest request)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            var dto = await _friends.SetFamilyRelationAsync(me, userId, request);
            return Ok(new { relation = dto });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
