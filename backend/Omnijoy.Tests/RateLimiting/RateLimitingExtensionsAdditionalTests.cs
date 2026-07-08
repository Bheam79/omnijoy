using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Omnijoy.Api.RateLimiting;

namespace Omnijoy.Tests.RateLimiting;

/// <summary>
/// Additional tests for <see cref="RateLimitingExtensions"/> covering
/// the WindowMinutes override path and combined configuration scenarios.
/// </summary>
public class RateLimitingExtensionsAdditionalTests
{
    // ── ResolveLimits — WindowMinutes fallback ────────────────────────────────

    [Fact]
    public void ResolveLimits_WindowMinutes_WhenNoWindowSeconds()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Upload:WindowMinutes"] = "60",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.UploadWindow.Should().Be(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public void ResolveLimits_GlobalUserPermitLimit_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Global:UserPermitLimit"] = "6000",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.GlobalUserPermitLimit.Should().Be(6000);
    }

    [Fact]
    public void ResolveLimits_GlobalIpPermitLimit_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Global:IpPermitLimit"] = "2000",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.GlobalIpPermitLimit.Should().Be(2000);
    }

    [Fact]
    public void ResolveLimits_GlobalWindowMinutes_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Global:WindowSeconds"] = "30",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.GlobalWindow.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ResolveLimits_StrictWindowSeconds_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Strict:WindowSeconds"] = "90",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.StrictWindow.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void ResolveLimits_StrictPermitLimit_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Strict:PermitLimit"] = "200",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.StrictPermitLimit.Should().Be(200);
    }

    // ── AddOmnijoyRateLimiting — with configuration ───────────────────────────

    [Fact]
    public void AddOmnijoyRateLimiting_WithConfiguration_SetsUpWithoutThrowing()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Upload:PermitLimit"]   = "1000",
                ["RateLimiting:Strict:PermitLimit"]   = "200",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var act = () => services.AddOmnijoyRateLimiting(null, cfg);

        act.Should().NotThrow();
    }

    [Fact]
    public void AddOmnijoyRateLimiting_WithWhitespaceRedis_TreatsAsNull()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        // Whitespace is treated as null (no Redis connection attempt)
        var act = () => services.AddOmnijoyRateLimiting("   ");

        act.Should().NotThrow();
    }

    // ── InteractionPolicy constants ───────────────────────────────────────────

    [Fact]
    public void Constants_InteractionPolicy_NameIsInteraction()
    {
        RateLimitConstants.InteractionPolicy.Should().Be("interaction");
    }

    [Fact]
    public void Constants_InteractionLimits_AreCorrect()
    {
        RateLimitConstants.InteractionPermitLimit.Should().Be(60);
        RateLimitConstants.InteractionWindow.Should().Be(TimeSpan.FromMinutes(1));
    }

    // ── ResolveLimits — Interaction overrides ─────────────────────────────────

    [Fact]
    public void ResolveLimits_NullConfiguration_UsesInteractionConstants()
    {
        var limits = RateLimitingExtensions.ResolveLimits(null);

        limits.InteractionPermitLimit.Should().Be(RateLimitConstants.InteractionPermitLimit);
        limits.InteractionWindow.Should().Be(RateLimitConstants.InteractionWindow);
    }

    [Fact]
    public void ResolveLimits_InteractionPermitLimit_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Interaction:PermitLimit"] = "1000",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.InteractionPermitLimit.Should().Be(1000);
    }

    [Fact]
    public void ResolveLimits_InteractionWindowSeconds_Override()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Interaction:WindowSeconds"] = "120",
            })
            .Build();

        var limits = RateLimitingExtensions.ResolveLimits(cfg);

        limits.InteractionWindow.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void AddOmnijoyRateLimiting_WithInteractionOverride_SetsUpWithoutThrowing()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Interaction:PermitLimit"]   = "1000",
                ["RateLimiting:Interaction:WindowSeconds"] = "60",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();

        var act = () => services.AddOmnijoyRateLimiting(null, cfg);

        act.Should().NotThrow();
    }
}
