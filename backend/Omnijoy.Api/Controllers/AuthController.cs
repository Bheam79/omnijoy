using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Omnijoy.Api.RateLimiting;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;

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

    public AuthController(IAuthService auth, ILogger<AuthController> logger)
    {
        _auth   = auth;
        _logger = logger;
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
}
