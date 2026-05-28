using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="RedisPresenceTracker"/> using an in-memory distributed
/// cache as the backing store (so no Redis instance is required for unit tests).
/// </summary>
public class RedisPresenceTrackerTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    // ── ConnectedAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Connected_FirstConnection_ReturnsTrueIndicatingCameOnline()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        var result = await sut.ConnectedAsync(userId, "conn1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Connected_SecondConnection_ReturnsFalseAlreadyOnline()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        var result = await sut.ConnectedAsync(userId, "conn2");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Connected_SameConnectionIdTwice_DoesNotDuplicateEntry()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        await sut.ConnectedAsync(userId, "conn1");

        var result = await sut.DisconnectedAsync(userId, "conn1");

        // After removing the only connection the user should be offline.
        result.Should().BeTrue();
    }

    // ── DisconnectedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Disconnected_LastConnection_ReturnsTrueIndicatingWentOffline()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        var result = await sut.DisconnectedAsync(userId, "conn1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Disconnected_RemainingConnections_ReturnsFalseStillOnline()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        await sut.ConnectedAsync(userId, "conn2");
        var result = await sut.DisconnectedAsync(userId, "conn1");

        result.Should().BeFalse();
        (await sut.IsOnlineAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task Disconnected_UnknownConnectionId_DoesNotThrow()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        var result = await sut.DisconnectedAsync(userId, "unknown-conn");

        // Still has conn1 — should not go offline.
        result.Should().BeFalse();
    }

    // ── IsOnlineAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task IsOnline_WithActiveConnection_ReturnsTrue()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");

        (await sut.IsOnlineAsync(userId)).Should().BeTrue();
    }

    [Fact]
    public async Task IsOnline_AfterAllConnectionsDropped_ReturnsFalse()
    {
        var sut = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");
        await sut.DisconnectedAsync(userId, "conn1");

        (await sut.IsOnlineAsync(userId)).Should().BeFalse();
    }

    [Fact]
    public async Task IsOnline_NeverConnected_ReturnsFalse()
    {
        var sut = new RedisPresenceTracker(CreateCache());

        (await sut.IsOnlineAsync(Guid.NewGuid())).Should().BeFalse();
    }

    // ── GetPresenceAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPresence_MixedOnlineOffline_ReturnsCorrectStatuses()
    {
        var sut = new RedisPresenceTracker(CreateCache());
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
        var sut    = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        await sut.ConnectedAsync(userId, "conn1");

        var presence = await sut.GetPresenceAsync([userId, userId, userId]);

        presence.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPresence_UnknownUser_ReturnsOfflineWithNullLastSeen()
    {
        var sut    = new RedisPresenceTracker(CreateCache());
        var userId = Guid.NewGuid();

        var presence = await sut.GetPresenceAsync([userId]);

        presence.Should().HaveCount(1);
        presence[0].IsOnline.Should().BeFalse();
    }

    // ── GetOnlineUsersAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetOnlineUsers_AlwaysReturnsEmptyArray()
    {
        // Key enumeration is not supported via IDistributedCache.
        var sut = new RedisPresenceTracker(CreateCache());
        await sut.ConnectedAsync(Guid.NewGuid(), "conn1");

        var result = await sut.GetOnlineUsersAsync();

        result.Should().BeEmpty();
    }
}
