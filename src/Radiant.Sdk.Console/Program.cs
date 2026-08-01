namespace Radiant.Sdk.Console
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using GetSomeInput;
    using Microsoft.Extensions.Logging;
    using Radiant;

    /// <summary>
    /// Interactive exerciser for Radiant. Starts a host, emits sample metrics, spans, and logs, and
    /// serves an in-process Prometheus endpoint so the pipeline can be driven by hand against a local
    /// Grafana stack with no host service to write.
    /// </summary>
    public static class Program
    {
        private static RadiantHost? _Host;
        private static readonly Random _Random = new Random();

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Unused command-line arguments.</param>
        /// <returns>A task that completes when the user quits.</returns>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("");
            Console.WriteLine("Radiant SDK console exerciser");
            Console.WriteLine("Type '?' for the menu.");
            Console.WriteLine("");

            bool running = true;
            while (running)
            {
                string command = Inputty.GetString("radiant>", "?", false);
                switch (command.Trim().ToLowerInvariant())
                {
                    case "?":
                        Menu();
                        break;
                    case "start":
                        Start();
                        break;
                    case "stop":
                        Stop();
                        break;
                    case "count":
                        Count();
                        break;
                    case "hist":
                        Histogram();
                        break;
                    case "gauge":
                        Gauge();
                        break;
                    case "span":
                        await SpanAsync().ConfigureAwait(false);
                        break;
                    case "log":
                        Log();
                        break;
                    case "load":
                        await LoadAsync().ConfigureAwait(false);
                        break;
                    case "flush":
                        Flush();
                        break;
                    case "status":
                        Status();
                        break;
                    case "q":
                    case "quit":
                        running = false;
                        break;
                    default:
                        Console.WriteLine("Unknown command. Type '?' for the menu.");
                        break;
                }
            }

            Stop();
        }

        private static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("  ?       help");
            Console.WriteLine("  start   start a host (prompts for endpoints)");
            Console.WriteLine("  stop    dispose the host");
            Console.WriteLine("  count   increment a request counter");
            Console.WriteLine("  hist    record a latency observation");
            Console.WriteLine("  gauge   register a live gauge from state");
            Console.WriteLine("  span    start and end a span");
            Console.WriteLine("  log     emit a log record");
            Console.WriteLine("  load    emit a burst of mixed telemetry");
            Console.WriteLine("  flush   force exporters to flush");
            Console.WriteLine("  status  show host status");
            Console.WriteLine("  q       quit");
            Console.WriteLine("");
        }

        private static void Start()
        {
            if (_Host != null)
            {
                Console.WriteLine("A host is already running. Stop it first.");
                return;
            }

            string serviceName = Inputty.GetString("Service name :", "radiant-console", false);
            bool otlp = Inputty.GetBoolean("Enable OTLP push :", true);
            string otlpEndpoint = Inputty.GetString("OTLP endpoint :", "http://localhost:4317", false);
            bool prometheus = Inputty.GetBoolean("Enable Prometheus scrape :", true);
            int prometheusPort = Inputty.GetInteger("Prometheus port :", 9464, true, false);
            bool loki = Inputty.GetBoolean("Enable direct Loki export :", false);
            string lokiEndpoint = Inputty.GetString("Loki endpoint :", "http://localhost:3100/otlp", false);

            RadiantSettings settings = new RadiantSettings(serviceName);
            settings.DiagnosticCallback = message => Console.WriteLine("[diag] " + message);
            settings.Otlp.Enable = otlp;
            settings.Otlp.Endpoint = otlpEndpoint;
            settings.Prometheus.Enable = prometheus;
            settings.Prometheus.Port = prometheusPort;
            settings.Loki.Enable = loki;
            if (loki) settings.Loki.Endpoint = lokiEndpoint;

            try
            {
                _Host = RadiantHost.Start(settings);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start host: " + e.Message);
                if (e.InnerException != null) Console.WriteLine("  cause: " + e.InnerException.Message);
                _Host = null;
                return;
            }

            Console.WriteLine("Host started. Instance id " + _Host.ServiceInstanceId + ".");
            if (prometheus) Console.WriteLine("Scrape at " + settings.Prometheus.ToScrapeUrl());
        }

        private static void Stop()
        {
            if (_Host == null) return;
            _Host.Dispose();
            _Host = null;
            Console.WriteLine("Host stopped.");
        }

        private static void Count()
        {
            if (!EnsureHost()) return;
            string protocol = Inputty.GetString("protocol label :", "http", false);
            _Host!.Client.Increment("radiant.console.requests", 1.0, new RadiantTag(SemConv.Attributes.Protocol, protocol));
            Console.WriteLine("Incremented radiant.console.requests {protocol=" + protocol + "}.");
        }

        private static void Histogram()
        {
            if (!EnsureHost()) return;
            double seconds = Inputty.GetDouble("latency seconds :", 0.05, false, true);
            _Host!.Client.Record("radiant.console.latency", seconds, new RadiantTag(SemConv.Attributes.Protocol, "http"));
            Console.WriteLine("Recorded radiant.console.latency = " + seconds + "s.");
        }

        private static void Gauge()
        {
            if (!EnsureHost()) return;
            int depth = Inputty.GetInteger("queue depth :", 5, true, true);
            _Host!.Client.RegisterGauge("radiant.console.queue.depth", () => depth, "{item}");
            Console.WriteLine("Registered gauge radiant.console.queue.depth = " + depth + ".");
        }

        private static async Task SpanAsync()
        {
            if (!EnsureHost()) return;
            string name = Inputty.GetString("span name :", "work", false);
            using (RadiantSpan span = _Host!.StartSpan(name, SpanKindEnum.Server))
            {
                span.SetTag(SemConv.Attributes.Protocol, "http");
                await Task.Delay(_Random.Next(5, 50)).ConfigureAwait(false);
                span.SetOk("done");
            }
            Console.WriteLine("Span '" + name + "' completed (recording=" + (_Host!.ActivitySource.HasListeners()) + ").");
        }

        private static void Log()
        {
            if (!EnsureHost()) return;
            string message = Inputty.GetString("message :", "hello from the exerciser", false);
            ILogger logger = _Host!.CreateLogger("Radiant.Console");
            logger.LogInformation("{Message}", message);
            Console.WriteLine("Logged.");
        }

        private static async Task LoadAsync()
        {
            if (!EnsureHost()) return;
            int count = Inputty.GetInteger("iterations :", 100, true, false);
            string[] protocols = new string[] { "http", "ws", "grpc" };
            int[] statuses = new int[] { 200, 200, 200, 404, 500 };

            for (int i = 0; i < count; i++)
            {
                string protocol = protocols[_Random.Next(protocols.Length)];
                int status = statuses[_Random.Next(statuses.Length)];
                _Host!.Client.Increment("radiant.console.requests", 1.0,
                    new RadiantTag(SemConv.Attributes.Protocol, protocol),
                    new RadiantTag(SemConv.Http.AttributeStatusCode, status));
                _Host!.Client.Record("radiant.console.latency", _Random.NextDouble() * 0.5,
                    new RadiantTag(SemConv.Attributes.Protocol, protocol));

                using (RadiantSpan span = _Host!.StartSpan("load-item", SpanKindEnum.Server))
                {
                    span.SetTag(SemConv.Http.AttributeStatusCode, status);
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
            Console.WriteLine("Emitted " + count + " iterations of mixed telemetry.");
        }

        private static void Flush()
        {
            if (!EnsureHost()) return;
            bool ok = _Host!.ForceFlush(10000);
            Console.WriteLine(ok ? "Flushed." : "Flush timed out.");
        }

        private static void Status()
        {
            if (_Host == null)
            {
                Console.WriteLine("No host running.");
                return;
            }

            Console.WriteLine("Enabled        : " + _Host.IsEnabled);
            Console.WriteLine("Instance id    : " + _Host.ServiceInstanceId);
            Console.WriteLine("Meter provider : " + (_Host.MeterProvider != null));
            Console.WriteLine("Tracer provider: " + (_Host.TracerProvider != null));
            Console.WriteLine("Logger factory : " + (_Host.LoggerFactory != null));
        }

        private static bool EnsureHost()
        {
            if (_Host != null) return true;
            Console.WriteLine("No host running. Use 'start' first.");
            return false;
        }
    }
}
