using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
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

    // ── BlacklistAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task Blacklist_ThenCheck_ReturnsTrue()
    {
        var sut = new RedisTokenBlacklist(CreateCache());
        var jti = Guid.NewGuid().ToString();

        await sut.BlacklistAsync(jti, TimeSpan.FromMinutes(5));

        (await sut.IsBlacklistedAsync(jti)).Should().BeTrue();
    }

    [Fact]
    public async Task Check_UnknownJti_ReturnsFalse()
    {
        var sut = new RedisTokenBlacklist(CreateCache());

        (await sut.IsBlacklistedAsync(Guid.NewGuid().ToString())).Should().BeFalse();
    }

    [Fact]
    public async Task Blacklist_MultipleJtis_TracksEachIndependently()
    {
        var sut = new RedisTokenBlacklist(CreateCache());
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
        var sut = new RedisTokenBlacklist(CreateCache());
        var jti = Guid.NewGuid().ToString();

        await sut.Invoking(s => s.BlacklistAsync(jti, TimeSpan.Zero))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Blacklist_SameJtiTwice_DoesNotThrow()
    {
        var sut = new RedisTokenBlacklist(CreateCache());
        var jti = Guid.NewGuid().ToString();

        await sut.BlacklistAsync(jti, TimeSpan.FromMinutes(5));
        await sut.Invoking(s => s.BlacklistAsync(jti, TimeSpan.FromMinutes(3)))
            .Should().NotThrowAsync();

        (await sut.IsBlacklistedAsync(jti)).Should().BeTrue();
    }
}
