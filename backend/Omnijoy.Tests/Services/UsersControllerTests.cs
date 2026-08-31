using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="UsersController"/>.</summary>
public class UsersControllerTests
{
    private static UsersController Build(
        Mock<IUserService>      users,
        Mock<IFriendService>    friends,
        Mock<IPresenceTracker>  presence,
        Mock<ISlugService>      slugs,
        Guid? userId)
    {
        var controller = new UsersController(users.Object, friends.Object, presence.Object, slugs.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static (Mock<IUserService>, Mock<IFriendService>, Mock<IPresenceTracker>, Mock<ISlugService>) Mocks()
        => (new(), new(), new(), new());

    private static UserProfileDto SampleProfile(Guid id) => new(
        Id:               id,
        DisplayName:      "Alice",
        AvatarUrl:        null,
        CoverUrl:         null,
        Bio:              null,
        Gender:           "Female",
        BirthDate:        null,
        ShowBirthDate:    false,
        FriendCount:      5,
        MutualFriendCount: null,
        IsOwnProfile:     false,
        IsFriend:         false,
        CreatedAt:        DateTime.UtcNow,
        UrlSlug:          null
    );

    // ── GetProfile ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_ReturnsOk_WithProfile()
    {
        var targetId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.GetUserProfileAsync(targetId, null)).ReturnsAsync(SampleProfile(targetId));
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetProfile(targetId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetProfile_ReturnsNotFound_WhenUserMissing()
    {
        var targetId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.GetUserProfileAsync(targetId, It.IsAny<Guid?>()))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetProfile(targetId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetProfile_Returns403_WhenAccessDenied()
    {
        var targetId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.GetUserProfileAsync(targetId, It.IsAny<Guid?>()))
             .ThrowsAsync(new UnauthorizedAccessException("private"));
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetProfile(targetId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── UpdateProfile ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_ReturnsOk_OnSuccess()
    {
        var userId   = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var request  = new UpdateProfileRequest("Bob", null, null, null, null);
        var userDto  = new Omnijoy.Core.DTOs.Auth.UserDto(userId, "u@t.com", "Bob", null, null, null, "NotDisclosed", null, false, DateTime.UtcNow);
        users.Setup(u => u.UpdateProfileAsync(userId, request)).ReturnsAsync(userDto);
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UpdateProfile(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.UpdateProfile(new UpdateProfileRequest(null, null, null, null, null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var request = new UpdateProfileRequest(null, null, null, null, null);
        users.Setup(u => u.UpdateProfileAsync(userId, It.IsAny<UpdateProfileRequest>())).ThrowsAsync(new ArgumentException("bad"));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UpdateProfile(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetUserFriends ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserFriends_ReturnsOk_WithPaginatedList()
    {
        var targetId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        friends.Setup(f => f.GetUserFriendsAsync(targetId, null, 1, 20))
               .ReturnsAsync(new PagedResult<FriendDto>(Array.Empty<FriendDto>(), 1, 20, false));
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetUserFriends(targetId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserFriends_ReturnsNotFound_WhenUserMissing()
    {
        var targetId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        friends.Setup(f => f.GetUserFriendsAsync(targetId, It.IsAny<Guid?>(), 1, 20))
               .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetUserFriends(targetId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── GetPrivacy ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPrivacy_ReturnsOk_WithSettings()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var settings = new PrivacySettingsDto(
            WhoCanSeePosts:       "Everyone",
            WhoCanSeeProfile:     "Everyone",
            WhoCanSendMessages:   "Friends",
            WhoCanSeeFriendList:  "Everyone",
            WhoCanSeeEvents:      "Everyone",
            WhoCanTagInPosts:     "Friends",
            WhoCanSeeFollowers:   "Friends"
        );
        users.Setup(u => u.GetPrivacySettingsAsync(userId)).ReturnsAsync(settings);
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.GetPrivacy();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPrivacy_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetPrivacy();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetPresence ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPresence_ReturnsOk_WithPresenceData()
    {
        var userId  = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        presence.Setup(p => p.GetPresenceAsync(It.IsAny<Guid[]>()))
                .ReturnsAsync(new[] { new UserPresenceDto(otherId, true, null) });
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.GetPresence(otherId.ToString());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetPresence_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.GetPresence("some-id");

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPresence_ReturnsEmptyArray_WhenUserIdsNull()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.GetPresence(null);

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(Array.Empty<object>());
    }

    // ── SetSlug ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetSlug_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        slugs.Setup(s => s.SetUserSlugAsync(userId, "alice", default)).ReturnsAsync("alice");
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.SetSlug(new SetSlugRequest("alice"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetSlug_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.SetSlug(new SetSlugRequest("alice"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task SetSlug_ReturnsBadRequest_WhenSlugInvalid()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        slugs.Setup(s => s.SetUserSlugAsync(userId, "INVALID!", default))
             .ThrowsAsync(new InvalidOperationException("invalid"));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.SetSlug(new SetSlugRequest("INVALID!"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UpdatePrivacy ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePrivacy_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var request  = new UpdatePrivacyRequest("Everyone", "Everyone", "Friends", "Everyone", "Everyone", "Friends");
        var settings = new PrivacySettingsDto("Everyone", "Everyone", "Friends", "Everyone", "Everyone", "Friends", "Everyone");
        users.Setup(u => u.UpdatePrivacySettingsAsync(userId, request)).ReturnsAsync(settings);
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UpdatePrivacy(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdatePrivacy_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.UpdatePrivacy(new UpdatePrivacyRequest(null, null, null, null, null, null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdatePrivacy_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var request = new UpdatePrivacyRequest("BadValue", null, null, null, null, null);
        users.Setup(u => u.UpdatePrivacySettingsAsync(userId, request)).ThrowsAsync(new ArgumentException("invalid value"));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UpdatePrivacy(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
