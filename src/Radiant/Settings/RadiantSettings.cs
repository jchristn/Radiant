namespace Radiant
{
    using System;

    /// <summary>
    /// The single settings object the application fills to describe its telemetry pipeline. Passed
    /// once to <see cref="RadiantHost.Start(RadiantSettings)"/> at the composition root. Every
    /// pillar is on by default; a fresh instance with only <see cref="ServiceName"/> set produces a
    /// working pipeline that pushes OTLP to a local collector.
    /// <para>
    /// This type is not thread safe. Build and mutate it during composition, then hand it to the
    /// host and stop mutating it.
    /// </para>
    /// </summary>
    public class RadiantSettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for the whole pipeline. Default true. When false,
        /// <see cref="RadiantHost.Start(RadiantSettings)"/> returns an inert host that builds no
        /// providers and binds no ports — telemetry emit stays a no-op.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The logical service name stamped as the <c>service.name</c> resource attribute.
        /// Required and non-empty. Setting a null or empty value throws.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string ServiceName
        {
            get
            {
                return _ServiceName;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ServiceName));
                _ServiceName = value;
            }
        }

        /// <summary>
        /// The service instance identifier stamped as <c>service.instance.id</c>. Default null,
        /// which causes the host to generate a stable GUID for the process lifetime.
        /// </summary>
        public string? ServiceInstanceId { get; set; } = null;

        /// <summary>
        /// The meter and activity-source names the host subscribes to. Never null.
        /// </summary>
        public SourceSettings Sources
        {
            get
            {
                return _Sources;
            }
            set
            {
                _Sources = value ?? new SourceSettings();
            }
        }

        /// <summary>
        /// Metrics pillar settings. Never null.
        /// </summary>
        public MetricsSettings Metrics
        {
            get
            {
                return _Metrics;
            }
            set
            {
                _Metrics = value ?? new MetricsSettings();
            }
        }

        /// <summary>
        /// Traces pillar settings. Never null.
        /// </summary>
        public TracesSettings Traces
        {
            get
            {
                return _Traces;
            }
            set
            {
                _Traces = value ?? new TracesSettings();
            }
        }

        /// <summary>
        /// Logs pillar settings. Never null.
        /// </summary>
        public LogsSettings Logs
        {
            get
            {
                return _Logs;
            }
            set
            {
                _Logs = value ?? new LogsSettings();
            }
        }

        /// <summary>
        /// OTLP push exporter settings. Never null.
        /// </summary>
        public OtlpExporterSettings Otlp
        {
            get
            {
                return _Otlp;
            }
            set
            {
                _Otlp = value ?? new OtlpExporterSettings();
            }
        }

        /// <summary>
        /// In-process Prometheus scrape endpoint settings. Never null.
        /// </summary>
        public PrometheusScrapeSettings Prometheus
        {
            get
            {
                return _Prometheus;
            }
            set
            {
                _Prometheus = value ?? new PrometheusScrapeSettings();
            }
        }

        /// <summary>
        /// Direct Loki export settings. Never null.
        /// </summary>
        public LokiExportSettings Loki
        {
            get
            {
                return _Loki;
            }
            set
            {
                _Loki = value ?? new LokiExportSettings();
            }
        }

        /// <summary>
        /// An optional callback invoked with internal diagnostic messages (for example a dropped
        /// undeclared label or an exporter warning). Library code never writes to the console; this
        /// is how host-side diagnostics surface when no logger is available yet. May be null.
        /// </summary>
        public Action<string>? DiagnosticCallback { get; set; } = null;

        #endregion

        #region Private-Members

        private string _ServiceName = String.Empty;
        private SourceSettings _Sources = new SourceSettings();
        private MetricsSettings _Metrics = new MetricsSettings();
        private TracesSettings _Traces = new TracesSettings();
        private LogsSettings _Logs = new LogsSettings();
        private OtlpExporterSettings _Otlp = new OtlpExporterSettings();
        private PrometheusScrapeSettings _Prometheus = new PrometheusScrapeSettings();
        private LokiExportSettings _Loki = new LokiExportSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create an empty settings object. Set <see cref="ServiceName"/> before starting a host.
        /// </summary>
        public RadiantSettings()
        {
        }

        /// <summary>
        /// Create a settings object for the given service.
        /// </summary>
        /// <param name="serviceName">The logical service name. Must be non-null and non-empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceName"/> is null or empty.</exception>
        public RadiantSettings(string serviceName)
        {
            if (String.IsNullOrWhiteSpace(serviceName)) throw new ArgumentNullException(nameof(serviceName));
            _ServiceName = serviceName;
        }

        #endregion
    }
}
