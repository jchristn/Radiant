# Changelog

All notable changes to Radiant are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.1] - 2026-08-01

### Added

- Project branding: `assets/logo.png` in the README and `assets/logo.ico` as the assembly icon.
- README "Why use it" section rewritten to articulate the concrete advantages (collapsed wiring,
  in-process `/metrics` with no infrastructure, lifecycle correctness, dependency-free libraries,
  dashboard-ready naming with a cardinality guardrail, vendor-neutral, unit-testable), with the
  honest boundary that it lowers wiring cost, not conceptual cost.

## [0.1.0] - 2026-08-01

First alpha. The host and export pipeline, emit conveniences, declared catalog, logs/Loki export,
naming conventions, reference deployment stack, and a full test harness are in place.

### Added

- **`Radiant` core.** `RadiantSettings` and `RadiantHost` — fill one settings object, start one
  host, get metrics/traces/logs wired and exportable. The host builds the OpenTelemetry
  `MeterProvider`, `TracerProvider`, and logging pipeline, subscribes to configured meter and
  activity-source names, binds the optional in-process Prometheus scrape endpoint, and flushes and
  releases everything on `Dispose` / `DisposeAsync`.
- **Subscribe-by-name source model.** `Sources.AddMeter(name)` and `AddActivitySource(name)` so the
  host picks up telemetry from any library without either side referencing the other.
- **OTLP push exporter.** Endpoint, gRPC vs HTTP/protobuf, timeout, and headers; invalid protocol
  fails fast rather than silently falling back.
- **In-process Prometheus scrape endpoint.** Optional, off by default; one host per scrape port per
  process, enforced with a clear error instead of a buried socket failure.
- **Built-in process and runtime metrics.** Working set, uptime, thread count, and optional
  `OpenTelemetry.Instrumentation.Runtime`, toggleable.
- **`RadiantClient` convenience emitter.** Cached BCL instrument handles, record helpers, and live
  gauges (single- and multi-measurement) read from state at collection time.
- **`RadiantSpan`.** A thin `IDisposable` over `ActivitySource` with tags, status, exception
  recording, and parent-based sampling from `Traces.SamplingRatio`.
- **Declared catalog (opt-in).** `Metrics.Define(...)` with a `LabelPolicyEnum` that resolves to
  strict in Debug and lenient in Release, enforced against the consuming application's build.
- **Logs pipeline and direct Loki export.** `ILogger`-based, with OTLP-HTTP export to Loki 3.x and
  trace/log correlation.
- **`RadiantLoggingExtensions.AddRadiant(ILoggingBuilder, RadiantSettings)`.** Wires Radiant's log
  export into a logging builder the application already owns.
- **`Radiant.SemConv`.** Emit-side naming vocabulary depending only on
  `System.Diagnostics.DiagnosticSource`: the `Convention` descriptor type (an open, immutable
  name-plus-kind-plus-unit-plus-labels value with `Counter` / `Histogram` / `UpDownCounter` /
  `Gauge` factories and an implicit conversion to its name) and the built-in semantic-convention
  definitions expressed as ready-made `Convention` instances. Custom instruments are declared with
  the same factories and flow through the same emit and catalog APIs as the built-ins —
  `host.Client.Record(convention, value, tags)` and `settings.Metrics.Define(convention)` /
  `DefineAll(...)`. `MetricKindEnum` moved here so a `netstandard2.0` library can share conventions
  without the OpenTelemetry SDK.
- **Console exerciser** (`Radiant.Sdk.Console`) and a **runner-agnostic test harness**
  (`Test.Shared` descriptors executed by console, xUnit, and NUnit runners) with in-memory metric
  readers and a stand-in recording HTTP endpoint for export assertions.
- **Reference telemetry stack** under `docker/`: OpenTelemetry Collector, Prometheus, Tempo, Loki,
  and Grafana with provisioned datasources and a Radiant Overview dashboard.

### Notes

- Both packages multi-target `netstandard2.0;netstandard2.1;net8.0;net10.0`.
- The core takes no stack-specific dependencies. Libraries emit through `System.Diagnostics`
  (`Meter` / `ActivitySource`) and the host subscribes by name, so there is no coupling between the
  telemetry consumer and producer.
- The in-process Prometheus endpoint depends on the OpenTelemetry Prometheus HTTP listener exporter,
  which upstream ships only as a prerelease; that is the sole prerelease dependency in the core.
- This is an alpha release. The public API, defaults, and package layout may change between 0.x
  versions.
