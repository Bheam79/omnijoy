namespace Omnijoy.Api.Metrics;

/// <summary>
/// Prometheus gauges for SignalR connection tracking.
///
/// Each hub calls <see cref="Increment"/> / <see cref="Decrement"/> in
/// <c>OnConnectedAsync</c> / <c>OnDisconnectedAsync</c> respectively.
/// The resulting metric is exposed as:
///
///   signalr_connections{hub="notification|chat|feed|live"} N
///
/// on the <c>/metrics</c> endpoint (internal network only — nginx restricts
/// external access; see nginx.prod.conf.template).
/// </summary>
public static class SignalRMetrics
{
    // Use fully-qualified Prometheus.Metrics to avoid ambiguity with the
    // Omnijoy.Api.Metrics namespace this class lives in.
    private static readonly Prometheus.Gauge Connections = Prometheus.Metrics.CreateGauge(
        "signalr_connections",
        "Number of active SignalR connections",
        labelNames: ["hub"]);

    /// <summary>Increments the gauge for <paramref name="hubName"/> by 1.</summary>
    public static void Increment(string hubName) => Connections.WithLabels(hubName).Inc();

    /// <summary>Decrements the gauge for <paramref name="hubName"/> by 1.</summary>
    public static void Decrement(string hubName) => Connections.WithLabels(hubName).Dec();
}
