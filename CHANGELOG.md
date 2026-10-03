# Changelog

All notable changes to Radiant are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.3] - 2026-10-03

### Changed

- **Dependency updates.** `OpenTelemetry`, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.17.0 →
  1.19.1; `OpenTelemetry.Exporter.Prometheus.HttpListener` 1.17.0-beta.1 → 1.19.1-beta.1;
  `OpenTelemetry.Instrumentation.Runtime` 1.17.0 → 1.19.0; `Microsoft.Extensions.Logging` and
  `Microsoft.Extensions.Logging.Abstractions` 10.0.0 → 10.0.12. No public API changes.
- **Test dependency updates.** `OpenTelemetry.Exporter.InMemory` 1.19.1; `Touchstone.*` 0.2.0;
  `Microsoft.NET.Test.Sdk` 18.10.1; `coverlet.collector` 10.1.0; `xunit.runner.visualstudio` 4.0.0;
  `NUnit` 5.0.0; `NUnit.Analyzers` 4.15.0; `NUnit3TestAdapter` 6.3.0.

### Added

- Test `Export/PrometheusScrapeServesMetrics`: starts the in-process Prometheus scrape endpoint on a
  free port, emits a counter, and asserts the scrape output contains it, covering the updated
  prerelease Prometheus exporter end to end.

### Fixed

- **Documentation: `Prometheus.Hostname` does not accept wildcards.** The XML docs said `+` or `*`
  binds every interface. In fact `*` and `+` make `RadiantHost.Start` throw (`UriFormatException`),
  and so do `0.0.0.0` and `[::]` (`HttpListenerException`). This was verified on macOS and Linux.
  The docs now say this, and explain that a hostname binds the address it resolves to, answers only
  requests addressed to that name, and that Compose deployments should bind and scrape by service
  name. The README settings table gains a `Prometheus.Hostname` row, and the commented direct-scrape
  job in `docker/prometheus.yaml` no longer suggests `host.docker.internal`, which the default
  hostname cannot serve.

## [0.1.2] - 2026-08-08

### Changed

- **`Radiant.SemConv` folded into `Radiant`.** The emit-side naming vocabulary — the `Convention`
  descriptor type, the `MetricKindEnum`, and the built-in `SemConv` semantic-convention definitions —
  now ships inside the `Radiant` package instead of as a separate NuGet package. The types are
  unchanged and remain in the `Radiant` namespace, so existing `using Radiant;` code and every call
  (`SemConv.Http.RequestDuration`, `Convention.Counter(...)`, `settings.Metrics.Define(...)`) compiles
  as before; only the extra package reference goes away. `System.Diagnostics.DiagnosticSource` is now a
  direct dependency of `Radiant`.

### Removed

- The standalone `Radiant.SemConv` NuGet package. Consumers reference `Radiant` for the same types.
  Note the trade-off: a `netstandard2.0` library can no longer take the naming vocabulary alone
  without also pulling in the OpenTelemetry SDK that `Radiant` carries.

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
