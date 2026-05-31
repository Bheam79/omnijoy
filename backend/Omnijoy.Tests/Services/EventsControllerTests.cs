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

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="EventsController"/>.</summary>
public class EventsControllerTests
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
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static EventDto SampleEvent(Guid id, Guid creatorId) => new(
        Id:                  id,
        Creator:             new EventCreatorDto(creatorId, "Alice", null),
        CompanyPageId:       null,
        CompanyPageName:     null,
        CompanyPageLogoUrl:  null,
        Title:               "Test Event",
        Description:         "A test event",
        StartAt:             DateTime.UtcNow.AddDays(1),
        EndAt:               null,
        Location:            "Online",
        CoverImageUrl:       null,
        Privacy:             "Everyone",
        MyRsvp:              null,
        GoingCount:          0,
        MaybeCount:          0,
        NotGoingCount:       0,
        CreatedAt:           DateTime.UtcNow
    );

    // ── GetEvents ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvents_ReturnsOk_WithList()
    {
        var userId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.GetEventsAsync(userId, null, 1, 20))
              .ReturnsAsync(new EventsPageResult(Array.Empty<EventDto>(), 1, 20, false));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.GetEvents();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetEvents_ReturnsUnauthorized_WhenNoUserId()
    {
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.GetEvents();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetPublicEvents ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicEvents_ReturnsOk_Anonymous()
    {
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.GetPublicEventsAsync(null, 1, 20))
              .ReturnsAsync(new EventsPageResult(Array.Empty<EventDto>(), 1, 20, false));
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.GetPublicEvents();

        result.Should().BeOfType<OkObjectResult>();
    }

    // ── GetEvent ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetEvent_ReturnsOk_WithDto()
    {
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.GetEventAsync(eventId, null)).ReturnsAsync(SampleEvent(eventId, Guid.NewGuid()));
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.GetEvent(eventId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetEvent_ReturnsNotFound_WhenMissing()
    {
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.GetEventAsync(eventId, It.IsAny<Guid?>())).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.GetEvent(eventId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // ── DeleteEvent ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEvent_ReturnsNoContent_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.DeleteEventAsync(eventId, userId)).Returns(Task.CompletedTask);
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.DeleteEvent(eventId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteEvent_ReturnsUnauthorized_WhenNoUserId()
    {
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.DeleteEvent(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task DeleteEvent_Returns403_WhenNotOwner()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.DeleteEventAsync(eventId, userId)).ThrowsAsync(new UnauthorizedAccessException("not owner"));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.DeleteEvent(eventId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── Rsvp ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Rsvp_ReturnsOk_OnSuccess()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.RsvpAsync(eventId, userId, "Going")).ReturnsAsync(SampleEvent(eventId, Guid.NewGuid()));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.Rsvp(eventId, new RsvpRequest("Going"));

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Rsvp_ReturnsUnauthorized_WhenNoUserId()
    {
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.Rsvp(Guid.NewGuid(), new RsvpRequest("Going"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Rsvp_ReturnsNotFound_WhenEventMissing()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.RsvpAsync(eventId, userId, "Going")).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.Rsvp(eventId, new RsvpRequest("Going"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Rsvp_ReturnsBadRequest_WhenBadStatus()
    {
        var userId  = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.RsvpAsync(eventId, userId, "Bad")).ThrowsAsync(new ArgumentException("bad status"));
        var controller = Build(events, feedHub, notifs, userId);

        var result = await controller.Rsvp(eventId, new RsvpRequest("Bad"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ── GetAttendees ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAttendees_ReturnsOk_WithList()
    {
        var eventId = Guid.NewGuid();
        var events  = new Mock<IEventService>();
        var feedHub = new Mock<IHubContext<FeedHub>>();
        var notifs  = new Mock<INotificationService>();
        events.Setup(e => e.GetAttendeesAsync(eventId, null))
              .ReturnsAsync(new EventAttendeesResult(Array.Empty<EventAttendeeDto>(), Array.Empty<EventAttendeeDto>(), Array.Empty<EventAttendeeDto>()));
        var controller = Build(events, feedHub, notifs, null);

        var result = await controller.GetAttendees(eventId);

        result.Should().BeOfType<OkObjectResult>();
    }
}
