using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs.Notifications;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="NotificationsController"/>.</summary>
public class NotificationsControllerTests
{
    private static NotificationsController Build(Mock<INotificationService> notifs, Guid? userId)
    {
        var controller = new NotificationsController(notifs.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static NotificationPageResult EmptyPage() => new(
        Items:       Array.Empty<NotificationDto>(),
        Page:        1,
        PageSize:    20,
        HasMore:     false,
        UnreadCount: 0
    );

    // ── GetNotifications ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotifications_ReturnsOk_WithPagedResult()
    {
        var userId = Guid.NewGuid();
        var notifs = new Mock<INotificationService>();
        notifs.Setup(n => n.GetNotificationsAsync(userId, 1, 20, null)).ReturnsAsync(EmptyPage());
        var controller = Build(notifs, userId);

        var result = await controller.GetNotifications();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().BeEquivalentTo(EmptyPage());
    }

    [Fact]
    public async Task GetNotifications_ReturnsUnauthorized_WhenNoUserId()
    {
        var notifs     = new Mock<INotificationService>();
        var controller = Build(notifs, null);

        var result = await controller.GetNotifications();

        result.Should().BeOfType<UnauthorizedResult>();
        notifs.Verify(n => n.GetNotificationsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string[]?>()), Times.Never);
    }

    [Fact]
    public async Task GetNotifications_PassesTypeFilter_WhenProvided()
    {
        var userId = Guid.NewGuid();
        var types  = new[] { "FriendRequest" };
        var notifs = new Mock<INotificationService>();
        notifs.Setup(n => n.GetNotificationsAsync(userId, 1, 20, types)).ReturnsAsync(EmptyPage());
        var controller = Build(notifs, userId);

        var result = await controller.GetNotifications(types);

        result.Should().BeOfType<OkObjectResult>();
        notifs.Verify(n => n.GetNotificationsAsync(userId, 1, 20, types), Times.Once);
    }

    // ── GetUnreadCount ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCount_ReturnsOk_WithCount()
    {
        var userId = Guid.NewGuid();
        var notifs = new Mock<INotificationService>();
        notifs.Setup(n => n.GetUnreadCountAsync(userId)).ReturnsAsync(5);
        var controller = Build(notifs, userId);

        var result = await controller.GetUnreadCount();

        result.Should().BeOfType<OkObjectResult>();
        var value = (dynamic)((OkObjectResult)result).Value!;
        ((int)value.count).Should().Be(5);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsUnauthorized_WhenNoUserId()
    {
        var notifs     = new Mock<INotificationService>();
        var controller = Build(notifs, null);

        var result = await controller.GetUnreadCount();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── MarkRead ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkRead_ReturnsNoContent_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var notifId = Guid.NewGuid();
        var notifs  = new Mock<INotificationService>();
        notifs.Setup(n => n.MarkReadAsync(notifId, userId)).Returns(Task.CompletedTask);
        var controller = Build(notifs, userId);

        var result = await controller.MarkRead(notifId);

        result.Should().BeOfType<NoContentResult>();
        notifs.Verify(n => n.MarkReadAsync(notifId, userId), Times.Once);
    }

    [Fact]
    public async Task MarkRead_ReturnsUnauthorized_WhenNoUserId()
    {
        var notifs     = new Mock<INotificationService>();
        var controller = Build(notifs, null);

        var result = await controller.MarkRead(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    // ── MarkAllRead ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllRead_ReturnsNoContent_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var notifs = new Mock<INotificationService>();
        notifs.Setup(n => n.MarkAllReadAsync(userId)).Returns(Task.CompletedTask);
        var controller = Build(notifs, userId);

        var result = await controller.MarkAllRead();

        result.Should().BeOfType<NoContentResult>();
        notifs.Verify(n => n.MarkAllReadAsync(userId), Times.Once);
    }

    [Fact]
    public async Task MarkAllRead_ReturnsUnauthorized_WhenNoUserId()
    {
        var notifs     = new Mock<INotificationService>();
        var controller = Build(notifs, null);

        var result = await controller.MarkAllRead();

        result.Should().BeOfType<UnauthorizedResult>();
    }
}
