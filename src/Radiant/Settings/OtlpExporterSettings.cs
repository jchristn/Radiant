namespace Radiant
{
    using System;
    using System.Collections.Generic;
    using Radiant.Internal;

    /// <summary>
    /// OTLP push exporter settings — the proven path to an OpenTelemetry Collector. The merged,
    /// de-diverged version of the settings the fleet hand-wrote before Radiant.
    /// <para>
    /// This type is not thread safe. Configure it before <see cref="RadiantHost.Start(RadiantSettings)"/>.
    /// </para>
    /// </summary>
    public class OtlpExporterSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the OTLP push exporter is enabled. Default true. When false, telemetry is not
        /// pushed to a collector; the in-process Prometheus endpoint (if enabled) still serves
        /// metrics.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The collector endpoint. Default <c>http://localhost:4317</c> (the gRPC port). Use
        /// <c>http://localhost:4318</c> for <see cref="OtlpProtocolEnum.HttpProtobuf"/>. Must be a
        /// non-empty, absolute URI.
        /// </summary>
        public string Endpoint
        {
            get
            {
                return _Endpoint;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Endpoint));
                if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) || parsed == null)
                    throw new ArgumentException("Endpoint must be an absolute URI.", nameof(Endpoint));
                _Endpoint = value;
            }
        }

        /// <summary>
        /// The OTLP wire protocol. Default <see cref="OtlpProtocolEnum.Grpc"/>. An unrecognized
        /// value fails fast at host start rather than silently falling back.
        /// </summary>
        public OtlpProtocolEnum Protocol { get; set; } = OtlpProtocolEnum.Grpc;

        /// <summary>
        /// The per-export timeout in milliseconds. Default 10000. Minimum 1000, maximum 120000.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                _TimeoutMs = RadiantMath.Clamp(value, 1000, 120000);
            }
        }

        /// <summary>
        /// Optional headers sent with every export (for example an auth token for a hosted
        /// collector). Never null. Serialized to the OTLP <c>key1=value1,key2=value2</c> header
        /// format at host start.
        /// </summary>
        public Dictionary<string, string> Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                _Headers = value ?? new Dictionary<string, string>();
            }
        }

        #endregion

        #region Private-Members

        private string _Endpoint = "http://localhost:4317";
        private int _TimeoutMs = 10000;
        private Dictionary<string, string> _Headers = new Dictionary<string, string>();

        #endregion
    }
}
