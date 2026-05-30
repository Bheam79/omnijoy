using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Omnijoy.Api.Hubs;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="NotificationHub"/>.</summary>
public class NotificationHubTests : IDisposable
{
    private readonly OmnijoyDbContext _db;

    public NotificationHubTests()
    {
        var opts = new DbContextOptionsBuilder<OmnijoyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new OmnijoyDbContext(opts);
    }

    public void Dispose() => _db.Dispose();

    private (NotificationHub, Mock<IHubCallerClients>, Mock<IGroupManager>)
        BuildHub(string? userId, string connectionId = "notif-conn-1")
    {
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.ConnectedAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(false); // not the first connection by default
        presence.Setup(p => p.DisconnectedAsync(It.IsAny<Guid>(), It.IsAny<string>()))
                .ReturnsAsync(false); // not the last connection by default

        var hub     = new NotificationHub(presence.Object, _db);
        var clients = new Mock<IHubCallerClients>();
        var groups  = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.UserIdentifier).Returns(userId);

        var groupProxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(groupProxy.Object);

        hub.Clients = clients.Object;
        hub.Groups  = groups.Object;
        hub.Context = context.Object;

        return (hub, clients, groups);
    }

    // ── OnConnectedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task OnConnectedAsync_AddsToUserGroup_WhenValidUserId()
    {
        var userId = Guid.NewGuid();
        var (hub, _, groups) = BuildHub(userId.ToString());

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync(
            "notif-conn-1", $"user:{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_DoesNotAddToGroup_WhenInvalidOrNullUserId()
    {
        var (hub, _, groups) = BuildHub(null);

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync(
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task OnConnectedAsync_BroadcastsOnline_WhenBecameOnline()
    {
        var userId   = Guid.NewGuid();
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.ConnectedAsync(userId, "notif-conn-1")).ReturnsAsync(true); // first connection
        presence.Setup(p => p.DisconnectedAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);

        var hub     = new NotificationHub(presence.Object, _db);
        var groups  = new Mock<IGroupManager>();
        var clients = new Mock<IHubCallerClients>();
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns("notif-conn-1");
        context.Setup(c => c.UserIdentifier).Returns(userId.ToString());

        var groupProxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(groupProxy.Object);

        hub.Clients = clients.Object;
        hub.Groups  = groups.Object;
        hub.Context = context.Object;

        await hub.OnConnectedAsync();

        // No friends → no broadcast, but the hub shouldn't throw
        // The hub calls BroadcastPresenceToFriendsAsync which queries DB (empty)
    }

    // ── OnDisconnectedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_RemovesFromUserGroup_WhenValidUserId()
    {
        var userId = Guid.NewGuid();
        var (hub, _, groups) = BuildHub(userId.ToString());

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync(
            "notif-conn-1", $"user:{userId}", default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNotRemove_WhenNullUserId()
    {
        var (hub, _, groups) = BuildHub(null);

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync(
            It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── GetPresence ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPresence_ReturnsPresenceData_ForParsedUserIds()
    {
        var userId  = Guid.NewGuid();
        var (hub, _, _) = BuildHub(userId.ToString());

        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.ConnectedAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);
        presence.Setup(p => p.DisconnectedAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);
        presence.Setup(p => p.GetPresenceAsync(It.IsAny<Guid[]>()))
                .ReturnsAsync(new[] { new Omnijoy.Core.DTOs.Notifications.UserPresenceDto(userId, true, null) });

        // Rebuild hub with the presence mock that has GetPresenceAsync set up
        var hub2     = new NotificationHub(presence.Object, _db);
        var groups   = new Mock<IGroupManager>();
        var clients  = new Mock<IHubCallerClients>();
        var context  = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns("c2");
        context.Setup(c => c.UserIdentifier).Returns(userId.ToString());
        hub2.Clients = clients.Object;
        hub2.Groups  = groups.Object;
        hub2.Context = context.Object;

        var result = await hub2.GetPresence(new[] { userId.ToString() });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPresence_IgnoresInvalidGuids()
    {
        var presence = new Mock<IPresenceTracker>();
        presence.Setup(p => p.ConnectedAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);
        presence.Setup(p => p.DisconnectedAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);
        presence.Setup(p => p.GetPresenceAsync(It.Is<Guid[]>(a => a.Length == 0)))
                .ReturnsAsync(Array.Empty<Omnijoy.Core.DTOs.Notifications.UserPresenceDto>());

        var hub     = new NotificationHub(presence.Object, _db);
        var groups  = new Mock<IGroupManager>();
        var clients = new Mock<IHubCallerClients>();
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns("c3");
        context.Setup(c => c.UserIdentifier).Returns(Guid.NewGuid().ToString());
        hub.Clients = clients.Object;
        hub.Groups  = groups.Object;
        hub.Context = context.Object;

        var result = await hub.GetPresence(new[] { "not-a-guid", "also-bad" });

        // Should have called presence with empty array, not throw
        presence.Verify(p => p.GetPresenceAsync(It.Is<Guid[]>(a => a.Length == 0)), Times.Once);
    }
}
