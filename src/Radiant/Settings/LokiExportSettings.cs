namespace Radiant
{
    using System;
    using Radiant.Internal;

    /// <summary>
    /// Direct Loki export settings. When enabled the log pipeline pushes records over OTLP-HTTP to
    /// a Loki 3.x OTLP endpoint, so logs leave the process without a collector in the path. Off by
    /// default; when disabled logs still flow to the OTLP collector configured on
    /// <see cref="OtlpExporterSettings"/> if the logs pillar is enabled.
    /// <para>
    /// This type is not thread safe. Configure it before <see cref="RadiantHost.Start(RadiantSettings)"/>.
    /// </para>
    /// </summary>
    public class LokiExportSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether direct Loki export is enabled. Default false.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// The Loki OTLP base endpoint. Default <c>http://localhost:3100/otlp</c>. The exporter
        /// appends the <c>/v1/logs</c> signal path. Must be a non-empty, absolute URI.
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
        /// The Loki tenant identifier sent as the <c>X-Scope-OrgID</c> header. May be null for a
        /// single-tenant Loki.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// The minimum severity (0..7, same scale as <see cref="LogsSettings.MinimumSeverity"/>) to
        /// forward to Loki. Default 1. When both Loki and the OTLP collector receive logs, the
        /// shared pipeline is filtered at the more verbose of the two thresholds so neither
        /// exporter is starved.
        /// </summary>
        public int MinimumSeverity
        {
            get
            {
                return _MinimumSeverity;
            }
            set
            {
                _MinimumSeverity = RadiantMath.Clamp(value, 0, 7);
            }
        }

        #endregion

        #region Private-Members

        private string _Endpoint = "http://localhost:3100/otlp";
        private int _MinimumSeverity = 1;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the full OTLP-HTTP logs endpoint (<see cref="Endpoint"/> plus the
        /// <c>/v1/logs</c> signal path).
        /// </summary>
        /// <returns>The absolute logs endpoint URI.</returns>
        public string ToLogsEndpoint()
        {
            string trimmed = _Endpoint.TrimEnd('/');
            return trimmed + "/v1/logs";
        }

        #endregion
    }
}
