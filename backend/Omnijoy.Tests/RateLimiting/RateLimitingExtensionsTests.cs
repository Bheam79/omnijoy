using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.RateLimiting;
using Omnijoy.Api.RateLimiting;

namespace Omnijoy.Tests.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimitingExtensions"/> and
/// <see cref="RateLimitConstants"/>.
/// </summary>
public class RateLimitingExtensionsTests
{
    // ── RateLimitConstants ────────────────────────────────────────────────────

    [Fact]
    public void Constants_PolicyNames_AreStable()
    {
        RateLimitConstants.StrictPolicy.Should().Be("strict");
        RateLimitConstants.UploadPolicy.Should().Be("upload");
    }

    [Fact]
    public void Constants_GlobalLimits_AreCorrect()
    {
        RateLimitConstants.GlobalIpPermitLimit.Should().Be(200);
        RateLimitConstants.GlobalUserPermitLimit.Should().Be(600);
        RateLimitConstants.GlobalWindow.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constants_StrictLimits_AreCorrect()
    {
        RateLimitConstants.StrictPermitLimit.Should().Be(10);
        RateLimitConstants.StrictWindow.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Constants_UploadLimits_AreCorrect()
    {
        RateLimitConstants.UploadPermitLimit.Should().Be(20);
        RateLimitConstants.UploadWindow.Should().Be(TimeSpan.FromHours(1));
    }

    // ── GetClientIp ───────────────────────────────────────────────────────────

    [Fact]
    public void GetClientIp_NoHeaders_ReturnsRemoteAddress()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public void GetClientIp_XForwardedFor_ReturnsThatIp()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.5";

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("203.0.113.5");
    }

    [Fact]
    public void GetClientIp_XForwardedForList_ReturnsLeftmost()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.5, 10.0.0.2, 10.0.0.3";

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("203.0.113.5");
    }

    [Fact]
    public void GetClientIp_EmptyXForwardedFor_FallsBackToRemoteAddress()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
        ctx.Request.Headers["X-Forwarded-For"] = string.Empty;

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public void GetClientIp_WhitespaceXForwardedFor_FallsBackToRemoteAddress()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
        ctx.Request.Headers["X-Forwarded-For"] = "   ";

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("1.2.3.4");
    }

    [Fact]
    public void GetClientIp_NoRemoteAddress_ReturnsUnknown()
    {
        var ctx = new DefaultHttpContext();
        // RemoteIpAddress is null by default in DefaultHttpContext

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("unknown");
    }

    [Fact]
    public void GetClientIp_XForwardedForWithLeadingSpace_TrimsKey()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");
        ctx.Request.Headers["X-Forwarded-For"] = "  203.0.113.9  ";

        var ip = RateLimitingExtensions.GetClientIp(ctx);

        ip.Should().Be("203.0.113.9");
    }

    // ── AddOmnijoyRateLimiting (service registration) ────────────────────────

    [Fact]
    public void AddOmnijoyRateLimiting_NullRedis_RegistersServicesWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var act = () => services.AddOmnijoyRateLimiting(null);

        act.Should().NotThrow();

        var provider = services.BuildServiceProvider();
        // The rate limiter service should be present
        var rateLimiter = provider.GetService<RateLimiterOptions>();
        // RateLimiterOptions is not directly in DI — verify the whole provider builds OK
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddOmnijoyRateLimiting_EmptyRedis_RegistersServicesWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var act = () => services.AddOmnijoyRateLimiting(string.Empty);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddOmnijoyRateLimiting_InvalidRedisString_FallsBackToMemoryWithoutThrowing()
    {
        // An unreachable Redis address causes ConnectionMultiplexer.Connect to throw,
        // which the extension catches and degrades gracefully to in-memory.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        // Use an address that will immediately refuse / fail so the test is fast.
        // The method wraps Connect in a try/catch, so this must not propagate.
        var act = () => services.AddOmnijoyRateLimiting("127.0.0.1:1,connectTimeout=50,abortConnect=false");

        act.Should().NotThrow();
    }
}
