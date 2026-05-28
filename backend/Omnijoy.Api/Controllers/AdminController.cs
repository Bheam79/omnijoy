using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

/// <summary>
/// Admin / Moderator user-management endpoints.
/// Per-action role requirements are declared on each method.
/// </summary>
[ApiController]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminService          _admin;
    private readonly IModerationLogService  _moderationLog;

    public AdminController(IAdminService admin, IModerationLogService moderationLog)
    {
        _admin         = admin;
        _moderationLog = moderationLog;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── PATCH /api/admin/users/{id}/role ──────────────────────────────────────

    /// <summary>
    /// Changes the platform role of the specified user (Admin only).
    /// Body: <c>{ "role": "User" | "Moderator" | "Admin" }</c>
    /// </summary>
    [HttpPatch("api/admin/users/{id:guid}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ChangeUserRole(
        Guid id,
        [FromBody] ChangeUserRoleRequest request)
    {
        if (CurrentUserId is not { } requesterId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var result = await _admin.ChangeUserRoleAsync(requesterId, id, request.Role);
            return Ok(result);
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

    // ── POST /api/admin/users/{id}/ban ────────────────────────────────────────

    /// <summary>
    /// Bans the specified user (Admin or Moderator).
    /// Body: <c>{ "notes": "optional reason" }</c>
    /// </summary>
    [HttpPost("api/admin/users/{id:guid}/ban")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> BanUser(Guid id, [FromBody] BanUserRequest? request)
    {
        if (CurrentUserId is not { } requesterId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var result = await _admin.BanUserAsync(requesterId, id, request?.Notes);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── POST /api/admin/users/{id}/unban ──────────────────────────────────────

    /// <summary>
    /// Unbans the specified user (Admin or Moderator).
    /// Body: <c>{ "notes": "optional reason" }</c>
    /// </summary>
    [HttpPost("api/admin/users/{id:guid}/unban")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> UnbanUser(Guid id, [FromBody] BanUserRequest? request)
    {
        if (CurrentUserId is not { } requesterId)
            return Unauthorized(new { error = "Not authenticated." });

        try
        {
            var result = await _admin.UnbanUserAsync(requesterId, id, request?.Notes);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ── GET /api/admin/audit-log ──────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated, optionally-filtered view of the moderation audit
    /// log (Admin only).
    /// Query: <c>actorId</c> (GUID), <c>action</c> (ModerationAction string),
    /// <c>page</c> (default 1), <c>pageSize</c> (default 20, max 100).
    /// </summary>
    [HttpGet("api/admin/audit-log")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAuditLog(
        [FromQuery] Guid? actorId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _moderationLog.ListAsync(actorId, action, page, pageSize);
        return Ok(result);
    }
}
