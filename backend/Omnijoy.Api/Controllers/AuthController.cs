using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Api.Controllers;

/// <summary>
/// Authentication endpoints. All actions are rate-limited to
/// <see cref="RateLimitConstants.StrictPermitLimit"/> requests per minute
/// (per IP) to prevent brute-force and enumeration attacks.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitConstants.StrictPolicy)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly OmnijoyDbContext _db;

    public AuthController(
        IAuthService auth,
        ILogger<AuthController> logger,
        IWebHostEnvironment env,
        OmnijoyDbContext db)
    {
        _auth   = auth;
        _logger = logger;
        _env    = env;
        _db     = db;
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var result = await _auth.RegisterAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginPasswordRequest request)
    {
        try
        {
            var result = await _auth.LoginWithPasswordAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/auth/otp/request
    [HttpPost("otp/request")]
    public async Task<IActionResult> OtpRequest([FromBody] OtpRequestDto request)
    {
        // Always return 200 to prevent email enumeration. If SMTP is not
        // configured or the send otherwise fails, log the error but do not
        // surface it — the client must never learn whether the address exists.
        try
        {
            await _auth.RequestOtpAsync(request);
        }
        catch (Exception ex) when (
            ex is System.Net.Mail.SmtpException ||
            ex.InnerException is System.Net.Mail.SmtpException)
        {
            _logger.LogError(ex, "Failed to send OTP email (SMTP not configured or unavailable); continuing silently.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending OTP email; continuing silently.");
        }

        return Ok(new { message = "If your email is registered, you will receive a code shortly." });
    }

    // POST /api/auth/otp/verify
    [HttpPost("otp/verify")]
    public async Task<IActionResult> OtpVerify([FromBody] OtpVerifyDto request)
    {
        try
        {
            var result = await _auth.VerifyOtpAsync(request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/auth/oauth/google
    [HttpPost("oauth/google")]
    public async Task<IActionResult> GoogleLogin([FromBody] OAuthRequest request)
    {
        try
        {
            var result = await _auth.LoginWithGoogleAsync(request.Token);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            // OAuth not configured or token unverifiable — always 401 to the client;
            // configuration state is an internal concern.
            return Unauthorized(new { error = "Invalid Google token." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/auth/oauth/facebook
    [HttpPost("oauth/facebook")]
    public async Task<IActionResult> FacebookLogin([FromBody] OAuthRequest request)
    {
        try
        {
            var result = await _auth.LoginWithFacebookAsync(request.Token);
            return Ok(result);
        }
        catch (InvalidOperationException)
        {
            // OAuth not configured or token unverifiable — always 401 to the client;
            // configuration state is an internal concern.
            return Unauthorized(new { error = "Invalid Facebook token." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var result = await _auth.RefreshAsync(request.RefreshToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _auth.LogoutAsync(request.RefreshToken, request.AccessToken);
        return Ok(new { message = "Logged out successfully." });
    }

    // POST /api/auth/password/forgot
    [HttpPost("password/forgot")]
    public async Task<IActionResult> PasswordForgot([FromBody] PasswordResetRequestDto request)
    {
        try { await _auth.RequestPasswordResetAsync(request); }
        catch (Exception ex) when (
            ex is System.Net.Mail.SmtpException ||
            ex.InnerException is System.Net.Mail.SmtpException)
        {
            _logger.LogError(ex, "Failed to send password-reset email; continuing silently.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending password-reset email; continuing silently.");
        }
        return Ok(new { message = "If your email is registered, you will receive a reset code shortly." });
    }

    // POST /api/auth/password/reset
    [HttpPost("password/reset")]
    public async Task<IActionResult> PasswordReset([FromBody] PasswordResetConfirmDto request)
    {
        try
        {
            await _auth.ConfirmPasswordResetAsync(request);
            return Ok(new { message = "Password reset successfully. Please log in with your new password." });
        }
        catch (ArgumentException ex)           { return BadRequest(new { error = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Unauthorized(new { error = ex.Message }); }
    }

    // POST /api/auth/verify-email?token=…
    //
    // The token IS the credential — no bearer JWT required. A bad/used token
    // is a client-side validation problem (stale link in an old email),
    // not an authentication failure, so map UnauthorizedAccessException to
    // 400 instead of letting it bubble up as 401.
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        try
        {
            await _auth.VerifyEmailAsync(token);
            return Ok(new { message = "Email verified successfully." });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/auth/resend-verification
    //
    // Authenticated — only the account owner may request another verification
    // link to their own address. Class-level [EnableRateLimiting(StrictPolicy)]
    // already covers brute-force / abuse.
    [HttpPost("resend-verification")]
    [Authorize]
    public async Task<IActionResult> ResendVerification()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(new { error = "Invalid authentication token." });

        try
        {
            await _auth.ResendVerificationEmailAsync(userId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex) when (
            ex is System.Net.Mail.SmtpException ||
            ex.InnerException is System.Net.Mail.SmtpException)
        {
            // Mirror the OTP-request pattern: log SMTP failures, don't tell
            // the client (so transient mail outages can't be probed).
            _logger.LogError(ex,
                "Failed to send verification email; continuing silently.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error while sending verification email; continuing silently.");
        }

        return Ok(new { message = "Verification email sent." });
    }

    // POST /api/auth/test/get-verification-token?email=…
    //
    // Test-only helper for the E2E suite. The verification token is sent to
    // the user via email; the E2E stack has no test inbox, so this endpoint
    // lets the spec retrieve the token directly from the DB.
    //
    // SECURITY: returns 404 outside of the Development environment, so the
    // route literally does not exist in Staging / Production.
    [HttpPost("test/get-verification-token")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVerificationToken([FromQuery] string email)
    {
        if (!_env.IsDevelopment())
            return NotFound();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "email query parameter is required." });

        var normalized = email.ToLowerInvariant().Trim();
        var user = await _db.Users
            .Where(u => u.Email == normalized)
            .Select(u => new { u.EmailVerificationToken, u.IsEmailVerified })
            .FirstOrDefaultAsync();

        if (user is null)
            return NotFound(new { error = "User not found." });

        return Ok(new
        {
            token           = user.EmailVerificationToken,
            isEmailVerified = user.IsEmailVerified,
        });
    }
}
