# Integrating Radiant

This is a hands-on walkthrough. By the end you will have a small working app that emits metrics,
traces, and logs, and a Radiant host that collects them and ships them to Prometheus, Tempo, and
Loki. We build one app the whole way through — a parcel sorter — so each step adds to something real
rather than a disconnected snippet.

The mental model to hold onto: **your code emits, the application hosts.** Emitting a measurement is
a base-class-library operation — you create a `Meter` or an `ActivitySource` and record into it. That
costs nothing and throws nothing when no one is listening. Radiant is the listener. It never reaches
into your emitting code; it subscribes to it by name. Keep those two jobs separate and everything
else falls into place.

## What you'll build

A `ParcelSorter` that "sorts" parcels by region. It is deliberately plain — a class and a loop — so
the telemetry is the interesting part. We instrument it with four metrics and a span, then stand up a
Radiant host in `Program.cs`, then look at the data in Grafana.

You need the .NET 8 or .NET 10 SDK. Create the project:

```
dotnet new console -n ParcelSorter
cd ParcelSorter
```

## Part 1 — Instrument your code with the BCL

Start with no Radiant dependency at all. The only thing your domain code touches is
`System.Diagnostics.Metrics`, which ships in the platform.

The unit of instrumentation is a `Meter`. Give it a **stable, namespaced name** — this string is the
contract between your code and any host that wants to observe it, so treat it like a public API. From
the meter you create instruments. Pick the instrument kind by what the number actually is:

- **Counter** — a value that only goes up (parcels sorted). You add deltas; the backend sums them.
- **Histogram** — a distribution you want percentiles over (sort duration).
- **UpDownCounter** — a value that rises and falls by deltas (parcels in flight right now).
- **ObservableGauge** — a value you *sample* rather than increment (current queue depth), read
  through a callback at collection time.

Here is `ParcelSorter.cs`:

```csharp
namespace ParcelSorter
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;

    public sealed class ParcelSorter
    {
        // The meter name is the contract. Keep it stable across releases.
        private static readonly Meter _Meter = new Meter("ParcelSorter.Core");

        private static readonly Counter<long> _Sorted =
            _Meter.CreateCounter<long>("parcels.sorted", unit: "{parcel}", description: "Parcels sorted.");

        private static readonly Histogram<double> _SortDuration =
            _Meter.CreateHistogram<double>("parcels.sort.duration", unit: "s", description: "Time to sort one parcel.");

        private static readonly UpDownCounter<long> _InFlight =
            _Meter.CreateUpDownCounter<long>("parcels.inflight", unit: "{parcel}", description: "Parcels being sorted right now.");

        private int _QueueDepth;

        public ParcelSorter()
        {
            // A gauge is read from state when metrics are collected, not when you change the value.
            _Meter.CreateObservableGauge<int>(
                "parcels.queue.depth",
                () => Volatile.Read(ref _QueueDepth),
                unit: "{parcel}",
                description: "Parcels waiting to be sorted.");
        }

        public void Enqueue(int count)
        {
            Interlocked.Add(ref _QueueDepth, count);
        }

        public void Sort(string region)
        {
            if (String.IsNullOrWhiteSpace(region)) throw new ArgumentNullException(nameof(region));

            _InFlight.Add(1);
            long startTicks = Stopwatch.GetTimestamp();
            try
            {
                DoWork();

                double seconds = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;

                // Tag with LOW-cardinality dimensions only. "region" has a handful of values; a
                // parcel id would not — see the cardinality note near the end.
                TagList tags = new TagList
                {
                    { "region", region },
                    { "outcome", "ok" }
                };
                _Sorted.Add(1, tags);
                _SortDuration.Record(seconds, tags);
            }
            finally
            {
                Interlocked.Add(ref _QueueDepth, -1);
                _InFlight.Add(-1);
            }
        }

        private static void DoWork()
        {
            Thread.Sleep(Random.Shared.Next(2, 40));
        }
    }
}
```

Two things worth calling out. `TagList` is a stack-allocated struct — using it for the hot-path tags
keeps `Sort` from allocating a `KeyValuePair` array on every call. And the whole class works today,
with or without Radiant: if nothing subscribes to `"ParcelSorter.Core"`, every `Add` and `Record` is
a cheap no-op. That is the property that lets you embed this in a library without imposing anything on
the apps that consume it.

### Naming, so the free dashboards work

The instrument names above are dotted and use UCUM units (`s`, `{parcel}`). That is not decoration.
Standard dashboards and vendors key off the OpenTelemetry semantic conventions, so a correctly named
`http.server.request.duration` renders in a stock Grafana panel with no configuration, while a
hand-invented name still flows but leaves you to build the panel yourself.

If you want the well-known names instead of typing strings, add the `Radiant.SemConv` package. It
has no dependency on the OpenTelemetry SDK, so even a `netstandard2.0` library can reference it just
to share names. Each built-in is a `Convention` — a name bundled with its kind, unit, and allowed
label keys — and it converts implicitly to its name, so it drops straight into a BCL call:

```csharp
using Radiant;

_Meter.CreateHistogram<double>(SemConv.Http.RequestDuration, SemConv.Http.UnitDuration);
```

`Convention` is also the extension point for *your* names. You declare your own the same way the
built-ins are declared — same type, same factories — so custom instruments carry the same metadata
and flow through the same APIs, with nothing second-class about them:

```csharp
using Radiant;

public static class ParcelConventions
{
    public static readonly Convention Sorted =
        Convention.Counter("parcels.sorted", "{parcel}", "region", "outcome");

    public static readonly Convention SortDuration =
        Convention.Histogram("parcels.sort.duration", "s", LatencyBuckets.Fast, "region", "outcome");

    public static readonly Convention[] All = { Sorted, SortDuration };
}
```

We'll register that set in Part 6 and could emit through it directly with
`host.Client.Record(ParcelConventions.SortDuration, seconds, tags)` — the identical call you'd make
with `SemConv.Http.RequestDuration`.

## Part 2 — Add a span

Traces answer "where did the time go" across a unit of work. The BCL type is `ActivitySource`;
starting an activity returns null when nothing is sampling, so the pattern is null-safe by design.

Give the sorter an activity source and wrap `Sort` in a span:

```csharp
private static readonly ActivitySource _Activity = new ActivitySource("ParcelSorter.Core");

public void Sort(string region)
{
    if (String.IsNullOrWhiteSpace(region)) throw new ArgumentNullException(nameof(region));

    using (Activity? activity = _Activity.StartActivity("sort-parcel", ActivityKind.Internal))
    {
        activity?.SetTag("region", region);

        _InFlight.Add(1);
        long startTicks = Stopwatch.GetTimestamp();
        try
        {
            DoWork();
            // ... record metrics as before ...
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            throw;
        }
    }
}
```

Same principle as the meter: the activity source is named, and the host decides whether to listen.
Nothing here knows Radiant exists.

## Part 3 — Host it with Radiant

Now the application side. This is the code that *does* depend on Radiant, and there should be exactly
one place like it — the composition root. Add the package:

```
dotnet add package Radiant
```

In `Program.cs`, describe the pipeline and start a host. The two names you pass to `Sources` are the
same strings the sorter used — that is the whole wiring:

```csharp
namespace ParcelSorter
{
    using System;
    using System.Threading;
    using Radiant;

    public static class Program
    {
        public static void Main()
        {
            RadiantSettings settings = new RadiantSettings("parcel-sorter");

            // Push to a Collector (default localhost:4317)...
            settings.Otlp.Endpoint = "http://localhost:4317";
            // ...and also serve /metrics in-process, useful before any Collector exists.
            settings.Prometheus.Enable = true;
            settings.Prometheus.Port = 9464;

            // Subscribe to the meter and activity source your code emits into.
            settings.Sources.AddMeter("ParcelSorter.Core");
            settings.Sources.AddActivitySource("ParcelSorter.Core");

            using (RadiantHost host = RadiantHost.Start(settings))
            {
                Console.WriteLine("Sorting. Scrape " + settings.Prometheus.ToScrapeUrl() + " — Ctrl+C to stop.");

                ParcelSorter sorter = new ParcelSorter();
                string[] regions = { "us-east", "us-west", "eu", "apac" };

                while (true)
                {
                    sorter.Enqueue(1);
                    sorter.Sort(regions[Random.Shared.Next(regions.Length)]);
                    Thread.Sleep(50);
                }
            }
        }
    }
}
```

Run it:

```
dotnet run
```

Even with no Collector running, the in-process endpoint is live. Scrape it:

```
curl http://localhost:9464/metrics
```

You'll see `parcels_sorted_total`, `parcels_sort_duration_seconds` buckets, `parcels_inflight`, and
`parcels_queue_depth`, each broken out by the `region` and `outcome` labels. The Prometheus exporter
applied the `_total` and unit suffixes; your code kept the clean dotted names.

That is the entire integration. A settings object, a host, two source names. Everything below is
optional.

## Part 4 — See it in Grafana

The repository ships a reference stack so you don't have to assemble one. From the Radiant repo root:

```
docker compose -f docker/compose.telemetry.yaml up -d
```

That brings up an OpenTelemetry Collector (OTLP on 4317/4318), Prometheus, Tempo, Loki, and Grafana
with datasources and a Radiant Overview dashboard already provisioned. Your app is already pushing to
`localhost:4317`, so open Grafana at `http://localhost:3000`, find the **Radiant Overview**
dashboard, and watch the panels fill in. Because Tempo and Loki are wired for correlation, a span you
click links to the logs from that same request once you add logging in Part 5.

If you'd rather not write the sample app, the `Radiant.Sdk.Console` exerciser in this repo does the
same thing interactively — start a host, emit bursts of metrics/spans/logs, serve `/metrics`:

```
dotnet run --project src/Radiant.Sdk.Console
```

## Part 5 — Optional conveniences

Everything so far used the raw BCL. Radiant adds a few things on top for the application author who
would rather not manage `Meter` fields. None of it replaces the BCL path; it wraps it.

**The convenience emitter.** `host.Client` hands back cached instrument handles, so a call site
records against a handle by name without re-declaring it. Tags are a `RadiantTag` struct rather than
a tuple:

```csharp
host.Client.Increment("parcels.sorted", 1, new RadiantTag("region", "eu"));
host.Client.Record("parcels.sort.duration", seconds, new RadiantTag("region", "eu"));
host.Client.Add("parcels.inflight", 1);
// Register a gauge from the app side instead of inside the sorter — the callback is read at
// collection time, so point it at whatever state holds the current value.
host.Client.RegisterGauge("parcels.backlog", () => backlog.Count, "{parcel}");
```

Every one of those methods also takes a `Convention`, which is usually the better call: the handle
picks up the unit and description from the convention, and the label set is the one you declared, so
the call site can't drift from the definition:

```csharp
host.Client.Increment(ParcelConventions.Sorted, 1, new RadiantTag("region", "eu"));
host.Client.Record(ParcelConventions.SortDuration, seconds, new RadiantTag("region", "eu"));
host.Client.Record(SemConv.Http.RequestDuration, seconds, tags);   // built-in, identical shape
```

**Spans without the null checks.** `RadiantSpan` is a thin `IDisposable` over `ActivitySource`:

```csharp
using (RadiantSpan span = host.StartSpan("sort-parcel", SpanKindEnum.Internal))
{
    span.SetTag("region", region);
    try { /* work */ span.SetOk(); }
    catch (Exception e) { span.RecordException(e); throw; }
}
```

**Logs to Loki.** Ask the host for an `ILogger` and the records flow through the OTLP/Loki pipeline
with `trace_id`/`span_id` stamped on, so they correlate with the span above:

```csharp
Microsoft.Extensions.Logging.ILogger logger = host.CreateLogger("ParcelSorter");
logger.LogInformation("Sorted a parcel for {Region}", region);
```

If your application already owns its logging builder — a generic host, or one you also hand to
another sink — wire Radiant's export in without going through `RadiantHost`:

```csharp
builder.Logging.AddRadiant(settings);
```

## Part 6 — Enforce a metric catalog (optional)

Names are easy to drift from. When you want bounded cardinality *enforced* rather than encouraged,
register your conventions up front. Because the `ParcelConventions` set from Part 1 already carries
each instrument's unit, buckets, and allowed labels, registering the whole catalog is one call:

```csharp
settings.Metrics.DefineAll(ParcelConventions.All);
```

`DefineAll` also takes individual conventions (`DefineAll(a, b, c)`), and `Define` takes one at a
time — built-in or custom, they register the same way: `settings.Metrics.Define(SemConv.Http.RequestDuration)`.

With a catalog present, a measurement that carries an undeclared label — say someone tags a parcel
id — is rejected. The default policy is strict in Debug and lenient in Release: it throws while you
develop and drops the stray label with a one-time warning in production, so a mistake surfaces on
your machine instead of silently exploding your time-series count on a customer's. The catalog is off
until you register something; the plain BCL path never touches it.

## Part 7 — Test your instrumentation

You can assert on metrics without a collector by attaching an in-memory reader to the same meter name
your code emits into. That is exactly what Radiant's own suites do — see `MetricHarness` in
`src/Test.Shared`. The shape:

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

List<Metric> exported = new List<Metric>();
using (MeterProvider provider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("ParcelSorter.Core")
    .AddInMemoryExporter(exported)
    .Build())
{
    ParcelSorter sorter = new ParcelSorter();
    sorter.Sort("eu");

    provider.ForceFlush();
    // Find "parcels.sorted" in `exported`, walk its metric points, assert the sum is 1
    // and that a point carries region=eu.
}
```

Because emit rides the BCL, your test provider and Radiant's host are just two independent listeners
on the same meter — you don't need a running host to verify the numbers.

## What it costs

The honest answer is "very little, until you make it expensive with cardinality." But the numbers are
worth knowing before you sprinkle instruments through a hot loop. Treat the figures below as
order-of-magnitude on a modern x64 core — measure your own hot paths if a call site runs millions of
times a second, but for the overwhelming majority of code this is noise next to the work being
measured.

**When nobody is listening, it's nearly free.** An unobserved `Counter.Add` or `Histogram.Record` is
an `Enabled` check and an early return — low single-digit nanoseconds, and **zero heap allocation** as
long as you pass tags via `TagList` (a stack struct) rather than a `params KeyValuePair[]` array. This
is the property that makes it safe to instrument a library that ships to apps which may never turn
telemetry on. A `StartActivity` on an unsampled source returns `null` in a few nanoseconds, which is
why the `activity?.` null-conditional pattern costs essentially nothing.

**When a host is subscribed, it's still cheap but not free.** Recording a measurement with tags means
the SDK looks up the aggregation state for that exact tag set and updates it in place. Ballpark:

| Operation | Unobserved | Observed |
|---|---|---|
| `Counter.Add` / `UpDownCounter.Add` with a couple of tags | ~1–5 ns, no alloc | ~15–50 ns, no alloc |
| `Histogram.Record` with a couple of tags | ~1–5 ns, no alloc | ~30–70 ns, no alloc |
| `ObservableGauge` callback | not called | called once per export interval (default 15 s) |
| `StartActivity` (span) | ~2–5 ns (returns null) | ~1 µs + a few hundred bytes allocated |
| `ILogger.Log` below the level filter | ~1–2 ns | formatting + enqueue, sub-µs |

Two takeaways from that table. First, **counters and histograms are the cheap pillars** — you can
record them on genuinely hot paths. Second, **spans and logs allocate when they're live**, so you
gate their volume with sampling and log levels rather than recording one of each per inner-loop
iteration. `Traces.SamplingRatio` is the dial: at `0.1` you pay the ~1 µs span cost on roughly a tenth
of root operations and the null-return cost on the rest.

**Export is off your thread.** The OTLP push and the Prometheus scrape both run on the SDK's own
schedule — a background timer at `Metrics.ExportIntervalMs`, or Prometheus pulling on its own
interval. Your `Add` call never blocks on the network; it only mutates in-memory aggregation state.
Serialization and the HTTP round-trip happen later, elsewhere.

**The real cost is memory, and it scales with cardinality, not call count.** The SDK holds one
aggregation record per unique combination of instrument and tag values. A counter tagged only by
`region` (4 values) and `outcome` (2 values) is 8 small records — call it a few kilobytes total,
regardless of whether you record a thousand times a second or a million. Add a `parcel_id` tag and you
get a new record for every parcel that ever passes through, growing without bound until the process
dies or the scrape endpoint times out serializing it. Ten thousand series is comfortable; ten million
is an outage. Call count is close to irrelevant; **distinct tag-value combinations are everything.**

Which leads directly to the rule that keeps all of this affordable.

## The one rule that keeps this healthy

Labels multiply. A metric with a `region` label (four values) and an `outcome` label (two values)
produces eight time series — fine. Add a `parcel_id` label and you get one series per parcel forever,
which is how a scrape endpoint falls over. Keep label values to things you could list on a whiteboard:
regions, statuses, methods, outcomes. Put the high-cardinality identifiers on **spans** (as tags) and
in **logs**, where they belong, and let a trace carry the id from span to log. The catalog in Part 6
exists precisely to catch the day someone forgets this.

That division — bounded dimensions on metrics, unbounded detail on traces and logs — is what lets the
same instrumentation stay cheap under load and still answer "what happened to *this specific*
parcel." Get it right once in the sorter and every service that copies the pattern inherits it.
