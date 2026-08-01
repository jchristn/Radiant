namespace SampleApplication
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Radiant;

    /// <summary>
    /// The composition root from INTEGRATION.md. This is the one place that depends on Radiant: it
    /// describes the pipeline, starts a host, subscribes to the names <see cref="ParcelSorter"/>
    /// emits into, and drives some work.
    /// <para>
    /// Run with no arguments to sort continuously until Ctrl+C (point Prometheus at the printed
    /// scrape URL, or watch the reference Grafana stack). Pass <c>--iterations N</c> for a bounded
    /// burst that flushes and exits — handy for a quick smoke test.
    /// </para>
    /// </summary>
    public static class Program
    {
        private static readonly string[] _Regions = new string[] { "us-east", "us-west", "eu", "apac" };

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Optional <c>--iterations N</c> for a bounded run.</param>
        /// <returns>0 on success.</returns>
        public static int Main(string[] args)
        {
            int iterations = ParseIterations(args);

            RadiantSettings settings = new RadiantSettings("parcel-sorter");
            settings.DiagnosticCallback = message => Console.WriteLine("[radiant] " + message);

            // Push to a Collector (default localhost:4317)...
            settings.Otlp.Endpoint = "http://localhost:4317";
            // ...and also serve /metrics in-process, useful before any Collector exists.
            settings.Prometheus.Enable = true;
            settings.Prometheus.Port = 9464;

            // Subscribe to the exact names ParcelSorter emits into.
            settings.Sources.AddMeter("ParcelSorter.Core");
            settings.Sources.AddActivitySource("ParcelSorter.Core");

            // Optional: enforce bounded cardinality by declaring this app's conventions. One call
            // registers their units, buckets, and allowed label keys, and turns on the label policy.
            settings.Metrics.DefineAll(ParcelConventions.All);

            using (RadiantHost host = RadiantHost.Start(settings))
            {
                ILogger logger = host.CreateLogger("SampleApplication");

                Console.WriteLine("parcel-sorter started (instance " + host.ServiceInstanceId + ").");
                Console.WriteLine("Prometheus scrape: " + settings.Prometheus.ToScrapeUrl());
                if (iterations > 0) Console.WriteLine("Sorting " + iterations + " parcels, then flushing and exiting.");
                else Console.WriteLine("Sorting until Ctrl+C.");
                Console.WriteLine("");

                bool cancelled = false;
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancelled = true;
                };

                ParcelSorter sorter = new ParcelSorter();
                long processed = 0;

                while (!cancelled && (iterations <= 0 || processed < iterations))
                {
                    string region = _Regions[Random.Shared.Next(_Regions.Length)];
                    sorter.Enqueue(1);
                    sorter.Sort(region);
                    processed++;

                    if (processed % 25 == 0)
                    {
                        logger.LogInformation("Sorted {Processed} parcels (last region {Region}).", processed, region);
                    }

                    Thread.Sleep(50);
                }

                Console.WriteLine("");
                Console.WriteLine("Sorted " + processed + " parcels. Flushing exporters...");
                host.ForceFlush(10000);
                Console.WriteLine("Done.");
            }

            return 0;
        }

        private static int ParseIterations(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--iterations" && i + 1 < args.Length)
                {
                    int parsed;
                    if (Int32.TryParse(args[i + 1], out parsed) && parsed > 0) return parsed;
                }
            }
            return 0;
        }
    }
}
