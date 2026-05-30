using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="AccountController"/>.</summary>
public class AccountControllerTests
{
    private static AccountController Build(Mock<IAccountService> account, Guid? userId)
    {
        var controller = new AccountController(account.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static NotificationPreferencesDto SamplePrefs() => new(
        LikesOnMyPosts:      true,
        CommentsOnMyPosts:   true,
        PostShares:          true,
        FriendRequests:      true,
        NewFollower:         false,
        Mentions:            true,
        NewPostsFromFriends: true,
        FamilyRelations:     false,
        EventInvites:        true,
        DirectMessages:      true,
        LiveStreams:          false,
        CompanyPageInvites:  true
    );

    // ── ChangeEmail ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeEmail_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.ChangeEmailAsync(userId, "new@t.com", "pass"))
               .Returns(Task.CompletedTask);
        var controller = Build(account, userId);

        var result = await controller.ChangeEmail(new ChangeEmailRequest("new@t.com", "pass"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ChangeEmail_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.ChangeEmailAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
               .ThrowsAsync(new ArgumentException("bad email"));
        var controller = Build(account, userId);

        var result = await controller.ChangeEmail(new ChangeEmailRequest("x", "p"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangeEmail_ReturnsUnauthorized_WhenNoUserId()
    {
        var account    = new Mock<IAccountService>();
        var controller = Build(account, userId: null);

        var result = await controller.ChangeEmail(new ChangeEmailRequest("new@t.com", "pass"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
        account.Verify(a => a.ChangeEmailAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChangeEmail_ReturnsUnauthorized_WhenPasswordWrong()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.ChangeEmailAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
               .ThrowsAsync(new UnauthorizedAccessException("bad password"));
        var controller = Build(account, userId);

        var result = await controller.ChangeEmail(new ChangeEmailRequest("new@t.com", "wrong"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ChangeEmail_ReturnsConflict_WhenEmailTaken()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.ChangeEmailAsync(userId, It.IsAny<string>(), It.IsAny<string>()))
               .ThrowsAsync(new InvalidOperationException("taken"));
        var controller = Build(account, userId);

        var result = await controller.ChangeEmail(new ChangeEmailRequest("taken@t.com", "pass"));

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // ── ChangePassword ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.ChangePasswordAsync(userId, "old", "new1!", "new1!"))
               .Returns(Task.CompletedTask);
        var controller = Build(account, userId);

        var result = await controller.ChangePassword(new ChangePasswordRequest("old", "new1!", "new1!"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsUnauthorized_WhenNoUserId()
    {
        var account    = new Mock<IAccountService>();
        var controller = Build(account, userId: null);

        var result = await controller.ChangePassword(new ChangePasswordRequest("old", "new", "new"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ChangePassword_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.ChangePasswordAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
               .ThrowsAsync(new ArgumentException("mismatch"));
        var controller = Build(account, userId);

        var result = await controller.ChangePassword(new ChangePasswordRequest("old", "a", "b"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetNotificationPreferences ───────────────────────────────────────────

    [Fact]
    public async Task GetNotificationPreferences_ReturnsOk_WithDto()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.GetNotificationPreferencesAsync(userId))
               .ReturnsAsync(SamplePrefs());
        var controller = Build(account, userId);

        var result = await controller.GetNotificationPreferences();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(SamplePrefs());
    }

    [Fact]
    public async Task GetNotificationPreferences_ReturnsUnauthorized_WhenNoUser()
    {
        var account    = new Mock<IAccountService>();
        var controller = Build(account, null);

        var result = await controller.GetNotificationPreferences();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetNotificationPreferences_ReturnsNotFound_WhenUserMissing()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.GetNotificationPreferencesAsync(userId))
               .ThrowsAsync(new KeyNotFoundException("missing"));
        var controller = Build(account, userId);

        var result = await controller.GetNotificationPreferences();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── UpdateNotificationPreferences ────────────────────────────────────────

    [Fact]
    public async Task UpdateNotificationPreferences_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var prefs   = SamplePrefs();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.UpdateNotificationPreferencesAsync(userId, prefs)).ReturnsAsync(prefs);
        var controller = Build(account, userId);

        var result = await controller.UpdateNotificationPreferences(prefs);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateNotificationPreferences_ReturnsUnauthorized_WhenNoUser()
    {
        var account    = new Mock<IAccountService>();
        var controller = Build(account, null);

        var result = await controller.UpdateNotificationPreferences(SamplePrefs());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── Deactivate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.DeactivateAccountAsync(userId)).Returns(Task.CompletedTask);
        var controller = Build(account, userId);

        var result = await controller.Deactivate();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Deactivate_ReturnsUnauthorized_WhenNoUser()
    {
        var account    = new Mock<IAccountService>();
        var controller = Build(account, null);

        var result = await controller.Deactivate();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Deactivate_ReturnsNotFound_WhenUserMissing()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.DeactivateAccountAsync(userId)).ThrowsAsync(new KeyNotFoundException("gone"));
        var controller = Build(account, userId);

        var result = await controller.Deactivate();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Deactivate_ReturnsConflict_WhenInvalidOperation()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.DeactivateAccountAsync(userId)).ThrowsAsync(new InvalidOperationException("already deactivated"));
        var controller = Build(account, userId);

        var result = await controller.Deactivate();

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.DeleteAccountAsync(userId, "user@t.com")).Returns(Task.CompletedTask);
        var controller = Build(account, userId);

        var result = await controller.Delete(new DeleteAccountRequest("user@t.com"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_ReturnsUnauthorized_WhenNoUser()
    {
        var account    = new Mock<IAccountService>();
        var controller = Build(account, null);

        var result = await controller.Delete(new DeleteAccountRequest("user@t.com"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Delete_ReturnsBadRequest_WhenEmailMismatch()
    {
        var userId  = Guid.NewGuid();
        var account = new Mock<IAccountService>();
        account.Setup(a => a.DeleteAccountAsync(userId, It.IsAny<string>()))
               .ThrowsAsync(new ArgumentException("email mismatch"));
        var controller = Build(account, userId);

        var result = await controller.Delete(new DeleteAccountRequest("wrong@t.com"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
