namespace Radiant
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One entry in the optional declared catalog (Tier 2). Names an instrument, its kind, unit,
    /// optional histogram buckets, and the label keys it is allowed to carry. When a catalog is
    /// present the host enforces the configured <see cref="MetricsSettings.LabelPolicy"/> against
    /// these declarations. The catalog is off by default; the plain BCL emit path never requires
    /// it.
    /// <para>
    /// This type is not thread safe. Build it during composition.
    /// </para>
    /// </summary>
    public class MetricDefinition
    {
        #region Public-Members

        /// <summary>
        /// The instrument name (for example <c>http.server.request.duration</c>). Never null or
        /// empty. Prefer OpenTelemetry semantic-convention names from
        /// <see cref="SemConv"/> so the instrument renders in stock dashboards.
        /// </summary>
        public string Key
        {
            get
            {
                return _Key;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Key));
                _Key = value;
            }
        }

        /// <summary>
        /// The instrument kind. Defaults to <see cref="MetricKindEnum.Counter"/>.
        /// </summary>
        public MetricKindEnum Kind { get; set; } = MetricKindEnum.Counter;

        /// <summary>
        /// The UCUM unit (for example <c>s</c>, <c>By</c>, <c>1</c>). May be null when the
        /// instrument is dimensionless and no unit is desired.
        /// </summary>
        public string? Unit { get; set; } = null;

        /// <summary>
        /// Explicit histogram bucket boundaries, applied only when
        /// <see cref="Kind"/> is <see cref="MetricKindEnum.Histogram"/>. Null uses the SDK default
        /// boundaries; see <see cref="LatencyBuckets"/> for ready-made sets. Values should be
        /// ascending.
        /// </summary>
        public double[]? Buckets { get; set; } = null;

        /// <summary>
        /// The label keys this instrument is allowed to carry. Never null. An empty list means the
        /// instrument carries no labels; any label then violates a strict policy.
        /// </summary>
        public List<string> LabelKeys
        {
            get
            {
                return _LabelKeys;
            }
            set
            {
                _LabelKeys = value ?? new List<string>();
            }
        }

        /// <summary>
        /// A human-readable description of the instrument. May be null.
        /// </summary>
        public string? Description { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Key = String.Empty;
        private List<string> _LabelKeys = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create an empty metric definition. Populate <see cref="Key"/> before use.
        /// </summary>
        public MetricDefinition()
        {
        }

        /// <summary>
        /// Create a metric definition.
        /// </summary>
        /// <param name="key">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="kind">The instrument kind.</param>
        /// <param name="unit">The UCUM unit, or null.</param>
        /// <param name="labelKeys">The allowed label keys, or null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
        public MetricDefinition(string key, MetricKindEnum kind, string? unit = null, IEnumerable<string>? labelKeys = null)
        {
            if (String.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            _Key = key;
            Kind = kind;
            Unit = unit;
            if (labelKeys != null) _LabelKeys = new List<string>(labelKeys);
        }

        #endregion
    }
}
