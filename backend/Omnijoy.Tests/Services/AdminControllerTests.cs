using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Behaviour tests for <see cref="AdminController"/> — verify each action's
/// happy / not-found / unauthenticated path. Authorization (role) checks are
/// declared via <c>[Authorize(Roles = ...)]</c> attributes and are enforced
/// by the ASP.NET Core middleware pipeline, not in the test layer.
/// </summary>
public class AdminControllerTests
{
    private static AdminController Build(
        Mock<IAdminService> admin,
        Mock<IModerationLogService> log,
        Guid? userId)
    {
        var controller = new AdminController(admin.Object, log.Object);

        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
            }, "test"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static AdminUserDto SampleDto(Guid id, bool isBanned = false) =>
        new(
            Id:          id,
            Email:       "u@t.com",
            DisplayName: "User",
            Role:        "User",
            IsActive:    true,
            IsBanned:    isBanned,
            BannedAt:    isBanned ? DateTime.UtcNow : null,
            CreatedAt:   DateTime.UtcNow
        );

    // ── BanUser ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task BanUser_ReturnsOk_WithDto()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.BanUserAsync(actorId, targetId, "spam"))
             .ReturnsAsync(SampleDto(targetId, isBanned: true));

        var log = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.BanUser(targetId, new BanUserRequest("spam"));

        result.Should().BeOfType<OkObjectResult>();
        var dto = (AdminUserDto)((OkObjectResult)result).Value!;
        dto.Id.Should().Be(targetId);
        dto.IsBanned.Should().BeTrue();
    }

    [Fact]
    public async Task BanUser_NotFound_When_Target_Missing()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.BanUserAsync(actorId, targetId, It.IsAny<string>()))
             .ThrowsAsync(new KeyNotFoundException("nope"));

        var log = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.BanUser(targetId, new BanUserRequest(null));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task BanUser_Unauthorized_When_NoUserId()
    {
        var admin = new Mock<IAdminService>();
        var log   = new Mock<IModerationLogService>();
        var controller = Build(admin, log, userId: null);

        var result = await controller.BanUser(Guid.NewGuid(), new BanUserRequest(null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
        admin.Verify(a => a.BanUserAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task BanUser_NullBody_PassesNullNotesToService()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.BanUserAsync(actorId, targetId, null))
             .ReturnsAsync(SampleDto(targetId, isBanned: true));

        var log = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.BanUser(targetId, request: null);

        result.Should().BeOfType<OkObjectResult>();
        admin.Verify(a => a.BanUserAsync(actorId, targetId, null), Times.Once);
    }

    // ── UnbanUser ────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnbanUser_ReturnsOk_WithDto()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.UnbanUserAsync(actorId, targetId, null))
             .ReturnsAsync(SampleDto(targetId, isBanned: false));

        var log = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.UnbanUser(targetId, new BanUserRequest(null));

        result.Should().BeOfType<OkObjectResult>();
        var dto = (AdminUserDto)((OkObjectResult)result).Value!;
        dto.IsBanned.Should().BeFalse();
    }

    [Fact]
    public async Task UnbanUser_NotFound_When_Target_Missing()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.UnbanUserAsync(actorId, targetId, It.IsAny<string>()))
             .ThrowsAsync(new KeyNotFoundException("nope"));

        var log = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.UnbanUser(targetId, new BanUserRequest(null));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UnbanUser_Unauthorized_When_NoUserId()
    {
        var admin = new Mock<IAdminService>();
        var log   = new Mock<IModerationLogService>();
        var controller = Build(admin, log, userId: null);

        var result = await controller.UnbanUser(Guid.NewGuid(), new BanUserRequest(null));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetAuditLog ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_Returns_PaginatedResult_From_Service()
    {
        var actorId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        var log   = new Mock<IModerationLogService>();

        var items = new[]
        {
            new ModerationLogDto(
                Id:               Guid.NewGuid(),
                ActorId:          actorId,
                ActorDisplayName: "Admin",
                Action:           ModerationAction.BanUser.ToString(),
                TargetType:       "User",
                TargetId:         Guid.NewGuid().ToString(),
                Notes:            null,
                CreatedAt:        DateTime.UtcNow),
        };
        log.Setup(l => l.ListAsync(actorId, "BanUser", 1, 20))
           .ReturnsAsync(new ModerationLogListResult(items, 1, 20, HasMore: false));

        var controller = Build(admin, log, actorId);

        var result = await controller.GetAuditLog(actorId, "BanUser");

        result.Should().BeOfType<OkObjectResult>();
        var dto = (ModerationLogListResult)((OkObjectResult)result).Value!;
        dto.Items.Should().HaveCount(1);
        dto.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuditLog_DefaultsPaging_When_NotProvided()
    {
        var admin = new Mock<IAdminService>();
        var log   = new Mock<IModerationLogService>();

        log.Setup(l => l.ListAsync(null, null, 1, 20))
           .ReturnsAsync(new ModerationLogListResult(Array.Empty<ModerationLogDto>(), 1, 20, HasMore: false));

        var controller = Build(admin, log, Guid.NewGuid());

        var result = await controller.GetAuditLog(null, null);

        result.Should().BeOfType<OkObjectResult>();
        log.Verify(l => l.ListAsync(null, null, 1, 20), Times.Once);
    }

    // ── ChangeUserRole ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChangeUserRole_InvalidRole_ReturnsBadRequest()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.ChangeUserRoleAsync(actorId, targetId, "SuperAdmin"))
             .ThrowsAsync(new ArgumentException("bad"));
        var log   = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.ChangeUserRole(targetId,
            new ChangeUserRoleRequest("SuperAdmin"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ChangeUserRole_NotFound_When_Target_Missing()
    {
        var actorId  = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var admin = new Mock<IAdminService>();
        admin.Setup(a => a.ChangeUserRoleAsync(actorId, targetId, "Moderator"))
             .ThrowsAsync(new KeyNotFoundException("nope"));
        var log   = new Mock<IModerationLogService>();
        var controller = Build(admin, log, actorId);

        var result = await controller.ChangeUserRole(targetId,
            new ChangeUserRoleRequest("Moderator"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
