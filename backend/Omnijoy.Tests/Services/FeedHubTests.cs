using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Omnijoy.Api.Hubs;

namespace Omnijoy.Tests.Services;

/// <summary>Unit tests for <see cref="FeedHub"/>.</summary>
public class FeedHubTests
{
    private static (FeedHub, Mock<IGroupManager>) BuildHub(
        string? userId = "feed-user", string connectionId = "feed-conn-1")
    {
        var hub     = new FeedHub();
        var clients = new Mock<IHubCallerClients>();
        var groups  = new Mock<IGroupManager>();
        var context = new Mock<HubCallerContext>();
        context.Setup(c => c.ConnectionId).Returns(connectionId);
        context.Setup(c => c.UserIdentifier).Returns(userId);

        hub.Clients = clients.Object;
        hub.Groups  = groups.Object;
        hub.Context = context.Object;

        return (hub, groups);
    }

    // ── OnConnectedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task OnConnectedAsync_AddsToUserGroup_WhenAuthenticated()
    {
        var (hub, groups) = BuildHub(userId: "feed-user");

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync("feed-conn-1", "user:feed-user", default), Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_DoesNotAddToGroup_WhenAnonymous()
    {
        var (hub, groups) = BuildHub(userId: null);

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── OnDisconnectedAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task OnDisconnectedAsync_RemovesFromUserGroup_WhenAuthenticated()
    {
        var (hub, groups) = BuildHub(userId: "feed-user");

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync("feed-conn-1", "user:feed-user", default), Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_DoesNotRemove_WhenAnonymous()
    {
        var (hub, groups) = BuildHub(userId: null);

        await hub.OnDisconnectedAsync(null);

        groups.Verify(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    // ── SubscribeToPost ──────────────────────────────────────────────────────

    [Fact]
    public async Task SubscribeToPost_AddsToPostGroup()
    {
        var (hub, groups) = BuildHub();
        var postId = Guid.NewGuid();

        await hub.SubscribeToPost(postId);

        groups.Verify(g => g.AddToGroupAsync("feed-conn-1", $"post:{postId}", default), Times.Once);
    }

    // ── UnsubscribeFromPost ──────────────────────────────────────────────────

    [Fact]
    public async Task UnsubscribeFromPost_RemovesFromPostGroup()
    {
        var (hub, groups) = BuildHub();
        var postId = Guid.NewGuid();

        await hub.UnsubscribeFromPost(postId);

        groups.Verify(g => g.RemoveFromGroupAsync("feed-conn-1", $"post:{postId}", default), Times.Once);
    }
}
