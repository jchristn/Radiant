namespace Radiant
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using Radiant.Internal;

    /// <summary>
    /// The single object an application owns to turn a <see cref="RadiantSettings"/> into a wired,
    /// exportable telemetry pipeline. <see cref="Start(RadiantSettings)"/> builds the
    /// <see cref="MeterProvider"/>, <see cref="TracerProvider"/>, and logging pipeline, subscribes
    /// to the configured sources, binds the optional Prometheus scrape port, and returns a host that
    /// is both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>.
    /// <para>
    /// One host owns the pipeline for the process. Starting a second host that binds a Prometheus
    /// scrape port already bound by this process fails fast with a clear
    /// <see cref="RadiantException"/> rather than a buried socket error.
    /// </para>
    /// <para>
    /// Emitting telemetry does not require this type: libraries and application code emit through the
    /// .NET base class library (<see cref="Meter"/>, <see cref="ActivitySource"/>,
    /// <see cref="ILogger"/>) and stay a no-op until a host subscribes. The optional
    /// <see cref="Client"/> is a convenience over the host's own meter and activity source.
    /// </para>
    /// <para>
    /// Thread safety: <see cref="Start(RadiantSettings)"/> and disposal are the boundary operations
    /// and must not race each other. Once started, the host and its <see cref="Client"/> are safe to
    /// use concurrently from many threads.
    /// </para>
    /// </summary>
    public sealed class RadiantHost : IDisposable, IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Whether this host built and wired a live pipeline. False when
        /// <see cref="RadiantSettings.Enable"/> was false at start, in which case the host is inert:
        /// its meter and activity source still exist but nothing is subscribed or exported.
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return _Enabled;
            }
        }

        /// <summary>
        /// The resolved service instance identifier stamped as <c>service.instance.id</c>. Equal to
        /// the configured value, or an auto-generated GUID when none was configured.
        /// </summary>
        public string ServiceInstanceId
        {
            get
            {
                return _InstanceId;
            }
        }

        /// <summary>
        /// The host's own meter, named after the service. The convenience <see cref="Client"/> and
        /// the built-in process gauges emit through it, and it is always among the subscribed meter
        /// names. Application code may create its own meters instead; add their names to
        /// <see cref="RadiantSettings.Sources"/>.
        /// </summary>
        public Meter Meter
        {
            get
            {
                return _Meter;
            }
        }

        /// <summary>
        /// The host's own activity source, named after the service. <see cref="RadiantSpan"/>
        /// started via <see cref="StartSpan(string, SpanKindEnum)"/> uses it, and it is always among
        /// the subscribed activity-source names.
        /// </summary>
        public ActivitySource ActivitySource
        {
            get
            {
                return _ActivitySource;
            }
        }

        /// <summary>
        /// The logger factory backing the logs pipeline, or null when the logs pillar is disabled or
        /// the host is inert. Prefer <see cref="CreateLogger(string)"/>, which never returns null.
        /// </summary>
        public ILoggerFactory? LoggerFactory
        {
            get
            {
                return _LoggerFactory;
            }
        }

        /// <summary>
        /// The built meter provider, or null when the metrics pillar is disabled or the host is
        /// inert. Exposed for advanced scenarios such as forcing a flush in tests.
        /// </summary>
        public MeterProvider? MeterProvider
        {
            get
            {
                return _MeterProvider;
            }
        }

        /// <summary>
        /// The built tracer provider, or null when the traces pillar is disabled or the host is
        /// inert. Exposed for advanced scenarios such as forcing a flush in tests.
        /// </summary>
        public TracerProvider? TracerProvider
        {
            get
            {
                return _TracerProvider;
            }
        }

        /// <summary>
        /// The convenience emitter over the host's meter and activity source. Cached; the same
        /// instance is returned on every access. Thread safe.
        /// </summary>
        public RadiantClient Client
        {
            get
            {
                RadiantClient? existing = _Client;
                if (existing != null) return existing;

                lock (_ClientLock)
                {
                    if (_Client == null) _Client = new RadiantClient(this, _Catalog);
                    return _Client;
                }
            }
        }

        #endregion

        #region Private-Members

        private static readonly object _PortLock = new object();
        private static readonly HashSet<int> _BoundPorts = new HashSet<int>();

        private readonly RadiantSettings _Settings;
        private readonly string _InstanceId;
        private readonly Meter _Meter;
        private readonly ActivitySource _ActivitySource;
        private readonly MetricCatalog _Catalog;
        private readonly bool _Enabled;
        private readonly DateTime _StartedUtc;
        private readonly List<object> _ProcessInstruments = new List<object>();
        private readonly object _ClientLock = new object();

        private MeterProvider? _MeterProvider;
        private TracerProvider? _TracerProvider;
        private ILoggerFactory? _LoggerFactory;
        private RadiantClient? _Client;
        private int _PrometheusPort = -1;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        private RadiantHost(RadiantSettings settings)
        {
            _Settings = settings;
            _StartedUtc = DateTime.UtcNow;
            _InstanceId = !String.IsNullOrWhiteSpace(settings.ServiceInstanceId)
                ? settings.ServiceInstanceId!
                : Guid.NewGuid().ToString();

            _Meter = new Meter(settings.ServiceName);
            _ActivitySource = new ActivitySource(settings.ServiceName);
            _Enabled = settings.Enable;

            LabelPolicyEnum resolvedPolicy = BuildEnvironment.Resolve(settings.Metrics.LabelPolicy);
            _Catalog = new MetricCatalog(settings.Metrics.Definitions, resolvedPolicy, settings.DiagnosticCallback);
        }

        /// <summary>
        /// Build and start the telemetry pipeline described by <paramref name="settings"/>.
        /// </summary>
        /// <param name="settings">
        /// The pipeline description. Must be non-null and carry a non-empty
        /// <see cref="RadiantSettings.ServiceName"/>.
        /// </param>
        /// <returns>A started host that owns the providers and exporters.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="RadiantSettings.ServiceName"/> is null or empty.
        /// </exception>
        /// <exception cref="RadiantException">
        /// Thrown when the Prometheus scrape port is already bound by another host in this process,
        /// or when a provider or exporter fails to initialize.
        /// </exception>
        public static RadiantHost Start(RadiantSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(settings.ServiceName))
                throw new ArgumentException("RadiantSettings.ServiceName is required and must be non-empty.", nameof(settings));

            RadiantHost host = new RadiantHost(settings);

            if (!host._Enabled) return host;

            host.BuildPipeline();
            return host;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a logger from the host's logs pipeline. When the logs pillar is disabled or the
        /// host is inert, returns a no-op logger rather than null.
        /// </summary>
        /// <param name="categoryName">The logger category. Must be non-null and non-empty.</param>
        /// <returns>A logger; never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="categoryName"/> is null or empty.</exception>
        public ILogger CreateLogger(string categoryName)
        {
            if (String.IsNullOrWhiteSpace(categoryName)) throw new ArgumentNullException(nameof(categoryName));
            if (_LoggerFactory == null) return NullLogger.Instance;
            return _LoggerFactory.CreateLogger(categoryName);
        }

        /// <summary>
        /// Create a logger categorized by type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The category type.</typeparam>
        /// <returns>A logger; never null.</returns>
        public ILogger<T> CreateLogger<T>()
        {
            if (_LoggerFactory == null) return NullLogger<T>.Instance;
            return _LoggerFactory.CreateLogger<T>();
        }

        /// <summary>
        /// Start a span (activity) on the host's activity source.
        /// </summary>
        /// <param name="name">The span name. Must be non-null and non-empty.</param>
        /// <param name="kind">The span kind. Defaults to <see cref="SpanKindEnum.Internal"/>.</param>
        /// <returns>
        /// A <see cref="RadiantSpan"/>. When no listener is sampling, the span is inert but safe to
        /// use and dispose.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public RadiantSpan StartSpan(string name, SpanKindEnum kind = SpanKindEnum.Internal)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            return RadiantSpan.Start(_ActivitySource, name, kind);
        }

        /// <summary>
        /// Force all metric and trace exporters to flush pending data.
        /// </summary>
        /// <param name="timeoutMs">
        /// The flush timeout in milliseconds. Default 10000. Use -1 to wait indefinitely.
        /// </param>
        /// <returns>True when every provider flushed within the timeout.</returns>
        public bool ForceFlush(int timeoutMs = 10000)
        {
            bool ok = true;
            if (_MeterProvider != null) ok &= _MeterProvider.ForceFlush(timeoutMs);
            if (_TracerProvider != null) ok &= _TracerProvider.ForceFlush(timeoutMs);
            return ok;
        }

        /// <summary>
        /// Dispose the host: flush and tear down every provider and exporter and release the
        /// Prometheus scrape port.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously dispose the host. Forces a flush of the metric and trace exporters, then
        /// tears everything down. Provided so callers on an async teardown path can
        /// <c>await using</c> the host; the underlying flush is synchronous.
        /// </summary>
        /// <returns>A completed task once teardown finishes.</returns>
        public ValueTask DisposeAsync()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
            return default(ValueTask);
        }

        #endregion

        #region Private-Methods

        private void BuildPipeline()
        {
            List<string> meterNames = BuildMeterNames();
            List<string> sourceNames = BuildSourceNames();

            ResourceBuilder resource = PipelineFactory.BuildResource(_Settings, _InstanceId);

            if (_Settings.Metrics.Enable && _Settings.Prometheus.Enable)
            {
                ReservePrometheusPort(_Settings.Prometheus.Port);
            }

            try
            {
                _MeterProvider = PipelineFactory.BuildMeterProvider(_Settings, resource, meterNames);
                _TracerProvider = PipelineFactory.BuildTracerProvider(_Settings, resource, sourceNames);
                _LoggerFactory = PipelineFactory.BuildLoggerFactory(_Settings, resource);
            }
            catch (Exception e)
            {
                ReleasePrometheusPort();
                throw new RadiantException(
                    "Radiant failed to build the telemetry pipeline for service '" + _Settings.ServiceName + "'. " +
                    "See the inner exception for the underlying cause (for example a Prometheus scrape port already in use).",
                    e);
            }

            if (_Settings.Metrics.Enable && _Settings.Metrics.IncludeProcess)
            {
                RegisterProcessInstruments();
            }
        }

        private List<string> BuildMeterNames()
        {
            List<string> names = new List<string>();
            names.Add(_Settings.ServiceName);
            foreach (string name in _Settings.Sources.MeterNames)
            {
                if (!String.IsNullOrWhiteSpace(name) && !names.Contains(name)) names.Add(name);
            }
            return names;
        }

        private List<string> BuildSourceNames()
        {
            List<string> names = new List<string>();
            names.Add(_Settings.ServiceName);
            foreach (string name in _Settings.Sources.ActivitySourceNames)
            {
                if (!String.IsNullOrWhiteSpace(name) && !names.Contains(name)) names.Add(name);
            }
            return names;
        }

        private void ReservePrometheusPort(int port)
        {
            lock (_PortLock)
            {
                if (_BoundPorts.Contains(port))
                {
                    throw new RadiantException(
                        "A Radiant host is already serving the Prometheus scrape endpoint on port " + port.ToString() +
                        " in this process. Only one host may bind a given scrape port; disable the Prometheus endpoint " +
                        "on the second host or choose a different port.");
                }

                _BoundPorts.Add(port);
                _PrometheusPort = port;
            }
        }

        private void ReleasePrometheusPort()
        {
            if (_PrometheusPort < 0) return;
            lock (_PortLock)
            {
                _BoundPorts.Remove(_PrometheusPort);
            }
            _PrometheusPort = -1;
        }

        private void RegisterProcessInstruments()
        {
            _ProcessInstruments.Add(_Meter.CreateObservableGauge<long>(
                SemConv.Process.MemoryUsage.Name,
                ObserveWorkingSet,
                SemConv.Process.MemoryUsage.Unit,
                SemConv.Process.MemoryUsage.Description));

            _ProcessInstruments.Add(_Meter.CreateObservableGauge<double>(
                SemConv.Process.Uptime.Name,
                ObserveUptimeSeconds,
                SemConv.Process.Uptime.Unit,
                SemConv.Process.Uptime.Description));

            _ProcessInstruments.Add(_Meter.CreateObservableGauge<int>(
                SemConv.Process.ThreadCount.Name,
                ObserveThreadCount,
                SemConv.Process.ThreadCount.Unit,
                SemConv.Process.ThreadCount.Description));
        }

        private static long ObserveWorkingSet()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                return process.WorkingSet64;
            }
        }

        private double ObserveUptimeSeconds()
        {
            return (DateTime.UtcNow - _StartedUtc).TotalSeconds;
        }

        private static int ObserveThreadCount()
        {
            using (Process process = Process.GetCurrentProcess())
            {
                return process.Threads.Count;
            }
        }

        private void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;

            if (disposing)
            {
                if (_MeterProvider != null)
                {
                    try { _MeterProvider.ForceFlush(5000); } catch (Exception e) { Diagnose(e); }
                    _MeterProvider.Dispose();
                    _MeterProvider = null;
                }

                if (_TracerProvider != null)
                {
                    try { _TracerProvider.ForceFlush(5000); } catch (Exception e) { Diagnose(e); }
                    _TracerProvider.Dispose();
                    _TracerProvider = null;
                }

                if (_LoggerFactory != null)
                {
                    _LoggerFactory.Dispose();
                    _LoggerFactory = null;
                }

                _Meter.Dispose();
                _ActivitySource.Dispose();
                _ProcessInstruments.Clear();

                ReleasePrometheusPort();
            }
        }

        private void Diagnose(Exception e)
        {
            Action<string>? callback = _Settings.DiagnosticCallback;
            if (callback == null) return;
            try { callback("Radiant: exporter flush during dispose raised " + e.GetType().Name + ": " + e.Message); }
            catch { /* a broken diagnostic callback must not break teardown */ }
        }

        #endregion
    }
}
