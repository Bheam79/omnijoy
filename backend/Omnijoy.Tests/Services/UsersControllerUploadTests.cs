using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for file-upload actions on <see cref="UsersController"/>
/// (avatar and cover photo uploads).
/// </summary>
public class UsersControllerUploadTests
{
    private static UsersController Build(
        Mock<IUserService>     users,
        Mock<IFriendService>   friends,
        Mock<IPresenceTracker> presence,
        Mock<ISlugService>     slugs,
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

    private static UserDto SampleDto(Guid id) => new(
        id, "u@t.com", "Alice", "/uploads/avatar.jpg", null, null,
        "NotDisclosed", null, false, DateTime.UtcNow);

    private static IFormFile MockFile(string name = "photo.jpg", long length = 1024)
    {
        var stream   = new MemoryStream(new byte[length]);
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(name);
        mockFile.Setup(f => f.Length).Returns(length);
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        return mockFile.Object;
    }

    // ── UploadAvatar ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAvatar_ReturnsOk_WithUpdatedDto()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.UploadAvatarAsync(userId, It.IsAny<Stream>(), "photo.jpg"))
             .ReturnsAsync(SampleDto(userId));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadAvatar(MockFile("photo.jpg"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UploadAvatar_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.UploadAvatar(MockFile());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UploadAvatar_ReturnsBadRequest_WhenNoFile()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadAvatar(null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadAvatar_ReturnsBadRequest_WhenFileIsEmpty()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadAvatar(MockFile("photo.jpg", 0));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadAvatar_ReturnsNotFound_WhenUserMissing()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.UploadAvatarAsync(userId, It.IsAny<Stream>(), It.IsAny<string>()))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadAvatar(MockFile());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UploadAvatar_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.UploadAvatarAsync(userId, It.IsAny<Stream>(), It.IsAny<string>()))
             .ThrowsAsync(new ArgumentException("unsupported format"));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadAvatar(MockFile());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UploadCover ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadCover_ReturnsOk_WithUpdatedDto()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        users.Setup(u => u.UploadCoverAsync(userId, It.IsAny<Stream>(), "cover.jpg"))
             .ReturnsAsync(SampleDto(userId));
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadCover(MockFile("cover.jpg"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UploadCover_ReturnsUnauthorized_WhenNoUserId()
    {
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, null);

        var result = await controller.UploadCover(MockFile());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UploadCover_ReturnsBadRequest_WhenNoFile()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadCover(null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadCover_ReturnsBadRequest_WhenFileIsEmpty()
    {
        var userId = Guid.NewGuid();
        var (users, friends, presence, slugs) = Mocks();
        var controller = Build(users, friends, presence, slugs, userId);

        var result = await controller.UploadCover(MockFile("cover.jpg", 0));

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
