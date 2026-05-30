using FluentAssertions;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="InMemoryPresenceTracker"/> — the single-instance,
/// in-process presence tracker backed by <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public class InMemoryPresenceTrackerTests
{
    // ── ConnectedAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Connected_FirstConnection_ReturnsTrueIndicatingCameOnline()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        var result = await sut.ConnectedAsync(userId, "conn1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Connected_SecondConnection_ReturnsFalseAlreadyOnline()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        var result = await sut.ConnectedAsync(userId, "conn2");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Connected_SameConnectionIdTwice_DoesNotDuplicateEntry()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        await sut.ConnectedAsync(userId, "conn1"); // duplicate

        // After removing the only logical connection the user should be offline.
        var result = await sut.DisconnectedAsync(userId, "conn1");

        result.Should().BeTrue();
    }

    // ── DisconnectedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Disconnected_LastConnection_ReturnsTrueIndicatingWentOffline()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        var result = await sut.DisconnectedAsync(userId, "conn1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Disconnected_RemainingConnections_ReturnsFalseStillOnline()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        await sut.ConnectedAsync(userId, "conn2");
        var result = await sut.DisconnectedAsync(userId, "conn1");

        result.Should().BeFalse();
        (await sut.IsOnlineAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task Disconnected_UnknownUser_ReturnsFalse()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        var result = await sut.DisconnectedAsync(userId, "conn1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Disconnected_UnknownConnectionId_DoesNotRemoveOtherConnections()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        var result = await sut.DisconnectedAsync(userId, "unknown-conn");

        // conn1 is still present → still online.
        result.Should().BeFalse();
        (await sut.IsOnlineAsync(userId)).Should().BeTrue();
    }

    // ── IsOnlineAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task IsOnline_WithActiveConnection_ReturnsTrue()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");

        (await sut.IsOnlineAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsOnline_AfterAllConnectionsDropped_ReturnsFalse()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        await sut.DisconnectedAsync(userId, "conn1");

        (await sut.IsOnlineAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task IsOnline_NeverConnected_ReturnsFalse()
    {
        var sut = new InMemoryPresenceTracker();

        (await sut.IsOnlineAsync(Guid.NewGuid())).Should().BeFalse();
    }

    // ── GetPresenceAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPresence_MixedOnlineOffline_ReturnsCorrectStatuses()
    {
        var sut         = new InMemoryPresenceTracker();
        var onlineUser  = Guid.NewGuid();
        var offlineUser = Guid.NewGuid();

        await sut.ConnectedAsync(onlineUser, "conn-a");
        await sut.ConnectedAsync(offlineUser, "conn-b");
        await sut.DisconnectedAsync(offlineUser, "conn-b");

        var presence = await sut.GetPresenceAsync([onlineUser, offlineUser]);

        presence.Should().HaveCount(2);
        presence.Single(p => p.UserId == onlineUser).IsOnline.Should().BeTrue();
        presence.Single(p => p.UserId == onlineUser).LastSeenAt.Should().BeNull();
        presence.Single(p => p.UserId == offlineUser).IsOnline.Should().BeFalse();
        presence.Single(p => p.UserId == offlineUser).LastSeenAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPresence_DeduplicatesUserIds()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");

        var presence = await sut.GetPresenceAsync([userId, userId, userId]);

        presence.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPresence_UnknownUser_ReturnsOfflineWithNullLastSeen()
    {
        var sut    = new InMemoryPresenceTracker();
        var userId = Guid.NewGuid();

        var presence = await sut.GetPresenceAsync([userId]);

        presence.Should().HaveCount(1);
        presence[0].IsOnline.Should().BeFalse();
        presence[0].LastSeenAt.Should().BeNull();
    }

    [Fact]
    public async Task GetPresence_EmptyInput_ReturnsEmptyArray()
    {
        var sut = new InMemoryPresenceTracker();

        var presence = await sut.GetPresenceAsync([]);

        presence.Should().BeEmpty();
    }

    // ── GetOnlineUsersAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetOnlineUsers_WithConnectedUsers_ReturnsTheirIds()
    {
        var sut  = new InMemoryPresenceTracker();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await sut.ConnectedAsync(user1, "conn1");
        await sut.ConnectedAsync(user2, "conn2");

        var result = await sut.GetOnlineUsersAsync();

        result.Should().Contain(user1);
        result.Should().Contain(user2);
    }

    [Fact]
    public async Task GetOnlineUsers_AfterDisconnect_ExcludesOfflineUser()
    {
        var sut    = new InMemoryPresenceTracker();
        var user1  = Guid.NewGuid();
        var user2  = Guid.NewGuid();

        await sut.ConnectedAsync(user1, "conn1");
        await sut.ConnectedAsync(user2, "conn2");
        await sut.DisconnectedAsync(user1, "conn1");

        var result = await sut.GetOnlineUsersAsync();

        result.Should().NotContain(user1);
        result.Should().Contain(user2);
    }

    [Fact]
    public async Task GetOnlineUsers_NobodyConnected_ReturnsEmptyArray()
    {
        var sut = new InMemoryPresenceTracker();

        var result = await sut.GetOnlineUsersAsync();

        result.Should().BeEmpty();
    }
}
