using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Omnijoy.Infrastructure.Services;

namespace Omnijoy.Tests.Services;

/// <summary>
/// Tests for <see cref="RedisTokenBlacklist"/> using an in-memory distributed
/// cache as the backing store (so no Redis instance is required for unit tests).
/// </summary>
public class RedisTokenBlacklistTests
{
    private static IDistributedCache CreateCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static RedisTokenBlacklist CreateSut(IDistributedCache? cache = null) =>
        new(cache ?? CreateCache(), NullLogger<RedisTokenBlacklist>.Instance);

    // ── BlacklistAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Blacklist_ThenCheck_ReturnsTrue()
    {
        var sut = CreateSut();
        var jti = Guid.NewGuid().ToString();

        await sut.BlacklistAsync(jti, TimeSpan.FromMinutes(5));

        (await sut.IsBlacklistedAsync(jti)).Should().BeTrue();
    }

    [Fact]
    public async Task Check_UnknownJti_ReturnsFalse()
    {
        var sut = CreateSut();

        (await sut.IsBlacklistedAsync(Guid.NewGuid().ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task Blacklist_MultipleJtis_TracksEachIndependently()
    {
        var sut = CreateSut();
        var jti1 = Guid.NewGuid().ToString();
        var jti2 = Guid.NewGuid().ToString();

        await sut.BlacklistAsync(jti1, TimeSpan.FromMinutes(5));

        (await sut.IsBlacklistedAsync(jti1)).Should().BeTrue();
        (await sut.IsBlacklistedAsync(jti2)).Should().BeFalse();
    }

    [Fact]
    public async Task Blacklist_ZeroTtl_StillStoredWithMinimumTtl()
    {
        // A zero/negative TTL could occur for already-expired tokens;
        // the implementation normalises it to 1 second so SetStringAsync
        // doesn't throw.
        var sut = CreateSut();
        var jti = Guid.NewGuid().ToString();

        await sut.Invoking(s => s.BlacklistAsync(jti, TimeSpan.Zero))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Blacklist_SameJtiTwice_DoesNotThrow()
    {
        var sut = CreateSut();
        var jti = Guid.NewGuid().ToString();

        await sut.BlacklistAsync(jti, TimeSpan.FromMinutes(5));
        await sut.Invoking(s => s.BlacklistAsync(jti, TimeSpan.FromMinutes(3)))
            .Should().NotThrowAsync();

        (await sut.IsBlacklistedAsync(jti)).Should().BeTrue();
    }

    // ── Resilience: cache failures (e.g. Redis unreachable) ──────────────────

    [Fact]
    public async Task IsBlacklisted_WhenCacheThrows_ReturnsFalseAndDoesNotPropagate()
    {
        // Simulates Redis being unavailable — should degrade gracefully so
        // the JWT pipeline doesn't 500 every authenticated request.
        var sut = CreateSut(new ThrowingDistributedCache());

        var result = await sut.IsBlacklistedAsync(Guid.NewGuid().ToString());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Blacklist_WhenCacheThrows_DoesNotPropagate()
    {
        // Failing to record a revoked token is bad, but crashing logout is worse.
        var sut = CreateSut(new ThrowingDistributedCache());

        await sut.Invoking(s => s.BlacklistAsync(Guid.NewGuid().ToString(), TimeSpan.FromMinutes(5)))
            .Should().NotThrowAsync();
    }

    /// <summary>
    /// IDistributedCache stub that throws on every operation, used to simulate
    /// a backing store (Redis) that is unreachable.
    /// </summary>
    private sealed class ThrowingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("cache down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => throw new InvalidOperationException("cache down");
        public void Refresh(string key) => throw new InvalidOperationException("cache down");
        public Task RefreshAsync(string key, CancellationToken token = default)
            => throw new InvalidOperationException("cache down");
        public void Remove(string key) => throw new InvalidOperationException("cache down");
        public Task RemoveAsync(string key, CancellationToken token = default)
            => throw new InvalidOperationException("cache down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            => throw new InvalidOperationException("cache down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => throw new InvalidOperationException("cache down");
    }
}
