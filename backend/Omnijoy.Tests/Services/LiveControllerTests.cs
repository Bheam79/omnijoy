using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Moq;
using Omnijoy.Api.Controllers;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.DTOs.Live;
using Omnijoy.Core.Interfaces;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="LiveController"/>.</summary>
public class LiveControllerTests
{
    private static LiveController Build(
        Mock<ILiveStreamService>        live,
        Mock<IHubContext<LiveHub>>       liveHub,
        Mock<INotificationService>       notifications,
        Mock<IHttpClientFactory>         httpClientFactory,
        Guid? userId)
    {
        var clients = new Mock<IHubClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        liveHub.Setup(h => h.Clients).Returns(clients.Object);

        var config     = new ConfigurationBuilder().Build();
        var controller = new LiveController(
            live.Object, liveHub.Object, notifications.Object,
            httpClientFactory.Object, config);

        var http = new DefaultHttpContext();
        if (userId is { } id)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static StartStreamResponse SampleStartResponse(Guid id) => new(
        Id:        id,
        Title:     "My Stream",
        StreamKey: "key-123",
        IngestUrl: "rtmp://localhost/live",
        HlsUrl:    "/api/live/" + id + "/hls/index.m3u8"
    );

    private static LiveStreamDto SampleStreamDto(Guid id) => new(
        Id:          id,
        Host:        new LiveStreamHostDto(Guid.NewGuid(), "Host", null),
        Title:       "Test stream",
        Status:      "Live",
        Privacy:     "Everyone",
        ViewerCount: 5,
        HlsUrl:      "/api/live/" + id + "/hls/index.m3u8",
        StartedAt:   DateTime.UtcNow,
        EndedAt:     null
    );

    // ── StartStream ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StartStream_ReturnsOk_OnSuccess()
    {
        var userId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var live      = new Mock<ILiveStreamService>();
        var liveHub   = new Mock<IHubContext<LiveHub>>();
        var notifs    = new Mock<INotificationService>();
        var http      = new Mock<IHttpClientFactory>();
        var request   = new StartStreamRequest("My Stream", "Everyone");
        live.Setup(l => l.StartStreamAsync(userId, request)).ReturnsAsync(SampleStartResponse(streamId));
        live.Setup(l => l.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid>());
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.StartStream(request);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task StartStream_ReturnsUnauthorized_WhenNoUserId()
    {
        var live      = new Mock<ILiveStreamService>();
        var liveHub   = new Mock<IHubContext<LiveHub>>();
        var notifs    = new Mock<INotificationService>();
        var http      = new Mock<IHttpClientFactory>();
        var controller = Build(live, liveHub, notifs, http, null);

        var result = await controller.StartStream(new StartStreamRequest("s", "Everyone"));

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task StartStream_ReturnsBadRequest_WhenArgumentException()
    {
        var userId  = Guid.NewGuid();
        var live    = new Mock<ILiveStreamService>();
        var liveHub = new Mock<IHubContext<LiveHub>>();
        var notifs  = new Mock<INotificationService>();
        var http    = new Mock<IHttpClientFactory>();
        live.Setup(l => l.StartStreamAsync(userId, It.IsAny<StartStreamRequest>()))
            .ThrowsAsync(new ArgumentException("already live"));
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.StartStream(new StartStreamRequest("s", "Everyone"));

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task StartStream_NotifiesFriends_WhenFriendsExist()
    {
        var userId    = Guid.NewGuid();
        var friendId  = Guid.NewGuid();
        var streamId  = Guid.NewGuid();
        var live      = new Mock<ILiveStreamService>();
        var liveHub   = new Mock<IHubContext<LiveHub>>();
        var notifs    = new Mock<INotificationService>();
        var http      = new Mock<IHttpClientFactory>();
        var request   = new StartStreamRequest("Live!", "Friends");

        var clients = new Mock<IHubClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        liveHub.Setup(h => h.Clients).Returns(clients.Object);

        live.Setup(l => l.StartStreamAsync(userId, request)).ReturnsAsync(SampleStartResponse(streamId));
        live.Setup(l => l.GetFriendIdsAsync(userId)).ReturnsAsync(new List<Guid> { friendId });
        notifs.Setup(n => n.CreateForManyAsync(
            It.IsAny<IEnumerable<Guid>>(),
            Omnijoy.Core.Models.Enums.NotificationType.LiveStreamStarted,
            streamId.ToString(),
            userId)).Returns(Task.CompletedTask);

        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.StartStream(request);

        result.Should().BeOfType<OkObjectResult>();
        notifs.Verify(n => n.CreateForManyAsync(
            It.IsAny<IEnumerable<Guid>>(),
            Omnijoy.Core.Models.Enums.NotificationType.LiveStreamStarted,
            It.IsAny<string>(),
            userId), Times.Once);
    }

    // ── EndStream ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndStream_ReturnsNoContent_OnSuccess()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var live     = new Mock<ILiveStreamService>();
        var liveHub  = new Mock<IHubContext<LiveHub>>();
        var notifs   = new Mock<INotificationService>();
        var http     = new Mock<IHttpClientFactory>();
        live.Setup(l => l.EndStreamAsync(streamId, userId)).Returns(Task.CompletedTask);
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.EndStream(streamId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task EndStream_ReturnsUnauthorized_WhenNoUserId()
    {
        var live    = new Mock<ILiveStreamService>();
        var liveHub = new Mock<IHubContext<LiveHub>>();
        var notifs  = new Mock<INotificationService>();
        var http    = new Mock<IHttpClientFactory>();
        var controller = Build(live, liveHub, notifs, http, null);

        var result = await controller.EndStream(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task EndStream_ReturnsNotFound_WhenStreamMissing()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var live     = new Mock<ILiveStreamService>();
        var liveHub  = new Mock<IHubContext<LiveHub>>();
        var notifs   = new Mock<INotificationService>();
        var http     = new Mock<IHttpClientFactory>();
        live.Setup(l => l.EndStreamAsync(streamId, userId)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.EndStream(streamId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task EndStream_Returns403_WhenNotHost()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var live     = new Mock<ILiveStreamService>();
        var liveHub  = new Mock<IHubContext<LiveHub>>();
        var notifs   = new Mock<INotificationService>();
        var http     = new Mock<IHttpClientFactory>();
        live.Setup(l => l.EndStreamAsync(streamId, userId)).ThrowsAsync(new UnauthorizedAccessException("not host"));
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.EndStream(streamId);

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    // ── GetActiveStreams ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveStreams_ReturnsOk_WithList()
    {
        var userId = Guid.NewGuid();
        var live   = new Mock<ILiveStreamService>();
        var liveHub = new Mock<IHubContext<LiveHub>>();
        var notifs  = new Mock<INotificationService>();
        var http    = new Mock<IHttpClientFactory>();
        live.Setup(l => l.GetActiveStreamsAsync(userId)).ReturnsAsync(new List<LiveStreamDto>());
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.GetActiveStreams();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetActiveStreams_ReturnsUnauthorized_WhenNoUserId()
    {
        var live    = new Mock<ILiveStreamService>();
        var liveHub = new Mock<IHubContext<LiveHub>>();
        var notifs  = new Mock<INotificationService>();
        var http    = new Mock<IHttpClientFactory>();
        var controller = Build(live, liveHub, notifs, http, null);

        var result = await controller.GetActiveStreams();

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // ── GetStream ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStream_ReturnsOk_WithDto()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var live     = new Mock<ILiveStreamService>();
        var liveHub  = new Mock<IHubContext<LiveHub>>();
        var notifs   = new Mock<INotificationService>();
        var http     = new Mock<IHttpClientFactory>();
        live.Setup(l => l.GetStreamAsync(streamId, userId)).ReturnsAsync(SampleStreamDto(streamId));
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.GetStream(streamId);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetStream_ReturnsNotFound_WhenMissing()
    {
        var userId   = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var live     = new Mock<ILiveStreamService>();
        var liveHub  = new Mock<IHubContext<LiveHub>>();
        var notifs   = new Mock<INotificationService>();
        var http     = new Mock<IHttpClientFactory>();
        live.Setup(l => l.GetStreamAsync(streamId, userId)).ThrowsAsync(new KeyNotFoundException("not found"));
        var controller = Build(live, liveHub, notifs, http, userId);

        var result = await controller.GetStream(streamId);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
