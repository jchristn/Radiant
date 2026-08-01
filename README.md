# Radiant

**Telemetry for .NET without the wiring.** Fill in one settings object, start one host, and your
application's metrics, traces, and logs flow to Prometheus, Tempo, and Loki — or to any
OpenTelemetry-compatible backend.

> **Status: alpha — v0.1.0.** Radiant is early and under active development. The public API,
> defaults, and package layout may change between 0.x releases without notice. Pin a version if you
> depend on it, and read the [CHANGELOG](CHANGELOG.md) before upgrading.

## What it is

Radiant is a small .NET library that owns the *host* side of telemetry — building the OpenTelemetry
providers, configuring OTLP export, serving an in-process Prometheus endpoint, pushing logs to Loki,
and shipping ready-made Grafana assets. You touch two types, `RadiantSettings` and `RadiantHost`,
once at your application's composition root.

The deliberate decision is what Radiant *doesn't* do: it does not invent a way to emit telemetry.
Emitting a measurement stays on the .NET base class library — `Meter`, `ActivitySource`, `ILogger`.
A library that wants to be observable creates a `Meter` with a stable name and records into it. That
costs nothing and throws nothing until an application decides to listen. Radiant is the listener —
one consumer of the platform's telemetry primitives, not a framework you adopt end to end.

That split is the point. Emit rides the base class library, so any C# library — a web server, a
cache, a driver — can produce telemetry without taking a dependency on Radiant. The application
hosts, once. The two sides never reference each other; they meet at a string name.

## What it does

- **Metrics** — subscribes to your `Meter` instruments, exports them over OTLP, and optionally serves
  a Prometheus `/metrics` endpoint in-process. Ships baseline process and .NET runtime metrics.
- **Traces** — subscribes to your `ActivitySource` spans with parent-based sampling and OTLP export,
  plus a thin `RadiantSpan` helper for timing units of work.
- **Logs** — an `ILogger` pipeline exporting over OTLP, with direct-to-Loki support and
  trace/log correlation so a slow span links to that request's logs.
- **Conventions** — an open `Convention` type and the OpenTelemetry semantic-convention catalog, so
  your instruments render in stock dashboards, plus an opt-in guardrail that enforces bounded metric
  cardinality.

## Why use it

**You get a working pipeline in a few lines instead of a few hundred.** Standing up OpenTelemetry by
hand means learning three provider builders, an exporter matrix, resource attributes, samplers, and a
Prometheus listener. Radiant makes the common path a settings object with sane defaults, while
leaving the raw OpenTelemetry SDK fully accessible underneath when you need it.

**Your libraries stay clean.** Because emit is just the base class library, a foundational component
can be fully instrumented and still impose nothing on the applications that use it. No Radiant
dependency, no configuration, no cost when telemetry is off. The library and the host meet at a name,
not a reference.

**It's vendor-neutral by construction.** The same instruments Radiant reads are readable by
`dotnet-counters`, `dotnet-monitor`, Application Insights, and any OpenTelemetry-compatible vendor —
Grafana Cloud, Datadog, New Relic, Azure Monitor, CloudWatch — often several at once. Follow the
semantic conventions and stock dashboards light up everywhere.

**It won't melt your metrics backend.** The one way a naive telemetry layer fails is an unbounded
`Record(anyString, value)` surface, where metric names and label values multiply into millions of
time series. Radiant rides the base class library's typed instruments and offers an opt-in declared
catalog that *enforces* bounded cardinality rather than merely encouraging it.

## Packages

| Package | What it is | Depends on |
|---|---|---|
| [`Radiant`](https://www.nuget.org/packages/Radiant) | The core: settings, host, convenience emitter, provider wiring | OpenTelemetry SDK + exporters, `Microsoft.Extensions.Logging` |
| [`Radiant.SemConv`](https://www.nuget.org/packages/Radiant.SemConv) | Emit-side naming vocabulary: the `Convention` type + OTel semantic-convention definitions | `System.Diagnostics.DiagnosticSource` only |

The core carries no stack-specific dependencies. A library becomes observable by creating a
`Meter`/`ActivitySource` with a stable name and emitting; Radiant subscribes to that name from the
host side. A component that only needs to name its instruments consistently pulls in `Radiant.SemConv`
and nothing else — not even the OpenTelemetry SDK. An application that wants the full pipeline pulls
in `Radiant`.

Both packages multi-target `netstandard2.0;netstandard2.1;net8.0;net10.0`. The emit path is
`netstandard2.0`-clean, so a down-level library can create a `Meter`, emit, and stay a no-op until a
modern host subscribes.

## Getting started

Install the core package:

```
dotnet add package Radiant
```

Start a host at your composition root. This is the only Radiant code most applications write:

```csharp
using Radiant;

RadiantSettings settings = new RadiantSettings("orders-api");
settings.Otlp.Endpoint = "http://localhost:4317";      // your OpenTelemetry Collector
settings.Prometheus.Enable = true;                      // also serve /metrics in-process
settings.Sources.AddMeter("Orders.Domain");             // subscribe to your library's meter
settings.Sources.AddActivitySource("Orders.Domain");

using (RadiantHost host = RadiantHost.Start(settings))
{
    // ... run your application ...
}
```

That host builds the meter, tracer, and logging providers, subscribes to the sources you named,
binds the Prometheus port, and flushes and releases everything on dispose. Nothing else is required
to get metrics, traces, and logs leaving the process.

### Emitting from library code (no Radiant dependency)

A library emits through the base class library. It never references Radiant:

```csharp
using System.Diagnostics.Metrics;

public sealed class OrderProcessor
{
    private static readonly Meter _Meter = new Meter("Orders.Domain");
    private static readonly Counter<long> _Processed = _Meter.CreateCounter<long>("orders.processed");

    public void Process(Order order)
    {
        // ... work ...
        _Processed.Add(1);
    }
}
```

If no host ever subscribes to `"Orders.Domain"`, that `Add(1)` is inert — no allocation that
matters, no exception, no configuration. When an application does subscribe, the same counter lights
up. That is exactly the behavior a foundational library needs when it may be embedded in an app that
doesn't care about observability.

### Emitting from application code (the convenience emitter)

If you would rather not manage `Meter` fields, the host exposes a `Client` that hands back cached
instrument handles so a call site records against a handle, not a re-typed string:

```csharp
host.Client.Increment("orders.processed", 1, new RadiantTag("region", "eu"));
host.Client.Record("orders.latency", elapsedSeconds, new RadiantTag("region", "eu"));
host.Client.RegisterGauge("orders.queue.depth", () => _queue.Count, "{item}");
```

Spans are a thin `IDisposable` over `ActivitySource`:

```csharp
using (RadiantSpan span = host.StartSpan("process-order", SpanKindEnum.Server))
{
    span.SetTag("order.id", order.Id);
    try { Process(order); span.SetOk(); }
    catch (Exception e) { span.RecordException(e); throw; }
}
```

For the full end-to-end walkthrough — instrumenting a sample app from scratch, testing your
instrumentation, and the performance cost of each instrument — see **[INTEGRATION.md](INTEGRATION.md)**.

## Connecting to a collector or backend

Radiant can push over OTLP, serve Prometheus in-process, and ship logs to Loki — independently or all
at once. Configure them on `RadiantSettings`.

**OTLP push to a Collector.** The default is gRPC on port 4317. For HTTP/protobuf, use port 4318 and
set the protocol:

```csharp
settings.Otlp.Endpoint = "http://collector:4318";
settings.Otlp.Protocol = OtlpProtocolEnum.HttpProtobuf;
settings.Otlp.TimeoutMs = 10000;
settings.Metrics.ExportIntervalMs = 15000;   // how often metrics are pushed
```

**A hosted backend (Grafana Cloud, Honeycomb, etc.).** Point the endpoint at the vendor's OTLP URL
and add an auth header:

```csharp
settings.Otlp.Endpoint = "https://otlp-gateway.example.grafana.net/otlp";
settings.Otlp.Protocol = OtlpProtocolEnum.HttpProtobuf;
settings.Otlp.Headers["Authorization"] = "Basic <base64-instance-and-token>";
```

**In-process Prometheus scrape.** When no Collector is deployed, serve `/metrics` from the app and
point Prometheus straight at it — no collector required:

```csharp
settings.Prometheus.Enable = true;
settings.Prometheus.Port = 9464;             // scrape at http://host:9464/metrics
```

OTLP push and the Prometheus endpoint can both run; the endpoint is a pull path, the OTLP exporter a
push path, and they don't interfere.

**Direct Loki export.** Send logs to a Loki 3.x OTLP endpoint without a collector in the path:

```csharp
settings.Loki.Enable = true;
settings.Loki.Endpoint = "http://loki:3100/otlp";
settings.Loki.TenantId = "team-orders";      // sent as X-Scope-OrgID
```

If your application already owns its `ILoggingBuilder` (a generic host, or one you hand to another
sink), wire Radiant's log export in without going through `RadiantHost`:

```csharp
builder.Logging.AddRadiant(settings);
```

### Try it against a real Grafana

The [`docker/`](docker/) directory holds a reference stack — Prometheus, Tempo, Loki, Grafana, and an
OpenTelemetry Collector wired together, with datasources and an overview dashboard provisioned:

```
docker compose -f docker/compose.telemetry.yaml up -d
```

Then drive telemetry by hand with the console exerciser, or run the sample app:

```
dotnet run --project src/Radiant.Sdk.Console      # interactive: emit metrics/spans/logs, serve /metrics
dotnet run --project src/SampleApplication         # the INTEGRATION.md walkthrough, runnable
```

Open Grafana at `http://localhost:3000` and watch the Radiant Overview dashboard fill in.

## Names and conventions

The price of that cross-vendor usefulness is semantic-convention compliance: correct instrument kind,
UCUM unit, and OTel-standard names and attributes. `Radiant.SemConv` ships those as reusable
`Convention` definitions so the emitting library and the reading host agree by referencing the same
value rather than re-typing strings:

```csharp
host.Client.Record(SemConv.Http.RequestDuration, seconds,
    new RadiantTag(SemConv.Http.AttributeStatusCode, 200));
```

`Convention` is also the open extension point for *your* names. You declare custom instruments with
the same factories, so built-in and custom are the same type flowing through the same emit and
catalog APIs — nothing second-class about the ones you wrote:

```csharp
public static readonly Convention Sorted =
    Convention.Counter("orders.processed", "{order}", "region", "outcome");

host.Client.Increment(Sorted, 1, new RadiantTag("region", "eu"));   // identical to the built-in call
settings.Metrics.DefineAll(Sorted, /* ... */);                       // register a whole set at once
```

A `Convention` converts implicitly to its name, so it also drops straight into a raw
`System.Diagnostics` call — a `netstandard2.0` library can reference `Radiant.SemConv` for the shared
vocabulary without touching the OpenTelemetry SDK.

## Configuration reference

`RadiantSettings` is the whole surface. Every pillar is on by default; numeric fields clamp to safe
ranges rather than throwing.

| Setting | Default | Notes |
|---|---|---|
| `ServiceName` | required | Stamped as `service.name`. Non-empty. |
| `ServiceInstanceId` | auto GUID | Stamped as `service.instance.id`. |
| `Metrics.ExportIntervalMs` | 15000 | OTLP metric reader cadence. Clamps 1000..300000. |
| `Metrics.IncludeRuntime` | true | .NET runtime instrumentation. |
| `Metrics.LabelPolicy` | Auto | Strict in Debug, lenient in Release. |
| `Traces.SamplingRatio` | 1.0 | Parent-based. Clamps 0..1. |
| `Logs.MinimumSeverity` | 1 | 0 (verbose) .. 7 (drop all). |
| `Otlp.Endpoint` | `http://localhost:4317` | Absolute URI. |
| `Otlp.Protocol` | `Grpc` | Or `HttpProtobuf`. Invalid values fail fast. |
| `Prometheus.Enable` | false | In-process scrape endpoint. |
| `Prometheus.Port` | 9464 | Clamps 1..65535. One host per port per process. |
| `Loki.Enable` | false | Direct OTLP-HTTP to Loki 3.x. |

## Documentation

- **[INTEGRATION.md](INTEGRATION.md)** — the full walkthrough: instrument a sample app, connect it to
  Radiant, define conventions, test your instrumentation, and understand the performance cost.
- **[CHANGELOG.md](CHANGELOG.md)** — what changed in each release.
- **[docker/](docker/)** — the reference Prometheus / Tempo / Loki / Grafana / Collector stack, with
  provisioned datasources and dashboard.
- **[src/SampleApplication/](src/SampleApplication/)** — the runnable companion to the walkthrough.

## Building and testing

```
dotnet build src/Radiant.slnx -c Release
dotnet run  --project src/Test.Automated          # console runner, colored output
dotnet test src/Test.Xunit                         # same suites through xUnit
dotnet test src/Test.Nunit                         # same suites through NUnit
```

The suites live once in `Test.Shared` as runner-agnostic descriptors and execute through all three
runners. They assert metric values and labels with in-memory readers, prove the catalog rejects
undeclared labels, and confirm export reaches a stand-in OTLP/Loki endpoint.

## Compatibility notes

The core executes on the app's real runtime (net8/net10) even though it publishes down-level assets.
The OpenTelemetry package versions are pinned to ones that publish `netstandard2.0` assets, and net7+
conveniences are guarded for the down-level targets. The in-process Prometheus endpoint depends on
`OpenTelemetry.Exporter.Prometheus.HttpListener`, which the OpenTelemetry project ships only as a
prerelease — the one prerelease dependency a stable Radiant carries, and a deliberate one.

## Contributing, issues, and discussions

Bug reports and feature requests are welcome while Radiant is finding its shape. Because it's alpha,
opening an issue *before* a large change is the fastest way to avoid rework.

- **File a bug or request a feature:** [open an issue](https://github.com/jchristn/Radiant/issues).
- **Ask a question or float an idea:** [start a discussion](https://github.com/jchristn/Radiant/discussions).
- **Contribute code:** fork, branch, and open a pull request against `main`. Please run the test
  suites (`dotnet test src/Radiant.slnx`) and match the existing code style.

## License

Radiant is released under the MIT License. See [LICENSE.md](LICENSE.md).
