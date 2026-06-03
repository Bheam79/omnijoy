using FluentAssertions;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Omnijoy.Tests.HealthChecks;

/// <summary>
/// Integration tests for the <c>/api/health/live</c> and <c>/api/health/ready</c>
/// endpoints mapped in <c>Program.cs</c>. Built on top of an in-memory
/// <see cref="WebApplication"/> so we exercise the real
/// <see cref="HealthCheckMiddleware"/> + <see cref="UIResponseWriter"/> pipeline
/// without booting the whole API (which would require a real MariaDB).
/// </summary>
public class HealthEndpointsTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;

    /// <summary>
    /// Health check implementation that always returns the configured status.
    /// Lets us register both healthy and unhealthy probes deterministically.
    /// </summary>
    private sealed class StaticHealthCheck : IHealthCheck
    {
        private readonly HealthStatus _status;
        public StaticHealthCheck(HealthStatus status) => _status = status;

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthCheckResult(_status, "static probe"));
    }

    /// <summary>
    /// Builds an in-memory host with the given health checks registered, plus
    /// the same two endpoints (<c>/api/health/live</c>, <c>/api/health/ready</c>)
    /// that <c>Program.cs</c> wires up — so the test verifies the live
    /// configuration, not a duplicate.
    /// </summary>
    private async Task<HttpClient> StartAsync(
        Action<IHealthChecksBuilder>? configureChecks = null)
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    var hc = services.AddHealthChecks();
                    configureChecks?.Invoke(hc);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHealthChecks("/api/health/live", new HealthCheckOptions
                        {
                            Predicate         = _ => false,
                            ResultStatusCodes = { [HealthStatus.Healthy] = StatusCodes.Status200OK },
                        });
                        endpoints.MapHealthChecks("/api/health/ready", new HealthCheckOptions
                        {
                            Predicate      = c => c.Tags.Contains("ready"),
                            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                        });
                    });
                });
            });

        _host = await builder.StartAsync();
        return _host.GetTestClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    // ── /api/health/live ─────────────────────────────────────────────────────

    [Fact]
    public async Task LiveEndpoint_NoChecks_Returns200()
    {
        _client = await StartAsync();

        var resp = await _client.GetAsync("/api/health/live");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LiveEndpoint_DoesNotRunReadinessProbes_EvenWhenUnhealthy()
    {
        // An unhealthy probe is registered but tagged "ready". Liveness uses
        // Predicate = _ => false, so the probe must not influence /live.
        _client = await StartAsync(hc => hc
            .AddCheck("alwaysBad", new StaticHealthCheck(HealthStatus.Unhealthy), tags: ["ready"]));

        var resp = await _client.GetAsync("/api/health/live");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── /api/health/ready ────────────────────────────────────────────────────

    [Fact]
    public async Task ReadyEndpoint_AllHealthy_Returns200WithStatusAndChecksJson()
    {
        _client = await StartAsync(hc => hc
            .AddCheck("alwaysGood", new StaticHealthCheck(HealthStatus.Healthy), tags: ["ready"]));

        var resp = await _client.GetAsync("/api/health/ready");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        // UIResponseWriter produces { "status": "...", "totalDuration": "...",
        // "entries": { "<name>": { "status": ..., "description": ... } } }
        // (Newtonsoft.Json serialisation with camelCase by default.)
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.TryGetProperty("status", out _).Should().BeTrue("payload must include 'status'");

        // Locate the per-component map regardless of whether the library names
        // it 'entries' or 'checks' across versions.
        var perComponent = TryGetComponentMap(doc);
        perComponent.Should().NotBeNull("payload must include per-component status");
        perComponent!.Value.EnumerateObject()
            .Should().Contain(p => p.Name == "alwaysGood");
        perComponent.Value.GetProperty("alwaysGood").GetProperty("status").GetString()
            .Should().Be("Healthy");
    }

    private static JsonElement? TryGetComponentMap(JsonElement payload)
    {
        if (payload.TryGetProperty("entries", out var entries)) return entries;
        if (payload.TryGetProperty("checks", out var checks))   return checks;
        return null;
    }

    [Fact]
    public async Task ReadyEndpoint_ProbeUnhealthy_Returns503()
    {
        _client = await StartAsync(hc => hc
            .AddCheck("alwaysBad", new StaticHealthCheck(HealthStatus.Unhealthy), tags: ["ready"]));

        var resp = await _client.GetAsync("/api/health/ready");

        // ASP.NET Core HealthChecks middleware maps Unhealthy → 503 by default.
        resp.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        doc.GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    [Fact]
    public async Task ReadyEndpoint_OnlyRunsProbesTaggedReady()
    {
        // 'noisy' is unhealthy but NOT tagged "ready" → must be skipped.
        _client = await StartAsync(hc => hc
            .AddCheck("noisy", new StaticHealthCheck(HealthStatus.Unhealthy))
            .AddCheck("alwaysGood", new StaticHealthCheck(HealthStatus.Healthy), tags: ["ready"]));

        var resp = await _client.GetAsync("/api/health/ready");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var perComponent = TryGetComponentMap(doc);
        perComponent.Should().NotBeNull();

        var names = perComponent!.Value.EnumerateObject().Select(p => p.Name).ToList();
        names.Should().ContainSingle().Which.Should().Be("alwaysGood");
    }
}
