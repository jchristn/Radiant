# SampleApplication

The runnable companion to [`INTEGRATION.md`](../../INTEGRATION.md). A parcel-sorting worker that
instruments its domain code with the .NET base class library — a `Meter` for metrics and an
`ActivitySource` for spans — and a `Program.cs` composition root that hosts it with Radiant.

`ParcelSorter.cs` takes no dependency on Radiant. `Program.cs` is the only place that does: it starts
a `RadiantHost`, subscribes to the `"ParcelSorter.Core"` meter and activity source by name, serves an
in-process Prometheus endpoint, and pushes OTLP to a collector.

## Run it

Continuous, so Prometheus (or the reference Grafana stack) can scrape it — Ctrl+C to stop:

```
dotnet run --project src/SampleApplication
```

Bounded burst that emits, flushes, and exits — useful as a smoke test:

```
dotnet run --project src/SampleApplication -- --iterations 200
```

Scrape the in-process endpoint while it runs:

```
curl http://localhost:9464/metrics
```

To see it in Grafana, bring up the reference stack first:

```
docker compose -f docker/compose.telemetry.yaml up -d
```

Then open http://localhost:3000 and find the Radiant Overview dashboard.
