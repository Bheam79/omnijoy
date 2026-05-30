using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Hubs;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="LiveHub"/>.</summary>
public class LiveHubTests
{
    private static (LiveHub, Mock<IHubCallerClients>, Mock<IGroupManager>, Mock<HubCallerContext>)
        BuildHub(string? userId = "user-xyz", string connectionId = "live-conn-1")
    {
        var hub     = new LiveHub();
        var clients = new Mock<IHubCallerClients>();
        var group   = new Mock<IClientProxy>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);

        var groups  = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.UserIdentifier).Returns(userId);

        hub.Clients = clients.Object;
        hub.Groups  = groups.Object;
        hub.Context = context.Object;

        return (hub, clients, groups, context);
    }

    // ── OnConnectedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task OnConnectedAsync_AddsToUserGroup_WhenAuthenticated()
    {
        var (hub, _, groups, _) = BuildHub(userId: "live-user");

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync("live-conn-1", "user:live-user", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_DoesNotAddToGroup_WhenAnonymous()
    {
        var (hub, _, groups, _) = BuildHub(userId: null);

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── OnDisconnectedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_RemovesFromUserGroup()
    {
        var (hub, _, groups, _) = BuildHub(userId: "live-user");

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync("live-conn-1", "user:live-user", default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNotRemove_WhenAnonymous()
    {
        var (hub, _, groups, _) = BuildHub(userId: null);

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── JoinStream ───────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinStream_AddsToStreamGroup_AndBroadcastsViewerJoined()
    {
        var streamId  = Guid.NewGuid().ToString();
        var (hub, clients, groups, _) = BuildHub();
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group($"stream:{streamId}")).Returns(proxy.Object);

        await hub.JoinStream(streamId);

        groups.Verify(g => g.AddToGroupAsync("live-conn-1", $"stream:{streamId}", default), Times.Once);
        proxy.Verify(p => p.SendCoreAsync("ViewerJoined", It.IsAny<object[]>(), default), Times.Once);
    }

    // ── LeaveStream ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveStream_RemovesFromStreamGroup_AndBroadcastsViewerLeft()
    {
        var streamId = Guid.NewGuid().ToString();
        var (hub, clients, groups, _) = BuildHub();
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group($"stream:{streamId}")).Returns(proxy.Object);

        // First join so there's something to leave
        await hub.JoinStream(streamId);
        await hub.LeaveStream(streamId);

        groups.Verify(g => g.RemoveFromGroupAsync("live-conn-1", $"stream:{streamId}", default), Times.Once);
        proxy.Verify(p => p.SendCoreAsync("ViewerLeft", It.IsAny<object[]>(), default), Times.AtLeastOnce);
    }

    // ── SendLiveChat ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SendLiveChat_BroadcastsToStreamGroup()
    {
        var streamId = Guid.NewGuid().ToString();
        var (hub, clients, _, _) = BuildHub(userId: "chatter");
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group($"stream:{streamId}")).Returns(proxy.Object);

        await hub.SendLiveChat(streamId, "Hello stream!");

        proxy.Verify(p => p.SendCoreAsync("LiveChatMessage", It.IsAny<object[]>(), default), Times.Once);
    }

    [Fact]
    public async Task SendLiveChat_IgnoresEmptyOrWhitespaceMessage()
    {
        var streamId = Guid.NewGuid().ToString();
        var (hub, clients, _, _) = BuildHub(userId: "chatter");
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group($"stream:{streamId}")).Returns(proxy.Object);

        await hub.SendLiveChat(streamId, "   ");

        proxy.Verify(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default), Times.Never);
    }

    // ── GetViewerCount ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetViewerCount_ReturnsZero_WhenNoViewers()
    {
        var (hub, _, _, _) = BuildHub();
        var streamId = Guid.NewGuid().ToString();

        var count = await hub.GetViewerCount(streamId);

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetViewerCount_ReturnsCorrectCount_AfterJoin()
    {
        var streamId = Guid.NewGuid().ToString();
        var (hub, clients, groups, _) = BuildHub();
        var proxy = new Mock<IClientProxy>();
        clients.Setup(c => c.Group($"stream:{streamId}")).Returns(proxy.Object);

        await hub.JoinStream(streamId);

        var count = await hub.GetViewerCount(streamId);

        count.Should().Be(1);
    }
}
