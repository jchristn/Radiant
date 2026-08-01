namespace Radiant
{
    using System;
    using System.Collections.Generic;
    using Radiant.Internal;

    /// <summary>
    /// Metrics pillar settings: export cadence, runtime instrumentation, the label policy, and the
    /// optional declared catalog.
    /// <para>
    /// This type is not thread safe. Configure it before <see cref="RadiantHost.Start(RadiantSettings)"/>.
    /// </para>
    /// </summary>
    public class MetricsSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the metrics pillar is enabled. Default true. When false, no meter provider is
        /// built and no metric exporter runs.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The metric export interval in milliseconds for the periodic OTLP reader. Default 15000.
        /// Minimum 1000, maximum 300000. Lower values push more frequently at higher overhead.
        /// The in-process Prometheus scrape endpoint is pull-based and ignores this value.
        /// </summary>
        public int ExportIntervalMs
        {
            get
            {
                return _ExportIntervalMs;
            }
            set
            {
                _ExportIntervalMs = RadiantMath.Clamp(value, 1000, 300000);
            }
        }

        /// <summary>
        /// Whether to include .NET runtime instrumentation (GC, heap, threads, JIT) via
        /// <c>OpenTelemetry.Instrumentation.Runtime</c>. Default true.
        /// </summary>
        public bool IncludeRuntime { get; set; } = true;

        /// <summary>
        /// Whether to include baseline process metrics (working set, uptime) emitted by Radiant's
        /// own meter. Default true.
        /// </summary>
        public bool IncludeProcess { get; set; } = true;

        /// <summary>
        /// The policy applied when a measurement carries a label key not declared in the catalog.
        /// Default <see cref="LabelPolicyEnum.Auto"/> (strict in Debug, lenient in Release).
        /// Applies only when <see cref="Definitions"/> is non-empty.
        /// </summary>
        public LabelPolicyEnum LabelPolicy { get; set; } = LabelPolicyEnum.Auto;

        /// <summary>
        /// The optional declared catalog (Tier 2). Never null. Leave empty to use the plain BCL
        /// emit path with no cardinality guardrail.
        /// </summary>
        public List<MetricDefinition> Definitions
        {
            get
            {
                return _Definitions;
            }
            set
            {
                _Definitions = value ?? new List<MetricDefinition>();
            }
        }

        #endregion

        #region Private-Members

        private int _ExportIntervalMs = 15000;
        private List<MetricDefinition> _Definitions = new List<MetricDefinition>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Declare an instrument in the catalog. Enables the label-policy guardrail for that
        /// instrument.
        /// </summary>
        /// <param name="definition">The definition to add. Must be non-null.</param>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="definition"/> is null.</exception>
        public MetricsSettings Define(MetricDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            _Definitions.Add(definition);
            return this;
        }

        /// <summary>
        /// Declare an instrument in the catalog by its parts.
        /// </summary>
        /// <param name="key">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="kind">The instrument kind.</param>
        /// <param name="unit">The UCUM unit, or null.</param>
        /// <param name="labelKeys">The allowed label keys, or null for none.</param>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
        public MetricsSettings Define(string key, MetricKindEnum kind, string? unit = null, IEnumerable<string>? labelKeys = null)
        {
            _Definitions.Add(new MetricDefinition(key, kind, unit, labelKeys));
            return this;
        }

        /// <summary>
        /// Declare an instrument in the catalog from a <see cref="Convention"/> — its name, kind,
        /// unit, allowed labels, buckets, and description. The same call registers a built-in
        /// convention such as <see cref="SemConv.Http.RequestDuration"/> or one you authored.
        /// </summary>
        /// <param name="convention">The convention to register. Must be non-null.</param>
        /// <returns>This instance, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        public MetricsSettings Define(Convention convention)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));

            MetricDefinition definition = new MetricDefinition(convention.Name, convention.Kind, convention.Unit, convention.LabelKeys);
            definition.Buckets = convention.Buckets;
            definition.Description = convention.Description;
            _Definitions.Add(definition);
            return this;
        }

        /// <summary>
        /// Declare several conventions in the catalog in one call.
        /// </summary>
        /// <param name="conventions">The conventions to register. Null entries are ignored.</param>
        /// <returns>This instance, for chaining.</returns>
        public MetricsSettings DefineAll(params Convention[] conventions)
        {
            if (conventions == null) return this;
            foreach (Convention convention in conventions)
            {
                if (convention != null) Define(convention);
            }
            return this;
        }

        /// <summary>
        /// Declare several conventions in the catalog in one call.
        /// </summary>
        /// <param name="conventions">The conventions to register. Null entries are ignored.</param>
        /// <returns>This instance, for chaining.</returns>
        public MetricsSettings DefineAll(IEnumerable<Convention> conventions)
        {
            if (conventions == null) return this;
            foreach (Convention convention in conventions)
            {
                if (convention != null) Define(convention);
            }
            return this;
        }

        #endregion
    }
}
