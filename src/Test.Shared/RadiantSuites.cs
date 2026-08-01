namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Radiant;
    using Touchstone.Core;

    /// <summary>
    /// All Radiant test suite descriptors. Runner-agnostic: consumed by the console runner, xUnit,
    /// and NUnit projects through <see cref="All"/>. No console output here — output is the runner's
    /// responsibility.
    /// </summary>
    public static class RadiantSuites
    {
        /// <summary>
        /// Every suite.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    SettingsSuite(),
                    TagSuite(),
                    SemConvSuite(),
                    ConventionSuite(),
                    CatalogSuite(),
                    HostSuite(),
                    MetricsSuite(),
                    SpanSuite(),
                    ExportSuite()
                };
            }
        }

        #region Settings

        private static TestSuiteDescriptor SettingsSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Settings",
                displayName: "Settings validation and clamping",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Settings", "ServiceNameRequired", "ServiceName rejects null/empty", _ =>
                    {
                        RadiantSettings settings = new RadiantSettings();
                        try
                        {
                            settings.ServiceName = "  ";
                            throw new Exception("Expected ArgumentNullException.");
                        }
                        catch (ArgumentNullException) { }
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "ExportIntervalClamp", "ExportIntervalMs clamps to 1000..300000", _ =>
                    {
                        MetricsSettings metrics = new MetricsSettings();
                        metrics.ExportIntervalMs = 10;
                        AssertEqual(1000, metrics.ExportIntervalMs, "low clamp");
                        metrics.ExportIntervalMs = 999999;
                        AssertEqual(300000, metrics.ExportIntervalMs, "high clamp");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "SamplingRatioClamp", "SamplingRatio clamps to 0..1", _ =>
                    {
                        TracesSettings traces = new TracesSettings();
                        traces.SamplingRatio = 5.0;
                        AssertEqual(1.0, traces.SamplingRatio, "high clamp");
                        traces.SamplingRatio = -1.0;
                        AssertEqual(0.0, traces.SamplingRatio, "low clamp");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "OtlpEndpointValidation", "OTLP endpoint rejects non-absolute URI", _ =>
                    {
                        OtlpExporterSettings otlp = new OtlpExporterSettings();
                        try
                        {
                            otlp.Endpoint = "not-a-uri";
                            throw new Exception("Expected ArgumentException.");
                        }
                        catch (ArgumentException) { }
                        otlp.Endpoint = "http://collector:4317";
                        AssertEqual("http://collector:4317", otlp.Endpoint, "valid endpoint");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "PrometheusPortAndPath", "Prometheus port clamps and path normalizes", _ =>
                    {
                        PrometheusScrapeSettings prom = new PrometheusScrapeSettings();
                        prom.Port = 70000;
                        AssertEqual(65535, prom.Port, "port clamp");
                        prom.Path = "custom";
                        AssertEqual("/custom", prom.Path, "path leading slash");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "SeverityClamp", "Log minimum severity clamps to 0..7", _ =>
                    {
                        LogsSettings logs = new LogsSettings();
                        logs.MinimumSeverity = 99;
                        AssertEqual(7, logs.MinimumSeverity, "high clamp");
                        logs.MinimumSeverity = -3;
                        AssertEqual(0, logs.MinimumSeverity, "low clamp");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "LokiLogsEndpoint", "Loki endpoint appends /v1/logs", _ =>
                    {
                        LokiExportSettings loki = new LokiExportSettings();
                        loki.Endpoint = "http://loki:3100/otlp";
                        AssertEqual("http://loki:3100/otlp/v1/logs", loki.ToLogsEndpoint(), "logs endpoint");
                        return Task.CompletedTask;
                    }),

                    Case("Settings", "DefaultsMatchPlan", "Defaults match the settled plan", _ =>
                    {
                        RadiantSettings settings = new RadiantSettings("svc");
                        if (!settings.Enable) throw new Exception("Enable should default true.");
                        AssertEqual(15000, settings.Metrics.ExportIntervalMs, "export interval default");
                        if (!settings.Metrics.IncludeRuntime) throw new Exception("IncludeRuntime should default true.");
                        if (settings.Prometheus.Enable) throw new Exception("Prometheus should default disabled.");
                        AssertEqual(9464, settings.Prometheus.Port, "prometheus port default");
                        if (settings.Otlp.Protocol != OtlpProtocolEnum.Grpc) throw new Exception("OTLP should default grpc.");
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Tag

        private static TestSuiteDescriptor TagSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Tag",
                displayName: "RadiantTag value semantics",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Tag", "NullKeyThrows", "Null/empty key throws", _ =>
                    {
                        try
                        {
                            RadiantTag tag = new RadiantTag("", 1);
                            throw new Exception("Expected ArgumentNullException.");
                        }
                        catch (ArgumentNullException) { }
                        return Task.CompletedTask;
                    }),

                    Case("Tag", "Equality", "Equal by key and value", _ =>
                    {
                        RadiantTag a = new RadiantTag("protocol", "http");
                        RadiantTag b = new RadiantTag("protocol", "http");
                        RadiantTag c = new RadiantTag("protocol", "ws");
                        if (!a.Equals(b)) throw new Exception("Equal tags should compare equal.");
                        if (a.Equals(c)) throw new Exception("Different values should not be equal.");
                        return Task.CompletedTask;
                    }),

                    Case("Tag", "ToKeyValuePair", "Converts to key/value pair", _ =>
                    {
                        RadiantTag tag = new RadiantTag("k", 42);
                        KeyValuePair<string, object?> pair = tag.ToKeyValuePair();
                        AssertEqual("k", pair.Key, "key");
                        AssertEqual(42, (int)pair.Value!, "value");
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region SemConv

        private static TestSuiteDescriptor SemConvSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SemConv",
                displayName: "Semantic convention constants",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SemConv", "HttpNames", "HTTP instrument names follow OTel conventions", _ =>
                    {
                        AssertEqual("http.server.request.duration", SemConv.Http.RequestDuration.Name, "request duration");
                        AssertEqual("http.server.active_requests", SemConv.Http.ServerActiveRequests.Name, "active requests");
                        AssertEqual("s", SemConv.Http.UnitDuration, "duration unit");
                        AssertEqual(MetricKindEnum.Histogram, SemConv.Http.RequestDuration.Kind, "request duration kind");
                        return Task.CompletedTask;
                    }),

                    Case("SemConv", "ProtocolAttribute", "Protocol attribute key is stable", _ =>
                    {
                        AssertEqual("protocol", SemConv.Attributes.Protocol, "protocol key");
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Convention

        private static TestSuiteDescriptor ConventionSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Convention",
                displayName: "Convention descriptor and emit",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Convention", "FactoryMetadata", "Factories capture kind, unit, labels, buckets", _ =>
                    {
                        Convention histogram = Convention.Histogram("app.latency", "s", new double[] { 0.1, 0.5, 1.0 }, "region", "outcome")
                            .WithDescription("App latency.");
                        AssertEqual(MetricKindEnum.Histogram, histogram.Kind, "kind");
                        AssertEqual("s", histogram.Unit!, "unit");
                        AssertEqual("App latency.", histogram.Description!, "description");
                        AssertEqual(2, histogram.LabelKeys.Count, "label count");
                        if (histogram.Buckets == null || histogram.Buckets.Length != 3) throw new Exception("Buckets not captured.");

                        Convention counter = Convention.Counter("app.count", "{item}", "region");
                        AssertEqual(MetricKindEnum.Counter, counter.Kind, "counter kind");
                        return Task.CompletedTask;
                    }),

                    Case("Convention", "ImplicitString", "Convention converts to its name", _ =>
                    {
                        Convention convention = Convention.Counter("app.thing");
                        string? name = convention;
                        AssertEqual("app.thing", name!, "implicit string");
                        AssertEqual("app.thing", convention.ToString(), "ToString");
                        return Task.CompletedTask;
                    }),

                    Case("Convention", "ClientEmitViaConvention", "Client records through a convention", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("convention-emit");
                        Convention sorted = Convention.Counter("app.sorted", "{item}", "region");

                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            host.Client.Increment(sorted, 2, new RadiantTag("region", "eu"));
                            host.Client.Increment(sorted, 3, new RadiantTag("region", "eu"));
                            double? sum = harness.GetSumWithTag("app.sorted", "region", "eu");
                            AssertEqual(5.0, sum ?? -1, "convention counter sum");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Convention", "DefineAllEnforcesLabels", "DefineAll registers catalog and enforces labels", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("convention-catalog");
                        settings.Metrics.LabelPolicy = LabelPolicyEnum.Strict;
                        Convention sorted = Convention.Counter("app.catalog.sorted", "{item}", "region");
                        settings.Metrics.DefineAll(sorted);

                        using (RadiantHost host = RadiantHost.Start(settings))
                        {
                            try
                            {
                                host.Client.Increment(sorted, 1, new RadiantTag("undeclared", "x"));
                                throw new Exception("Expected ArgumentException for undeclared label.");
                            }
                            catch (ArgumentException) { }
                        }
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Catalog

        private static TestSuiteDescriptor CatalogSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Catalog",
                displayName: "Declared catalog label policy",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Catalog", "StrictRejectsUndeclared", "Strict policy throws on undeclared label", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("catalog-strict");
                        settings.Metrics.LabelPolicy = LabelPolicyEnum.Strict;
                        settings.Metrics.Define("radiant.cat.strict", MetricKindEnum.Counter, "1", new List<string> { "allowed" });

                        using (RadiantHost host = RadiantHost.Start(settings))
                        {
                            try
                            {
                                host.Client.Increment("radiant.cat.strict", 1, new RadiantTag("notallowed", "x"));
                                throw new Exception("Expected ArgumentException for undeclared label.");
                            }
                            catch (ArgumentException) { }
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Catalog", "LenientDropsUndeclared", "Lenient policy drops undeclared label but records value", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("catalog-lenient");
                        settings.Metrics.LabelPolicy = LabelPolicyEnum.Lenient;
                        settings.Metrics.Define("radiant.cat.lenient", MetricKindEnum.Counter, "1", new List<string> { "allowed" });

                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            host.Client.Increment("radiant.cat.lenient", 4, new RadiantTag("allowed", "yes"), new RadiantTag("notallowed", "x"));
                            double? sum = harness.GetSum("radiant.cat.lenient");
                            AssertEqual(4.0, sum ?? -1, "recorded value");
                            if (harness.HasTag("radiant.cat.lenient", "notallowed", "x"))
                                throw new Exception("Undeclared label should have been dropped.");
                            if (!harness.HasTag("radiant.cat.lenient", "allowed", "yes"))
                                throw new Exception("Declared label should be present.");
                        }
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Host

        private static TestSuiteDescriptor HostSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Host",
                displayName: "Host lifecycle",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Host", "NullSettingsThrows", "Start rejects null settings", _ =>
                    {
                        try
                        {
                            RadiantHost.Start(null!);
                            throw new Exception("Expected ArgumentNullException.");
                        }
                        catch (ArgumentNullException) { }
                        return Task.CompletedTask;
                    }),

                    Case("Host", "InstanceIdGenerated", "Auto instance id when unset", _ =>
                    {
                        using (RadiantHost host = RadiantHost.Start(NoExportSettings("host-id")))
                        {
                            if (String.IsNullOrWhiteSpace(host.ServiceInstanceId))
                                throw new Exception("Instance id should be generated.");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Host", "DisabledIsInert", "Disabled host builds no providers", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("host-disabled");
                        settings.Enable = false;
                        using (RadiantHost host = RadiantHost.Start(settings))
                        {
                            if (host.IsEnabled) throw new Exception("Host should be inert when disabled.");
                            if (host.MeterProvider != null) throw new Exception("No meter provider when disabled.");
                            // Emitting must not throw even when inert.
                            host.Client.Increment("radiant.inert", 1);
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Host", "DisposeIsIdempotent", "Dispose twice is safe", _ =>
                    {
                        RadiantHost host = RadiantHost.Start(NoExportSettings("host-dispose"));
                        host.Dispose();
                        host.Dispose();
                        return Task.CompletedTask;
                    }),

                    Case("Host", "AsyncDispose", "await using disposes cleanly", async _ =>
                    {
                        await using (RadiantHost host = RadiantHost.Start(NoExportSettings("host-async")))
                        {
                            host.Client.Increment("radiant.async", 1);
                        }
                    })
                });
        }

        #endregion

        #region Metrics

        private static TestSuiteDescriptor MetricsSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Metrics",
                displayName: "Metric emission through the client",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Metrics", "CounterValueAndTags", "Counter records value with tags", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("metrics-counter");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            host.Client.Increment("radiant.requests", 2, new RadiantTag("protocol", "http"));
                            host.Client.Increment("radiant.requests", 3, new RadiantTag("protocol", "http"));
                            double? sum = harness.GetSumWithTag("radiant.requests", "protocol", "http");
                            AssertEqual(5.0, sum ?? -1, "counter sum");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Metrics", "Histogram", "Histogram records observations", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("metrics-histogram");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            host.Client.Record("radiant.latency", 0.1);
                            host.Client.Record("radiant.latency", 0.2);
                            long? count = harness.GetHistogramCount("radiant.latency");
                            AssertEqual(2L, count ?? -1, "histogram count");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Metrics", "UpDownCounter", "Up/down counter nets deltas", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("metrics-updown");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            host.Client.Add("radiant.inflight", 5);
                            host.Client.Add("radiant.inflight", -2);
                            double? sum = harness.GetSum("radiant.inflight");
                            AssertEqual(3.0, sum ?? -1, "updown net");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Metrics", "ObservableGauge", "Gauge reads from state at collection", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("metrics-gauge");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            int depth = 7;
                            host.Client.RegisterGauge("radiant.queue.depth", () => depth, "{item}");
                            double? value = harness.GetGauge("radiant.queue.depth");
                            AssertEqual(7.0, value ?? -1, "gauge value");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Metrics", "ProcessMetrics", "Built-in process metrics are present", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("metrics-process");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness(settings.ServiceName))
                        {
                            double? memory = harness.GetGauge(SemConv.Process.MemoryUsage.Name);
                            if (memory == null || memory <= 0) throw new Exception("Process memory gauge should be positive.");
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Metrics", "RawBclEmit", "Raw BCL meter is collected by name", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("metrics-bcl");
                        settings.Sources.AddMeter("Radiant.RawTest");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        using (MetricHarness harness = new MetricHarness("Radiant.RawTest"))
                        {
                            using (System.Diagnostics.Metrics.Meter meter = new System.Diagnostics.Metrics.Meter("Radiant.RawTest"))
                            {
                                System.Diagnostics.Metrics.Counter<long> counter = meter.CreateCounter<long>("radiant.raw");
                                counter.Add(9);
                                double? sum = harness.GetSum("radiant.raw");
                                AssertEqual(9.0, sum ?? -1, "raw counter");
                            }
                        }
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Span

        private static TestSuiteDescriptor SpanSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Span",
                displayName: "Span creation and enrichment",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Span", "RecordsWhenSampled", "Span records with a listener attached", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("span-svc");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        {
                            ActivityListener listener = new ActivityListener();
                            listener.ShouldListenTo = source => source.Name == settings.ServiceName;
                            listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData;
                            ActivitySource.AddActivityListener(listener);

                            try
                            {
                                using (RadiantSpan span = host.StartSpan("unit-of-work", SpanKindEnum.Server))
                                {
                                    if (!span.IsRecording) throw new Exception("Span should be recording.");
                                    span.SetTag("protocol", "http").SetOk("done");
                                    if (String.IsNullOrEmpty(span.TraceId)) throw new Exception("TraceId should be set.");
                                }
                            }
                            finally
                            {
                                listener.Dispose();
                            }
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Span", "RecordException", "RecordException sets error status", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("span-ex");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        {
                            ActivityListener listener = new ActivityListener();
                            listener.ShouldListenTo = source => source.Name == settings.ServiceName;
                            listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData;
                            ActivitySource.AddActivityListener(listener);

                            try
                            {
                                using (RadiantSpan span = host.StartSpan("failing"))
                                {
                                    span.RecordException(new InvalidOperationException("boom"));
                                    if (span.Activity == null) throw new Exception("Activity should exist.");
                                    if (span.Activity.Status != ActivityStatusCode.Error)
                                        throw new Exception("Status should be Error.");
                                }
                            }
                            finally
                            {
                                listener.Dispose();
                            }
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Span", "InertWhenUnsampled", "Span is a safe no-op with no listener", _ =>
                    {
                        RadiantSettings settings = NoExportSettings("span-inert");
                        using (RadiantHost host = RadiantHost.Start(settings))
                        {
                            using (RadiantSpan span = host.StartSpan("nobody-listening"))
                            {
                                span.SetTag("k", "v").SetError("still safe");
                            }
                        }
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Export

        private static TestSuiteDescriptor ExportSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Export",
                displayName: "Export pipeline wiring",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Export", "OtlpHttpReceivesMetrics", "OTLP/HTTP export reaches the endpoint", _ =>
                    {
                        using (RecordingHttpEndpoint endpoint = new RecordingHttpEndpoint())
                        {
                            RadiantSettings settings = new RadiantSettings("export-svc");
                            settings.Otlp.Enable = true;
                            settings.Otlp.Protocol = OtlpProtocolEnum.HttpProtobuf;
                            settings.Otlp.Endpoint = endpoint.BaseUrl;
                            settings.Metrics.ExportIntervalMs = 1000;
                            settings.Traces.Enable = false;
                            settings.Logs.Enable = false;
                            settings.Prometheus.Enable = false;

                            using (RadiantHost host = RadiantHost.Start(settings))
                            {
                                host.Client.Increment("radiant.export.counter", 1);
                                host.ForceFlush(5000);
                                if (!endpoint.WaitForAnyRequest(5000))
                                    throw new Exception("OTLP endpoint received no request.");
                            }
                        }
                        return Task.CompletedTask;
                    }),

                    Case("Export", "LokiReceivesLogs", "Direct Loki export reaches the endpoint", _ =>
                    {
                        using (RecordingHttpEndpoint endpoint = new RecordingHttpEndpoint())
                        {
                            RadiantSettings settings = new RadiantSettings("loki-svc");
                            settings.Otlp.Enable = false;
                            settings.Metrics.Enable = false;
                            settings.Traces.Enable = false;
                            settings.Logs.Enable = true;
                            settings.Logs.MinimumSeverity = 0;
                            settings.Prometheus.Enable = false;
                            settings.Loki.Enable = true;
                            settings.Loki.Endpoint = endpoint.BaseUrl + "/otlp";
                            settings.Loki.MinimumSeverity = 0;

                            using (RadiantHost host = RadiantHost.Start(settings))
                            {
                                Microsoft.Extensions.Logging.ILogger logger = host.CreateLogger("test");
                                Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(logger, "hello loki");
                                host.LoggerFactory?.Dispose();
                                if (!endpoint.WaitForAnyRequest(5000))
                                    throw new Exception("Loki endpoint received no request.");
                            }
                        }
                        return Task.CompletedTask;
                    })
                });
        }

        #endregion

        #region Helpers

        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<System.Threading.CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(
                suiteId: suiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: executeAsync);
        }

        private static RadiantSettings NoExportSettings(string serviceName)
        {
            RadiantSettings settings = new RadiantSettings(serviceName);
            settings.Otlp.Enable = false;
            settings.Prometheus.Enable = false;
            settings.Logs.Enable = false;
            settings.Traces.Enable = true;
            settings.Metrics.Enable = true;
            settings.Metrics.IncludeRuntime = false;
            return settings;
        }

        private static void AssertEqual(object expected, object actual, string what)
        {
            if (!Equals(expected, actual))
                throw new Exception("Expected " + what + " = " + expected + " but was " + actual + ".");
        }

        #endregion
    }
}
