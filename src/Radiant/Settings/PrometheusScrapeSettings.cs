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
        /// The hostname to bind. Default <c>localhost</c>. Must be non-empty.
        /// <para>
        /// Wildcard binding is not supported. <c>*</c> and <c>+</c> fail at
        /// <see cref="RadiantHost.Start(RadiantSettings)"/> with a <see cref="RadiantException"/>
        /// wrapping a <c>UriFormatException</c>. <c>0.0.0.0</c> and <c>[::]</c> fail with a
        /// <see cref="RadiantException"/> wrapping an <c>HttpListenerException</c>.
        /// </para>
        /// <para>
        /// A name binds the address it resolves to on this machine, and the endpoint answers only requests
        /// that use that name (any other Host gets a 404). To be scraped from another machine or container,
        /// set a name that resolves to a reachable interface (in Docker Compose, the service name), and
        /// point Prometheus at that same name, for example <c>my-service:9464</c>. Use <c>127.0.0.1</c> for
        /// loopback-only scraping by address.
        /// </para>
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
