using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

/// <summary>
/// Admin-only user management endpoints.
/// All routes require the <c>Admin</c> role unless noted.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin)
    {
        _admin = admin;
    }

    private Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ── PATCH /api/admin/users/{id}/role ──────────────────────────────────────

    /// <summary>
    /// Changes the platform role of the specified user.
    /// Body: <c>{ "role": "User" | "Moderator" | "Admin" }</c>
    /// </summary>
    [HttpPatch("api/admin/users/{id:guid}/role")]
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
}
