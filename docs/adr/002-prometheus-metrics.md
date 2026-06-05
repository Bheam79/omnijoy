# ADR 002 — Prometheus metrics library: prometheus-net.AspNetCore

**Status:** Accepted  
**Date:** 2026-06-05

## Context

We need to expose application metrics in the Prometheus text format so that
an ops team can scrape and alert on request throughput, latency, error rates,
.NET runtime health (GC, thread pool), and per-hub SignalR connection counts.

Two viable libraries exist for exporting Prometheus metrics from an
ASP.NET Core application:

| | **prometheus-net.AspNetCore** | **OpenTelemetry.Exporter.Prometheus.AspNetCore** |
|---|---|---|
| Maturity | 10+ years, battle-tested | GA since OTel .NET 1.6 (2023) |
| Config overhead | Near-zero — `AddHttpMetrics()` + `UseHttpMetrics()` | Requires full OTel `MeterProvider` pipeline |
| Custom metrics | `Metrics.CreateGauge/Counter/Histogram` — direct API | `System.Diagnostics.Metrics.Meter` + `Instrument` wrappers |
| Semantic conventions | Prometheus-native | OTel semantic conventions → mapped to Prometheus at export |
| Extra dependencies | 1 package | 4–6 packages (OTel SDK, host, exporters, …) |
| Distributed tracing | Not included | Full Jaeger/Tempo/OTLP pipeline available | 

## Decision

Use **prometheus-net.AspNetCore**.

## Rationale

1. **Single-exporter scenario.** We have one metrics consumer (Prometheus).
   The full OpenTelemetry pipeline (OTLP exporter, semantic-convention
   translation, resource detectors) adds abstraction weight that yields no
   operational benefit in this scenario.

2. **Near-zero configuration.** Two lines in `Program.cs`
   (`AddHttpMetrics` + `UseHttpMetrics`) + `MapMetrics("/metrics")` is the
   entire integration. The equivalent OTel setup requires configuring a
   `MeterProvider`, adding AspNetCore instrumentation, and wiring an exporter.

3. **Direct custom-metric API.** `Metrics.CreateGauge("signalr_connections", …)`
   is idiomatic and readable. The OTel equivalent requires creating a
   `Meter`, then an `ObservableGauge` or `UpDownCounter`, which is
   unnecessarily verbose for our use case.

4. **Proven runtime metrics out of the box.** prometheus-net ships default
   collectors for GC, thread pool, process memory, and .NET runtime events
   with no additional configuration.

## Consequences

- A future distributed-tracing requirement (Jaeger, Tempo, OTLP) may prompt
  revisiting this decision — OTel's breadth is a genuine advantage once traces
  are in scope. The migration path from prometheus-net to OTel is
  straightforward (the metric _names_ can remain the same; only the producer
  API changes).
- No vendor lock-in: prometheus-net exposes the standard Prometheus text
  format, consumable by any Prometheus-compatible backend.
