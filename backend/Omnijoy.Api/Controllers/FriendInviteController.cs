using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/friends/invite")]
public class FriendInviteController : ControllerBase
{
    private readonly IFriendInviteService _invites;
    private readonly INotificationService _notifications;

    public FriendInviteController(
        IFriendInviteService invites,
        INotificationService notifications)
    {
        _invites       = invites;
        _notifications = notifications;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>
    /// Returns the base URL to use when constructing invite links.
    /// Uses the Request's scheme + host so it works in dev and prod.
    /// </summary>
    private string BaseUrl =>
        $"{Request.Scheme}://{Request.Host}";

    // ── POST /api/friends/invite/link ─────────────────────────────────────────
    // Generates (or reuses) a shareable invite link for the current user.

    [HttpPost("link")]
    [Authorize]
    public async Task<IActionResult> CreateLink()
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            var dto = await _invites.CreateInviteLinkAsync(me, BaseUrl);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── POST /api/friends/invite/email ────────────────────────────────────────
    // Sends a friend invitation to an email address.
    // If the user already exists, friendship is auto-accepted immediately.

    [HttpPost("email")]
    [Authorize]
    public async Task<IActionResult> InviteByEmail([FromBody] FriendInviteEmailRequest request)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Email is required." });

        try
        {
            var result = await _invites.InviteByEmailAsync(me, request.Email, BaseUrl);

            // If the target was already on the platform and we just became friends,
            // push a notification so they see it in real-time.
            if (result.Outcome == "accepted")
            {
                // Look up the target user's Id to push the notification.
                // The service already did this lookup internally; to avoid an
                // extra DB round-trip we rely on the "accepted" outcome + a
                // transient push (no persistent row needed — the friendship itself is the fact).
                await _notifications.PushTransientAsync(
                    // We can't know the acceptor's Id without an extra lookup here,
                    // so we skip the push — the next page load will show the friend.
                    // (The FriendService.AcceptRequestAsync path already handles push.)
                    me, "FriendInviteEmailAccepted", new { email = request.Email });
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    // ── GET /api/friends/invite/{token} ───────────────────────────────────────
    // Returns invite metadata (inviter name/avatar) without requiring auth.
    // Used to render the accept page before login.

    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInfo(string token)
    {
        try
        {
            var dto = await _invites.GetInviteInfoAsync(token);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── POST /api/friends/invite/{token}/accept ────────────────────────────────
    // Redeems the invite: creates an accepted friendship.
    // Requires the acceptor to be authenticated.

    [HttpPost("{token}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(string token)
    {
        if (CurrentUserId is not { } me) return Unauthorized();

        try
        {
            var dto = await _invites.AcceptInviteAsync(token, me);

            // Notify the inviter that someone accepted their invite.
            await _notifications.CreateAsync(
                recipientUserId: dto.InviterId,
                type:            NotificationType.FriendRequestAccepted,
                referenceId:     me.ToString(),
                actorUserId:     me);

            await _notifications.PushTransientAsync(
                dto.InviterId,
                "FriendRequestAccepted",
                new { by = new { id = me.ToString(), displayName = User.FindFirstValue(ClaimTypes.Name) } });

            return Ok(dto);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    // ── DELETE /api/friends/invite/link ──────────────────────────────────────
    // Revokes all active link invites for the current user.

    [HttpDelete("link")]
    [Authorize]
    public async Task<IActionResult> RevokeLinks()
    {
        if (CurrentUserId is not { } me) return Unauthorized();
        await _invites.RevokeLinkInvitesAsync(me);
        return NoContent();
    }
}
