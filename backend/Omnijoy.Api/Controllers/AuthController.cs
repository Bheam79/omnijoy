using Microsoft.AspNetCore.Mvc;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

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
        // Always return 200 to prevent email enumeration
        await _auth.RequestOtpAsync(request);
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
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
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
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { error = ex.Message });
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
