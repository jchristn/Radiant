namespace Radiant.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Extensions.Logging;
    using OpenTelemetry;
    using OpenTelemetry.Exporter;
    using OpenTelemetry.Logs;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;

    /// <summary>
    /// Turns a <see cref="RadiantSettings"/> into built OpenTelemetry providers. Isolated from
    /// <see cref="RadiantHost"/> so the provider-construction logic is testable and the host stays a
    /// lifecycle owner rather than a builder.
    /// </summary>
    internal static class PipelineFactory
    {
        #region Internal-Methods

        internal static ResourceBuilder BuildResource(RadiantSettings settings, string instanceId)
        {
            return ResourceBuilder.CreateDefault().AddService(
                serviceName: settings.ServiceName,
                serviceVersion: null,
                autoGenerateServiceInstanceId: false,
                serviceInstanceId: instanceId);
        }

        internal static MeterProvider? BuildMeterProvider(RadiantSettings settings, ResourceBuilder resource, IEnumerable<string> meterNames)
        {
            if (!settings.Metrics.Enable) return null;

            MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder();
            builder.SetResourceBuilder(resource);

            foreach (string name in meterNames)
            {
                if (!String.IsNullOrWhiteSpace(name)) builder.AddMeter(name);
            }

            if (settings.Metrics.IncludeRuntime) builder.AddRuntimeInstrumentation();

            foreach (MetricDefinition definition in settings.Metrics.Definitions)
            {
                if (definition == null) continue;
                if (definition.Kind != MetricKindEnum.Histogram) continue;
                if (definition.Buckets == null || definition.Buckets.Length == 0) continue;

                ExplicitBucketHistogramConfiguration configuration = new ExplicitBucketHistogramConfiguration();
                configuration.Boundaries = definition.Buckets;
                builder.AddView(definition.Key, configuration);
            }

            if (settings.Otlp.Enable)
            {
                int intervalMs = settings.Metrics.ExportIntervalMs;
                builder.AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    ConfigureOtlp(exporterOptions, settings.Otlp.Endpoint, settings.Otlp.Protocol, settings.Otlp.TimeoutMs, BuildHeaders(settings.Otlp.Headers, null, null));
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = intervalMs;
                });
            }

            if (settings.Prometheus.Enable)
            {
                builder.AddPrometheusHttpListener(listenerOptions =>
                {
                    listenerOptions.Host = settings.Prometheus.Hostname;
                    listenerOptions.Port = settings.Prometheus.Port;
                    listenerOptions.ScrapeEndpointPath = settings.Prometheus.Path;
                });
            }

            return builder.Build();
        }

        internal static TracerProvider? BuildTracerProvider(RadiantSettings settings, ResourceBuilder resource, IEnumerable<string> sourceNames)
        {
            if (!settings.Traces.Enable) return null;

            TracerProviderBuilder builder = Sdk.CreateTracerProviderBuilder();
            builder.SetResourceBuilder(resource);

            foreach (string name in sourceNames)
            {
                if (!String.IsNullOrWhiteSpace(name)) builder.AddSource(name);
            }

            builder.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(settings.Traces.SamplingRatio)));

            if (settings.Otlp.Enable)
            {
                builder.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureOtlp(exporterOptions, settings.Otlp.Endpoint, settings.Otlp.Protocol, settings.Otlp.TimeoutMs, BuildHeaders(settings.Otlp.Headers, null, null));
                });
            }

            return builder.Build();
        }

        internal static ILoggerFactory? BuildLoggerFactory(RadiantSettings settings, ResourceBuilder resource)
        {
            if (!settings.Logs.Enable) return null;

            LogLevel effectiveMinimum = ComputeLogMinimum(settings);

            return LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(effectiveMinimum);
                builder.AddOpenTelemetry(options => ConfigureLogging(options, settings, resource));
            });
        }

        internal static LogLevel ComputeLogMinimum(RadiantSettings settings)
        {
            LogLevel minimum = SeverityMap.ToLogLevel(settings.Logs.MinimumSeverity);
            if (settings.Loki.Enable)
            {
                LogLevel lokiMinimum = SeverityMap.ToLogLevel(settings.Loki.MinimumSeverity);
                if ((int)lokiMinimum < (int)minimum) minimum = lokiMinimum;
            }
            return minimum;
        }

        internal static void ConfigureLogging(OpenTelemetryLoggerOptions options, RadiantSettings settings, ResourceBuilder resource)
        {
            options.SetResourceBuilder(resource);
            options.IncludeFormattedMessage = settings.Logs.IncludeFormattedMessage;
            options.IncludeScopes = settings.Logs.IncludeScopes;
            options.ParseStateValues = true;

            if (settings.Otlp.Enable)
            {
                options.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureOtlp(exporterOptions, settings.Otlp.Endpoint, settings.Otlp.Protocol, settings.Otlp.TimeoutMs, BuildHeaders(settings.Otlp.Headers, null, null));
                });
            }

            if (settings.Loki.Enable)
            {
                options.AddOtlpExporter(exporterOptions =>
                {
                    ConfigureOtlp(exporterOptions, settings.Loki.ToLogsEndpoint(), OtlpProtocolEnum.HttpProtobuf, settings.Otlp.TimeoutMs, BuildHeaders(null, "X-Scope-OrgID", settings.Loki.TenantId));
                });
            }
        }

        #endregion

        #region Private-Methods

        private static void ConfigureOtlp(OtlpExporterOptions options, string endpoint, OtlpProtocolEnum protocol, int timeoutMs, string? headers)
        {
            options.Endpoint = new Uri(endpoint);

            // OtlpExportProtocol.Grpc is marked obsolete only for the down-level (netstandard/.NET
            // Framework) compile targets, where OTLP/gRPC needs a hand-configured HttpClientFactory.
            // Radiant's core executes on the app's real runtime (net8/net10 across the fleet), where
            // gRPC is fully supported, so the down-level caution does not apply to actual use. The
            // value is still the app's explicit choice; suppress the compile-time caution here.
#pragma warning disable CS0618
            options.Protocol = protocol == OtlpProtocolEnum.Grpc ? OtlpExportProtocol.Grpc : OtlpExportProtocol.HttpProtobuf;
#pragma warning restore CS0618

            options.TimeoutMilliseconds = timeoutMs;
            if (!String.IsNullOrEmpty(headers)) options.Headers = headers;
        }

        private static string? BuildHeaders(Dictionary<string, string>? baseHeaders, string? extraKey, string? extraValue)
        {
            StringBuilder sb = new StringBuilder();

            if (baseHeaders != null)
            {
                foreach (KeyValuePair<string, string> pair in baseHeaders)
                {
                    if (String.IsNullOrEmpty(pair.Key)) continue;
                    if (sb.Length > 0) sb.Append(",");
                    sb.Append(pair.Key).Append("=").Append(pair.Value ?? String.Empty);
                }
            }

            if (!String.IsNullOrEmpty(extraKey) && !String.IsNullOrEmpty(extraValue))
            {
                if (sb.Length > 0) sb.Append(",");
                sb.Append(extraKey).Append("=").Append(extraValue);
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        #endregion
    }
}
