# Radiant

Radiant is a .NET telemetry SDK that turns metrics, traces, and logs into a wired, exportable
pipeline through one object your application owns. The library ships on NuGet as `Radiant`; this
page covers the **reference telemetry stack** it ships alongside — the Prometheus, Tempo, Loki,
Grafana, and OpenTelemetry Collector containers a Radiant host exports into.

## What Radiant is

Emitting telemetry stays on the .NET base class library — `Meter`, `ActivitySource`, `ILogger` — so
any library can produce telemetry without a dependency on Radiant, and stays a no-op until an
application listens. The application does the hosting once: fill a `RadiantSettings`, start a
`RadiantHost`, and metrics flow to Prometheus, traces to Tempo, logs to Loki. Radiant owns the
provider construction, OTLP wiring, in-process Prometheus endpoint, and Loki export so services
don't hand-roll each of those.

## The reference stack

The `docker/compose.telemetry.yaml` bundle brings up the four backends Radiant targets plus a
collector that fans OTLP out to all three:

- **OpenTelemetry Collector** — receives OTLP on `4317` (gRPC) and `4318` (HTTP), exports metrics to
  Prometheus, traces to Tempo, logs to Loki.
- **Prometheus** (`:9090`) — scrapes the collector, or a Radiant host's in-process `/metrics`
  endpoint directly.
- **Tempo** (`:3200`) — trace storage, OTLP ingest.
- **Loki** (`:3100`) — log storage with OTLP ingest.
- **Grafana** (`:3000`) — datasources and a Radiant Overview dashboard provisioned on boot, with
  trace-to-log correlation wired so a slow span links to that request's logs.

## Getting started

```bash
docker compose -f docker/compose.telemetry.yaml up -d
```

Point a Radiant host at the collector:

```csharp
RadiantSettings settings = new RadiantSettings("orders-api");
settings.Otlp.Endpoint = "http://localhost:4317";
using (RadiantHost host = RadiantHost.Start(settings)) { /* run */ }
```

Or drive it by hand with the console exerciser, which emits sample metrics, spans, and logs and
serves `/metrics`:

```bash
dotnet run --project src/Radiant.Sdk.Console
```

Open Grafana at `http://localhost:3000` (anonymous admin is enabled in this reference config) and
watch the Radiant Overview dashboard fill in.

## Architecture at a glance

```
your app / libraries ──emit──> System.Diagnostics (Meter / ActivitySource / ILogger)
                                        │
                                RadiantHost subscribes
                                        │
                     ┌──────────────────┼───────────────────┐
                 OTLP push         in-proc /metrics      OTLP-HTTP
                     │                   │                   │
              OTel Collector        Prometheus            Loki
                 │      │                                    │
              Tempo   Prometheus ──────────► Grafana ◄───────┘
```

## Use cases

- A service with no Collector deployed gets useful metrics on day one via the in-process Prometheus
  endpoint, and upgrades to the Collector later without code changes.
- A library emits through `System.Diagnostics` with a stable meter name and takes no dependency on
  Radiant; the application subscribes to that name and the instruments light up.
- A service routes its `ILogger` output to Loki by calling `AddRadiant` on its logging builder,
  without changing log call sites.

## License

MIT. Source and full documentation: https://github.com/jchristn/Radiant
