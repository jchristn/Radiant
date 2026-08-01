namespace Radiant
{
    using Radiant.Internal;

    /// <summary>
    /// Logs pillar settings for the <see cref="Microsoft.Extensions.Logging"/>-based pipeline.
    /// Severity is expressed on a compact 0..7 scale where higher means more severe, so it reads
    /// like the fleet's syslog conventions; the host maps it onto the .NET
    /// <see cref="Microsoft.Extensions.Logging.LogLevel"/> scale internally.
    /// <para>
    /// This type is not thread safe. Configure it before <see cref="RadiantHost.Start(RadiantSettings)"/>.
    /// </para>
    /// </summary>
    public class LogsSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the logs pillar is enabled. Default true. When false, no OpenTelemetry logger
        /// provider is attached (application logging via other providers is unaffected).
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The minimum severity to emit, in the range 0..7 where 0 is the most verbose (Trace) and
        /// 7 is the most severe (maps to "none — drop everything"). Default 1, which keeps Debug
        /// and above while dropping Trace. Records below this severity are dropped before export.
        /// The mapping onto <see cref="Microsoft.Extensions.Logging.LogLevel"/> is: 0 → Trace,
        /// 1 → Debug, 2 → Information, 3 → Information, 4 → Warning, 5 → Error, 6 → Critical,
        /// 7 → None.
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

        /// <summary>
        /// Whether to stamp the active <c>trace_id</c> and <c>span_id</c> onto log records so logs
        /// correlate with traces. Default true.
        /// </summary>
        public bool IncludeTraceCorrelation { get; set; } = true;

        /// <summary>
        /// Whether to include the formatted log message text on exported records. Default true.
        /// </summary>
        public bool IncludeFormattedMessage { get; set; } = true;

        /// <summary>
        /// Whether to include logging scopes on exported records. Default true.
        /// </summary>
        public bool IncludeScopes { get; set; } = true;

        #endregion

        #region Private-Members

        private int _MinimumSeverity = 1;

        #endregion
    }
}
