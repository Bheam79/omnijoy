using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.CompanyPages;
using Omnijoy.Core.DTOs.Events;
using Omnijoy.Core.DTOs.Friends;
using Omnijoy.Core.DTOs.Users;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Targeted tests covering remaining exception-path branches in controllers.
/// These plug the final gaps between the current line-coverage and the 80% target.
/// </summary>
public class ControllerExceptionPathTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ClaimsPrincipal UserPrincipal(Guid id) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));

    private static ControllerContext CtxWith(Guid? userId, bool withEmptyForm = false)
    {
        var http = new DefaultHttpContext();
        if (withEmptyForm)
            http.Request.Form = new FormCollection(
                new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
                new FormFileCollection());
        if (userId is { } id) http.User = UserPrincipal(id);
        return new ControllerContext { HttpContext = http };
    }

    private static CompanyPageDto SamplePage(Guid id) => new(
        id, "ACME", null, null, null,
        new CompanyPageCreatorDto(Guid.NewGuid(), "Alice", null),
        0, false, "Owner", DateTime.UtcNow);

    private static EventDto SampleEvent(Guid id) => new(
        id, new EventCreatorDto(Guid.NewGuid(), "Alice", null), null, null, null,
        "Event", null, DateTime.UtcNow.AddDays(1), null, null, null,
        "Everyone", null, 0, 0, 0, DateTime.UtcNow);

    // ── CompanyPagesController ────────────────────────────────────────────────

    [Fact]
    public async Task GetMyPages_ReturnsUnauthorized_WhenNoUserId()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(null);

        var result = await controller.GetMyPages();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetAdmins_ReturnsNotFound_WhenPageMissing()
    {
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.GetAdminsAsync(pageId, It.IsAny<Guid?>()))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(null);

        var result = await controller.GetAdmins(pageId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddAdmin_ReturnsNotFound_WhenPageMissing()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.AddOrUpdateAdminAsync(pageId, userId, It.IsAny<AddAdminRequest>()))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId, withEmptyForm: false);

        var result = await controller.AddAdmin(pageId, new AddAdminRequest(Guid.NewGuid(), "Editor"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddAdmin_ReturnsBadRequest_WhenInvalidOperationException()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.AddOrUpdateAdminAsync(pageId, userId, It.IsAny<AddAdminRequest>()))
             .ThrowsAsync(new InvalidOperationException("cannot add owner"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.AddAdmin(pageId, new AddAdminRequest(Guid.NewGuid(), "Owner"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RemoveAdmin_ReturnsNotFound_WhenPageMissing()
    {
        var userId   = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var pageId   = Guid.NewGuid();
        var pages    = new Mock<ICompanyPageService>();
        var slugs    = new Mock<ISlugService>();
        pages.Setup(p => p.RemoveAdminAsync(pageId, userId, targetId))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.RemoveAdmin(pageId, targetId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RemoveAdmin_Returns403_WhenNotAuthorized()
    {
        var userId   = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var pageId   = Guid.NewGuid();
        var pages    = new Mock<ICompanyPageService>();
        var slugs    = new Mock<ISlugService>();
        pages.Setup(p => p.RemoveAdminAsync(pageId, userId, targetId))
             .ThrowsAsync(new UnauthorizedAccessException("not owner"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.RemoveAdmin(pageId, targetId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveAdmin_ReturnsBadRequest_WhenInvalidOperation()
    {
        var userId   = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var pageId   = Guid.NewGuid();
        var pages    = new Mock<ICompanyPageService>();
        var slugs    = new Mock<ISlugService>();
        pages.Setup(p => p.RemoveAdminAsync(pageId, userId, targetId))
             .ThrowsAsync(new InvalidOperationException("cannot remove self"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.RemoveAdmin(pageId, targetId);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Follow_ReturnsNotFound_WhenPageMissing()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.FollowAsync(pageId, userId)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.Follow(pageId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Unfollow_ReturnsUnauthorized_WhenNoUser()
    {
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(null);

        var result = await controller.Unfollow(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Unfollow_ReturnsNotFound_WhenPageMissing()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.UnfollowAsync(pageId, userId)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.Unfollow(pageId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdatePage_ReturnsBadRequest_WhenArgumentException()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        pages.Setup(p => p.UpdatePageAsync(pageId, userId, It.IsAny<UpdateCompanyPageRequest>(), null, null))
             .ThrowsAsync(new ArgumentException("name too long"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId, withEmptyForm: true);

        var result = await controller.UpdatePage(pageId, new UpdatePageFormInput { Name = new string('x', 1000) });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SetSlug_ReturnsNotFound_WhenPageMissing()
    {
        var userId = Guid.NewGuid();
        var pageId = Guid.NewGuid();
        var pages  = new Mock<ICompanyPageService>();
        var slugs  = new Mock<ISlugService>();
        slugs.Setup(s => s.SetCompanyPageSlugAsync(pageId, userId, "acme", default))
             .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = new CompanyPagesController(pages.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.SetSlug(pageId, new SetSlugRequest("acme"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── EventsController ─────────────────────────────────────────────────────

    private static EventsController BuildEventsController(
        Mock<IEventService> events, Guid? userId)
    {
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        var clients = new Mock<IHubClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        feedHub.Setup(h => h.Clients).Returns(clients.Object);
        var controller = new EventsController(events.Object, feedHub.Object, notifs.Object);
        controller.ControllerContext = CtxWith(userId, withEmptyForm: true);
        return controller;
    }

    [Fact]
    public async Task GetEvent_Returns403_WhenAccessDenied()
    {
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        events.Setup(e => e.GetEventAsync(eventId, It.IsAny<Guid?>()))
              .ThrowsAsync(new UnauthorizedAccessException("private"));
        var controller = BuildEventsController(events, null);

        var result = await controller.GetEvent(eventId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetAttendees_Returns403_WhenAccessDenied()
    {
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        events.Setup(e => e.GetAttendeesAsync(eventId, It.IsAny<Guid?>()))
              .ThrowsAsync(new UnauthorizedAccessException("private"));
        var controller = BuildEventsController(events, null);

        var result = await controller.GetAttendees(eventId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetAttendees_ReturnsNotFound_WhenEventMissing()
    {
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        events.Setup(e => e.GetAttendeesAsync(eventId, It.IsAny<Guid?>()))
              .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = BuildEventsController(events, null);

        var result = await controller.GetAttendees(eventId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── FriendsController ─────────────────────────────────────────────────────

    private static FriendsController BuildFriendsController(
        Mock<IFriendService> friends, Guid? userId)
    {
        var notifs = new Mock<INotificationService>();
        var controller = new FriendsController(friends.Object, notifs.Object);
        controller.ControllerContext = CtxWith(userId);
        return controller;
    }

    [Fact]
    public async Task CancelRequest_ReturnsNotFound_WhenRequestMissing()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        friends.Setup(f => f.CancelRequestAsync(me, other)).ThrowsAsync(new KeyNotFoundException("no request"));
        var controller = BuildFriendsController(friends, me);

        var result = await controller.CancelRequest(other);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RemoveFriend_ReturnsNotFound_WhenFriendshipMissing()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        friends.Setup(f => f.RemoveFriendAsync(me, other)).ThrowsAsync(new KeyNotFoundException("no friendship"));
        var controller = BuildFriendsController(friends, me);

        var result = await controller.RemoveFriend(other);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Unblock_ReturnsNotFound_WhenNotBlocked()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        friends.Setup(f => f.UnblockUserAsync(me, other)).ThrowsAsync(new KeyNotFoundException("not blocked"));
        var controller = BuildFriendsController(friends, me);

        var result = await controller.Unblock(other);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetFamilyRelation_ReturnsConflict_WhenInvalidOperation()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var req   = new SetFamilyRelationRequest("Sibling", "Sister");
        friends.Setup(f => f.SetFamilyRelationAsync(me, other, req))
               .ThrowsAsync(new InvalidOperationException("not friends"));
        var controller = BuildFriendsController(friends, me);

        var result = await controller.SetFamilyRelation(other, req);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task SetFamilyRelation_ReturnsBadRequest_WhenArgumentException()
    {
        var me    = Guid.NewGuid();
        var other = Guid.NewGuid();
        var friends = new Mock<IFriendService>();
        var req   = new SetFamilyRelationRequest("Invalid", "Bad");
        friends.Setup(f => f.SetFamilyRelationAsync(me, other, req))
               .ThrowsAsync(new ArgumentException("invalid type"));
        var controller = BuildFriendsController(friends, me);

        var result = await controller.SetFamilyRelation(other, req);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── UsersController ───────────────────────────────────────────────────────

    private static UsersController BuildUsersController(
        Mock<IUserService> users, Guid? userId)
    {
        var friends  = new Mock<IFriendService>();
        var presence = new Mock<IPresenceTracker>();
        var slugs    = new Mock<ISlugService>();
        var controller = new UsersController(users.Object, friends.Object, presence.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);
        return controller;
    }

    [Fact]
    public async Task GetUserFriends_Returns403_WhenAccessDenied()
    {
        var targetId = Guid.NewGuid();
        var users    = new Mock<IUserService>();
        var friends  = new Mock<IFriendService>();
        friends.Setup(f => f.GetUserFriendsAsync(targetId, It.IsAny<Guid?>(), 1, 20))
               .ThrowsAsync(new UnauthorizedAccessException("private"));
        var presence = new Mock<IPresenceTracker>();
        var slugs    = new Mock<ISlugService>();
        var controller = new UsersController(users.Object, friends.Object, presence.Object, slugs.Object);
        controller.ControllerContext = CtxWith(null);

        var result = await controller.GetUserFriends(targetId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SetSlug_ReturnsNotFound_WhenUserMissing()
    {
        var userId = Guid.NewGuid();
        var users  = new Mock<IUserService>();
        var slugs  = new Mock<ISlugService>();
        slugs.Setup(s => s.SetUserSlugAsync(userId, "alice", default))
             .ThrowsAsync(new KeyNotFoundException("user not found"));
        var friends  = new Mock<IFriendService>();
        var presence = new Mock<IPresenceTracker>();
        var controller = new UsersController(users.Object, friends.Object, presence.Object, slugs.Object);
        controller.ControllerContext = CtxWith(userId);

        var result = await controller.SetSlug(new SetSlugRequest("alice"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_ReturnsNotFound_WhenUserMissing()
    {
        var userId = Guid.NewGuid();
        var users  = new Mock<IUserService>();
        users.Setup(u => u.UpdateProfileAsync(userId, It.IsAny<UpdateProfileRequest>()))
             .ThrowsAsync(new KeyNotFoundException("user not found"));
        var controller = BuildUsersController(users, userId);

        var result = await controller.UpdateProfile(new UpdateProfileRequest(null, null, null, null, null));

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
