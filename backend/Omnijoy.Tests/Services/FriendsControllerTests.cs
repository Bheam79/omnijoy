using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Core.DTOs;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="FriendsController"/>.</summary>
public class FriendsControllerTests
{
    private static FriendsController Build(
        Mock<IFriendService> friends,
        Mock<INotificationService> notifications,
        Guid? userId)
    {
        var controller = new FriendsController(friends.Object, notifications.Object);
        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static FriendRequestDto SampleRequest(Guid from) => new(
        FriendshipId: Guid.NewGuid(),
        From:         new FriendUserDto(from, "Alice", null),
        SentAt:       DateTime.UtcNow
    );

    private static PagedResult<FriendDto> EmptyPage() =>
        new(Array.Empty<FriendDto>(), 1, 20, false);

    // ── SendRequest ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SendRequest_ReturnsOk_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.SendRequestAsync(me, other)).ReturnsAsync(SampleRequest(me));
        notifs.Setup(n => n.CreateAsync(other, NotificationType.FriendRequest, me.ToString(), me))
              .ReturnsAsync(new Omnijoy.Core.DTOs.Notifications.NotificationDto(
                  Guid.NewGuid(), NotificationType.FriendRequest.ToString(), null, false, DateTime.UtcNow, null, null, null));
        notifs.Setup(n => n.PushTransientAsync(other, It.IsAny<string>(), It.IsAny<object>()))
              .Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.SendRequest(other);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SendRequest_ReturnsUnauthorized_WhenNoUserId()
    {
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        var controller = Build(friends, notifs, null);

        var result = await controller.SendRequest(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
        friends.Verify(f => f.SendRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SendRequest_ReturnsConflict_WhenAlreadySent()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.SendRequestAsync(me, other)).ThrowsAsync(new InvalidOperationException("already sent"));
        var controller = Build(friends, notifs, me);

        var result = await controller.SendRequest(other);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // ── AcceptRequest ────────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptRequest_ReturnsOk_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        var friendDto = new FriendDto(Guid.NewGuid(), new FriendUserDto(other, "Bob", null), 0, DateTime.UtcNow, null);
        friends.Setup(f => f.AcceptRequestAsync(me, other)).ReturnsAsync(friendDto);
        notifs.Setup(n => n.CreateAsync(other, NotificationType.FriendRequestAccepted, me.ToString(), me))
              .ReturnsAsync(new Omnijoy.Core.DTOs.Notifications.NotificationDto(
                  Guid.NewGuid(), NotificationType.FriendRequestAccepted.ToString(), null, false, DateTime.UtcNow, null, null, null));
        notifs.Setup(n => n.PushTransientAsync(other, It.IsAny<string>(), It.IsAny<object>()))
              .Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.AcceptRequest(other);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AcceptRequest_ReturnsUnauthorized_WhenNoUserId()
    {
        var friends    = new Mock<IFriendService>();
        var notifs     = new Mock<INotificationService>();
        var controller = Build(friends, notifs, null);

        var result = await controller.AcceptRequest(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AcceptRequest_ReturnsNotFound_WhenRequestMissing()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.AcceptRequestAsync(me, other)).ThrowsAsync(new KeyNotFoundException("no request"));
        var controller = Build(friends, notifs, me);

        var result = await controller.AcceptRequest(other);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── DeclineRequest ───────────────────────────────────────────────────────

    [Fact]
    public async Task DeclineRequest_ReturnsNoContent_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.DeclineRequestAsync(me, other)).Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.DeclineRequest(other);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeclineRequest_ReturnsNotFound_WhenRequestMissing()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.DeclineRequestAsync(me, other)).ThrowsAsync(new KeyNotFoundException("no"));
        var controller = Build(friends, notifs, me);

        var result = await controller.DeclineRequest(other);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── CancelRequest ────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelRequest_ReturnsNoContent_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.CancelRequestAsync(me, other)).Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.CancelRequest(other);

        result.Should().BeOfType<NoContentResult>();
    }

    // ── RemoveFriend ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveFriend_ReturnsNoContent_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.RemoveFriendAsync(me, other)).Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.RemoveFriend(other);

        result.Should().BeOfType<NoContentResult>();
    }

    // ── Block ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Block_ReturnsNoContent_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.BlockUserAsync(me, other)).Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.Block(other);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Block_ReturnsBadRequest_WhenArgumentException()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.BlockUserAsync(me, other)).ThrowsAsync(new ArgumentException("can't block self"));
        var controller = Build(friends, notifs, me);

        var result = await controller.Block(other);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── Unblock ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unblock_ReturnsNoContent_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.UnblockUserAsync(me, other)).Returns(Task.CompletedTask);
        var controller = Build(friends, notifs, me);

        var result = await controller.Unblock(other);

        result.Should().BeOfType<NoContentResult>();
    }

    // ── GetFriends ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFriends_ReturnsOk_WithPaginatedList()
    {
        var me      = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.GetFriendsAsync(me, 1, 20)).ReturnsAsync(EmptyPage());
        var controller = Build(friends, notifs, me);

        var result = await controller.GetFriends();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetPendingRequests ───────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingRequests_ReturnsOk()
    {
        var me      = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.GetPendingRequestsAsync(me)).ReturnsAsync(Array.Empty<FriendRequestDto>());
        var controller = Build(friends, notifs, me);

        var result = await controller.GetPendingRequests();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetSentRequests ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSentRequests_ReturnsOk()
    {
        var me      = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.GetSentRequestsAsync(me)).ReturnsAsync(Array.Empty<FriendRequestDto>());
        var controller = Build(friends, notifs, me);

        var result = await controller.GetSentRequests();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetSuggestions ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSuggestions_ReturnsOk()
    {
        var me      = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.GetSuggestionsAsync(me, 10)).ReturnsAsync(Array.Empty<FriendSuggestionDto>());
        var controller = Build(friends, notifs, me);

        var result = await controller.GetSuggestions();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetStatus ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatus_ReturnsOk()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.GetFriendStatusAsync(me, other)).ReturnsAsync(new FriendStatusDto("None"));
        var controller = Build(friends, notifs, me);

        var result = await controller.GetStatus(other);

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetPendingCount ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPendingCount_ReturnsOk_WithCount()
    {
        var me      = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        friends.Setup(f => f.GetPendingRequestCountAsync(me)).ReturnsAsync(3);
        var controller = Build(friends, notifs, me);

        var result = await controller.GetPendingCount();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── SetFamilyRelation ────────────────────────────────────────────────────

    [Fact]
    public async Task SetFamilyRelation_ReturnsOk_OnSuccess()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        var request = new SetFamilyRelationRequest("Sibling", "Brother");
        friends.Setup(f => f.SetFamilyRelationAsync(me, other, request))
               .ReturnsAsync(new FamilyRelationDto("Sibling", "Brother"));
        var controller = Build(friends, notifs, me);

        var result = await controller.SetFamilyRelation(other, request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SetFamilyRelation_ReturnsNotFound_WhenUserMissing()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var notifs  = new Mock<INotificationService>();
        var request = new SetFamilyRelationRequest("Sibling", "Brother");
        friends.Setup(f => f.SetFamilyRelationAsync(me, other, request)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(friends, notifs, me);

        var result = await controller.SetFamilyRelation(other, request);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
