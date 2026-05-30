using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Hubs;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="ChatHub"/>.</summary>
public class ChatHubTests
{
    private static (ChatHub, Mock<IHubCallerClients>, Mock<IGroupManager>) BuildHub(
        string? userId = "user-123", string connectionId = "conn-1")
    {
        var hub = new ChatHub();

        var clients     = new Mock<IHubCallerClients>();
        var others      = new Mock<IClientProxy>();
        var othersGroup = new Mock<IClientProxy>();

        clients.Setup(c => c.Others).Returns(others.Object);
        clients.Setup(c => c.OthersInGroup(It.IsAny<string>())).Returns(othersGroup.Object);

        var groups  = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.UserIdentifier).Returns(userId);

        hub.Clients = clients.Object;
        hub.Groups  = groups.Object;
        hub.Context = context.Object;

        return (hub, clients, groups);
    }

    // ── OnConnectedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task OnConnectedAsync_AddsToUserGroup_WhenAuthenticated()
    {
        var (hub, _, groups) = BuildHub(userId: "user-abc");

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync("conn-1", "user:user-abc", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_BroadcastsUserOnline_WhenAuthenticated()
    {
        var (hub, clients, _) = BuildHub(userId: "user-abc");

        await hub.OnConnectedAsync();

        clients.Verify(c => c.Others, Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnConnectedAsync_DoesNotAddToGroup_WhenAnonymous()
    {
        var (hub, _, groups) = BuildHub(userId: null);

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── OnDisconnectedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_RemovesFromUserGroup_WhenAuthenticated()
    {
        var (hub, _, groups) = BuildHub(userId: "user-abc");

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync("conn-1", "user:user-abc", default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_BroadcastsUserOffline_WhenAuthenticated()
    {
        var (hub, clients, _) = BuildHub(userId: "user-abc");

        await hub.OnDisconnectedAsync(null);

        clients.Verify(c => c.Others, Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNotRemove_WhenAnonymous()
    {
        var (hub, _, groups) = BuildHub(userId: null);

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── JoinConversation ─────────────────────────────────────────────────────

    [Fact]
    public async Task JoinConversation_AddsToConversationGroup()
    {
        var (hub, _, groups) = BuildHub();
        var convId = Guid.NewGuid().ToString();

        await hub.JoinConversation(convId);

        groups.Verify(g => g.AddToGroupAsync("conn-1", $"conversation:{convId}", default), Times.Once);
    }

    // ── LeaveConversation ────────────────────────────────────────────────────

    [Fact]
    public async Task LeaveConversation_RemovesFromConversationGroup()
    {
        var (hub, _, groups) = BuildHub();
        var convId = Guid.NewGuid().ToString();

        await hub.LeaveConversation(convId);

        groups.Verify(g => g.RemoveFromGroupAsync("conn-1", $"conversation:{convId}", default), Times.Once);
    }

    // ── SendTyping ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendTyping_BroadcastsToOthersInGroup()
    {
        var (hub, clients, _) = BuildHub(userId: "user-abc");
        var convId            = Guid.NewGuid().ToString();
        var proxy             = new Mock<IClientProxy>();
        clients.Setup(c => c.OthersInGroup($"conversation:{convId}")).Returns(proxy.Object);

        await hub.SendTyping(convId);

        proxy.Verify(p => p.SendCoreAsync(
            "UserTyping",
            It.Is<object[]>(a => (string)a[0] == convId),
            default), Times.Once);
    }
}
