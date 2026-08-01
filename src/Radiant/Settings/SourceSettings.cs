namespace Radiant
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The <see cref="System.Diagnostics.Metrics.Meter"/> and
    /// <see cref="System.Diagnostics.ActivitySource"/> names the host subscribes to. The host and
    /// the libraries that emit never reference each other directly — they meet at these string
    /// names. A name added here is picked up whether it belongs to your own code or a third-party
    /// library.
    /// <para>
    /// This type is not thread safe. Populate it during composition, before
    /// <see cref="RadiantHost.Start(RadiantSettings)"/>, and do not mutate it afterward.
    /// </para>
    /// </summary>
    public class SourceSettings
    {
        #region Public-Members

        /// <summary>
        /// Meter names to subscribe to for metrics. Never null. Wildcards are supported by the
        /// underlying SDK (for example <c>"MyCompany.*"</c>).
        /// </summary>
        public List<string> MeterNames
        {
            get
            {
                return _MeterNames;
            }
            set
            {
                _MeterNames = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Activity source names to subscribe to for traces. Never null.
        /// </summary>
        public List<string> ActivitySourceNames
        {
            get
            {
                return _ActivitySourceNames;
            }
            set
            {
                _ActivitySourceNames = value ?? new List<string>();
            }
        }

        #endregion

        #region Private-Members

        private List<string> _MeterNames = new List<string>();
        private List<string> _ActivitySourceNames = new List<string>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Subscribe to a meter by name. Duplicate and empty names are ignored.
        /// </summary>
        /// <param name="name">The meter name (for example <c>"Watson.Webserver"</c>).</param>
        /// <returns>This instance, for chaining.</returns>
        public SourceSettings AddMeter(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return this;
            if (!_MeterNames.Contains(name)) _MeterNames.Add(name);
            return this;
        }

        /// <summary>
        /// Subscribe to an activity source by name. Duplicate and empty names are ignored.
        /// </summary>
        /// <param name="name">The activity source name (for example <c>"MyApp"</c>).</param>
        /// <returns>This instance, for chaining.</returns>
        public SourceSettings AddActivitySource(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return this;
            if (!_ActivitySourceNames.Contains(name)) _ActivitySourceNames.Add(name);
            return this;
        }

        #endregion
    }
}
