using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Events;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models.Enums;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Extended tests for <see cref="EventsController"/> form-upload actions.
/// </summary>
public class EventsControllerExtendedTests
{
    private static EventsController Build(
        Mock<IEventService>        events,
        Mock<IHubContext<FeedHub>> feedHub,
        Mock<INotificationService> notifs,
        Guid? userId)
    {
        var clients = new Mock<IHubClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        feedHub.Setup(h => h.Clients).Returns(clients.Object);

        var controller = new EventsController(events.Object, feedHub.Object, notifs.Object);
        var http = new DefaultHttpContext();
        // Pre-populate the form so Request.Form.Files doesn't throw.
        http.Request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection());
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static EventDto SampleEvent(Guid id, Guid creatorId) => new(
        Id:            id,
        Creator:       new EventCreatorDto(creatorId, "Alice", null),
        CompanyPageId: null,
        Title:         "Test Event",
        Description:   null,
        StartAt:       DateTime.UtcNow.AddDays(1),
        EndAt:         null,
        Location:      null,
        CoverImageUrl: null,
        Privacy:       "Everyone",
        MyRsvp:        null,
        GoingCount:    0,
        MaybeCount:    0,
        NotGoingCount: 0,
        CreatedAt:     DateTime.UtcNow
    );

    // ── CreateEvent ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_ReturnsCreated_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();

        events.Setup(e => e.CreateEventAsync(userId, It.IsAny<CreateEventRequest>(), null))
              .ReturnsAsync(SampleEvent(eventId, userId));
        events.Setup(e => e.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid>());
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.CreateEvent(new CreateEventFormInput
        {
            Title    = "My Event",
            StartAt  = DateTime.UtcNow.AddDays(7),
            Privacy  = "Everyone",
        });

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task CreateEvent_ReturnsUnauthorized_WhenNoUserId()
    {
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.CreateEvent(new CreateEventFormInput
        {
            Title   = "My Event",
            StartAt = DateTime.UtcNow.AddDays(7),
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreateEvent_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.CreateEventAsync(userId, It.IsAny<CreateEventRequest>(), null))
              .ThrowsAsync(new ArgumentException("invalid date"));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.CreateEvent(new CreateEventFormInput
        {
            Title   = "",
            StartAt = DateTime.UtcNow.AddDays(-1),
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateEvent_NotifiesFriends_WhenFriendsExist()
    {
        var userId   = Guid.NewGuid();
        var friendId = Guid.NewGuid();
        var eventId  = Guid.NewGuid();
        var events   = new Mock<IEventService>();
        var feedHub  = new Mock<IHubContext<FeedHub>>();
        var notifs   = new Mock<INotificationService>();

        events.Setup(e => e.CreateEventAsync(userId, It.IsAny<CreateEventRequest>(), null))
              .ReturnsAsync(SampleEvent(eventId, userId));
        events.Setup(e => e.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid> { friendId });
        notifs.Setup(n => n.CreateForManyAsync(
            It.IsAny<IEnumerable<Guid>>(), NotificationType.EventCreatedByFriend,
            It.IsAny<string?>(), userId)).Returns(Task.CompletedTask);

        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.CreateEvent(new CreateEventFormInput
        {
            Title   = "Party",
            StartAt = DateTime.UtcNow.AddDays(3),
            Privacy = "Friends",
        });

        result.Should().BeOfType<CreatedResult>();
        notifs.Verify(n => n.CreateForManyAsync(
            It.IsAny<IEnumerable<Guid>>(), NotificationType.EventCreatedByFriend,
            It.IsAny<string?>(), userId), Times.Once);
    }

    // ── UpdateEvent ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEvent_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();

        events.Setup(e => e.UpdateEventAsync(eventId, userId, It.IsAny<UpdateEventRequest>(), null))
              .ReturnsAsync(SampleEvent(eventId, userId));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.UpdateEvent(eventId, new UpdateEventFormInput
        {
            Title = "Updated Title",
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateEvent_ReturnsUnauthorized_WhenNoUserId()
    {
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.UpdateEvent(Guid.NewGuid(), new UpdateEventFormInput());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task UpdateEvent_ReturnsNotFound_WhenEventMissing()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.UpdateEventAsync(eventId, userId, It.IsAny<UpdateEventRequest>(), null))
              .ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.UpdateEvent(eventId, new UpdateEventFormInput());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateEvent_Returns403_WhenNotCreator()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.UpdateEventAsync(eventId, userId, It.IsAny<UpdateEventRequest>(), null))
              .ThrowsAsync(new UnauthorizedAccessException("not creator"));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.UpdateEvent(eventId, new UpdateEventFormInput());

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }
}
