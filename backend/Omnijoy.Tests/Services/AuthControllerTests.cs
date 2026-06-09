using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="AuthController"/>.</summary>
public class AuthControllerTests
{
    private static AuthController Build(Mock<IAuthService> auth, Guid? userId = null)
    {
        var logger = new Mock<ILogger<AuthController>>();
        var env    = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns("Development");

        var dbOptions = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OmnijoyDbContext(dbOptions);

        var controller = new AuthController(auth.Object, logger.Object, env.Object, db);
        var http       = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static AuthResponse SampleAuthResponse() => new(
        AccessToken:  "access",
        RefreshToken: "refresh",
        ExpiresIn:    3600,
        User: new UserDto(Guid.NewGuid(), "u@t.com", "Test User",
            null, null, null, "NotDisclosed", null, false, DateTime.UtcNow));

    // ── Register ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new RegisterRequest("a@b.com", "Alice", "password", "pass1", "Female");
        auth.Setup(a => a.RegisterAsync(request)).ReturnsAsync(SampleAuthResponse());
        var controller = Build(auth);

        var result = await controller.Register(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var auth    = new Mock<IAuthService>();
        var request = new RegisterRequest("a@b.com", "Alice", "password", "pass1");
        auth.Setup(a => a.RegisterAsync(request)).ThrowsAsync(new InvalidOperationException("exists"));
        var controller = Build(auth);

        var result = await controller.Register(request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenArgumentInvalid()
    {
        var auth    = new Mock<IAuthService>();
        var request = new RegisterRequest("bad", "Alice", "password", null);
        auth.Setup(a => a.RegisterAsync(request)).ThrowsAsync(new ArgumentException("bad email"));
        var controller = Build(auth);

        var result = await controller.Register(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new LoginPasswordRequest("a@b.com", "pass");
        auth.Setup(a => a.LoginWithPasswordAsync(request)).ReturnsAsync(SampleAuthResponse());
        var controller = Build(auth);

        var result = await controller.Login(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenCredentialsWrong()
    {
        var auth    = new Mock<IAuthService>();
        var request = new LoginPasswordRequest("a@b.com", "wrong");
        auth.Setup(a => a.LoginWithPasswordAsync(request)).ThrowsAsync(new UnauthorizedAccessException("bad"));
        var controller = Build(auth);

        var result = await controller.Login(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── OtpRequest ───────────────────────────────────────────────────────────

    [Fact]
    public async Task OtpRequest_AlwaysReturnsOk_EvenOnSmtpFailure()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OtpRequestDto("a@b.com");
        auth.Setup(a => a.RequestOtpAsync(request))
            .ThrowsAsync(new System.Net.Mail.SmtpException("no smtp"));
        var controller = Build(auth);

        var result = await controller.OtpRequest(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task OtpRequest_AlwaysReturnsOk_EvenOnUnexpectedError()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OtpRequestDto("a@b.com");
        auth.Setup(a => a.RequestOtpAsync(request)).ThrowsAsync(new Exception("unexpected"));
        var controller = Build(auth);

        var result = await controller.OtpRequest(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task OtpRequest_ReturnsOk_WhenSucceeds()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OtpRequestDto("a@b.com");
        auth.Setup(a => a.RequestOtpAsync(request)).Returns(Task.CompletedTask);
        var controller = Build(auth);

        var result = await controller.OtpRequest(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── OtpVerify ────────────────────────────────────────────────────────────

    [Fact]
    public async Task OtpVerify_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OtpVerifyDto("a@b.com", "123456");
        auth.Setup(a => a.VerifyOtpAsync(request)).ReturnsAsync(SampleAuthResponse());
        var controller = Build(auth);

        var result = await controller.OtpVerify(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task OtpVerify_ReturnsUnauthorized_WhenInvalidCode()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OtpVerifyDto("a@b.com", "000000");
        auth.Setup(a => a.VerifyOtpAsync(request)).ThrowsAsync(new UnauthorizedAccessException("bad code"));
        var controller = Build(auth);

        var result = await controller.OtpVerify(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GoogleLogin ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleLogin_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OAuthRequest("google-token");
        auth.Setup(a => a.LoginWithGoogleAsync("google-token")).ReturnsAsync(SampleAuthResponse());
        var controller = Build(auth);

        var result = await controller.GoogleLogin(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GoogleLogin_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OAuthRequest("bad");
        auth.Setup(a => a.LoginWithGoogleAsync("bad")).ThrowsAsync(new InvalidOperationException("bad token"));
        var controller = Build(auth);

        var result = await controller.GoogleLogin(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GoogleLogin_ReturnsUnauthorized_WhenAccessDenied()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OAuthRequest("blocked");
        auth.Setup(a => a.LoginWithGoogleAsync("blocked")).ThrowsAsync(new UnauthorizedAccessException("denied"));
        var controller = Build(auth);

        var result = await controller.GoogleLogin(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── FacebookLogin ────────────────────────────────────────────────────────

    [Fact]
    public async Task FacebookLogin_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OAuthRequest("fb-token");
        auth.Setup(a => a.LoginWithFacebookAsync("fb-token")).ReturnsAsync(SampleAuthResponse());
        var controller = Build(auth);

        var result = await controller.FacebookLogin(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task FacebookLogin_ReturnsUnauthorized_WhenInvalidOperation()
    {
        var auth    = new Mock<IAuthService>();
        var request = new OAuthRequest("bad");
        auth.Setup(a => a.LoginWithFacebookAsync("bad")).ThrowsAsync(new InvalidOperationException("bad token"));
        var controller = Build(auth);

        var result = await controller.FacebookLogin(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new RefreshTokenRequest("refresh-token");
        auth.Setup(a => a.RefreshAsync("refresh-token")).ReturnsAsync(SampleAuthResponse());
        var controller = Build(auth);

        var result = await controller.Refresh(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenTokenInvalid()
    {
        var auth    = new Mock<IAuthService>();
        var request = new RefreshTokenRequest("expired");
        auth.Setup(a => a.RefreshAsync("expired")).ThrowsAsync(new UnauthorizedAccessException("expired"));
        var controller = Build(auth);

        var result = await controller.Refresh(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ReturnsOk()
    {
        var auth    = new Mock<IAuthService>();
        var request = new LogoutRequest("refresh-token", "access-token");
        auth.Setup(a => a.LogoutAsync("refresh-token", "access-token")).Returns(Task.CompletedTask);
        var controller = Build(auth);

        var result = await controller.Logout(request);

        result.Should().BeOfType<OkObjectResult>();
        auth.Verify(a => a.LogoutAsync("refresh-token", "access-token"), Times.Once);
    }

    // ── PasswordForgot ────────────────────────────────────────────────────────

    [Fact]
    public async Task PasswordForgot_ReturnsOk_WithGenericMessage()
    {
        var auth    = new Mock<IAuthService>();
        var request = new PasswordResetRequestDto { Email = "user@example.com" };
        auth.Setup(a => a.RequestPasswordResetAsync(It.IsAny<PasswordResetRequestDto>()))
            .Returns(Task.CompletedTask);
        var controller = Build(auth);

        var result = await controller.PasswordForgot(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        json.Should().Contain("registered");
    }

    [Fact]
    public async Task PasswordForgot_ReturnsOk_EvenWhenSmtpThrows()
    {
        var auth    = new Mock<IAuthService>();
        var request = new PasswordResetRequestDto { Email = "user@example.com" };
        auth.Setup(a => a.RequestPasswordResetAsync(It.IsAny<PasswordResetRequestDto>()))
            .ThrowsAsync(new System.Net.Mail.SmtpException("smtp unreachable"));
        var controller = Build(auth);

        var result = await controller.PasswordForgot(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PasswordForgot_ReturnsOk_EvenWhenUnexpectedThrows()
    {
        var auth    = new Mock<IAuthService>();
        var request = new PasswordResetRequestDto { Email = "user@example.com" };
        auth.Setup(a => a.RequestPasswordResetAsync(It.IsAny<PasswordResetRequestDto>()))
            .ThrowsAsync(new Exception("something unexpected"));
        var controller = Build(auth);

        var result = await controller.PasswordForgot(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── PasswordReset ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PasswordReset_ReturnsOk_OnSuccess()
    {
        var auth    = new Mock<IAuthService>();
        var request = new PasswordResetConfirmDto { Email = "user@example.com", Code = "123456", NewPassword = "NewPass1!" };
        auth.Setup(a => a.ConfirmPasswordResetAsync(It.IsAny<PasswordResetConfirmDto>()))
            .Returns(Task.CompletedTask);
        var controller = Build(auth);

        var result = await controller.PasswordReset(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task PasswordReset_Returns400_OnArgumentException()
    {
        var auth    = new Mock<IAuthService>();
        var request = new PasswordResetConfirmDto { Email = "user@example.com", Code = "123456", NewPassword = "bad" };
        auth.Setup(a => a.ConfirmPasswordResetAsync(It.IsAny<PasswordResetConfirmDto>()))
            .ThrowsAsync(new ArgumentException("Password must be at least 8 characters."));
        var controller = Build(auth);

        var result = await controller.PasswordReset(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PasswordReset_Returns401_OnUnauthorized()
    {
        var auth    = new Mock<IAuthService>();
        var request = new PasswordResetConfirmDto { Email = "user@example.com", Code = "000000", NewPassword = "NewPass1!" };
        auth.Setup(a => a.ConfirmPasswordResetAsync(It.IsAny<PasswordResetConfirmDto>()))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid or expired reset code."));
        var controller = Build(auth);

        var result = await controller.PasswordReset(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
