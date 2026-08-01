namespace Radiant
{
    using System;
    using Radiant.Internal;

    /// <summary>
    /// In-process Prometheus scrape endpoint settings. When enabled the host binds an
    /// <c>HttpListener</c> serving the configured path so Prometheus can scrape the app directly,
    /// making metrics useful without a collector deployed. Off by default.
    /// <para>
    /// This type is not thread safe. Configure it before <see cref="RadiantHost.Start(RadiantSettings)"/>.
    /// </para>
    /// </summary>
    public class PrometheusScrapeSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the in-process scrape endpoint is enabled. Default false.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// The hostname to bind. Default <c>localhost</c>. Use <c>+</c> or <c>*</c> to bind all
        /// interfaces (requires an HTTP namespace reservation on Windows). Must be non-empty.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// The TCP port to bind. Default 9464 (the OpenTelemetry Prometheus convention). Minimum 1,
        /// maximum 65535.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = RadiantMath.Clamp(value, 1, 65535);
            }
        }

        /// <summary>
        /// The scrape path. Default <c>/metrics</c>. Must be non-empty and begin with <c>/</c>.
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Path));
                _Path = value.StartsWith("/", StringComparison.Ordinal) ? value : "/" + value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 9464;
        private string _Path = "/metrics";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the full scrape URL (<c>http://host:port/path</c>) a Prometheus server should be
        /// pointed at.
        /// </summary>
        /// <returns>The absolute scrape URL.</returns>
        public string ToScrapeUrl()
        {
            return "http://" + _Hostname + ":" + _Port.ToString() + _Path;
        }

        #endregion
    }
}
