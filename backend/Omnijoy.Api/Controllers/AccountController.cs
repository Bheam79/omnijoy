using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountService _account;

    public AccountController(IAccountService account) => _account = account;

    private Guid GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (sub is null || !Guid.TryParse(sub, out var id))
            throw new UnauthorizedAccessException("Invalid token.");
        return id;
    }

    // POST /api/account/change-email
    [HttpPost("change-email")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _account.ChangeEmailAsync(userId, request.NewEmail, request.CurrentPassword);
            return Ok(new { message = "Email updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // POST /api/account/change-password
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _account.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, request.ConfirmNewPassword);
            return Ok(new { message = "Password updated successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // GET /api/account/notification-preferences
    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        try
        {
            var userId = GetUserId();
            var dto = await _account.GetNotificationPreferencesAsync(userId);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // PUT /api/account/notification-preferences
    [HttpPut("notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] NotificationPreferencesDto request)
    {
        try
        {
            var userId = GetUserId();
            var dto = await _account.UpdateNotificationPreferencesAsync(userId, request);
            return Ok(dto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/account/deactivate
    [HttpPost("deactivate")]
    public async Task<IActionResult> Deactivate()
    {
        try
        {
            var userId = GetUserId();
            await _account.DeactivateAccountAsync(userId);
            return Ok(new { message = "Account deactivated." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/account/delete
    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteAccountRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _account.DeleteAccountAsync(userId, request.ConfirmEmail);
            return Ok(new { message = "Account scheduled for deletion." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }
}
