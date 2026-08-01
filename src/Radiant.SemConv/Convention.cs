namespace Radiant
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An immutable description of a metric instrument: its name, kind, unit, allowed label keys,
    /// optional histogram buckets, and description. This is the open extension point for naming —
    /// the built-in <see cref="SemConv"/> members are ready-made instances of this type, and an
    /// application or library declares its own the same way, through the same factories, and passes
    /// them to the same emit and catalog APIs. There is no separate path for custom instruments.
    /// <para>
    /// A convention carries no behavior; it is data. The host and the convenience emitter read it to
    /// create instruments with the right unit and to enforce label policy. Because it lives in the
    /// emit-side naming package, a <c>netstandard2.0</c> library can share conventions without taking
    /// a dependency on the OpenTelemetry SDK.
    /// </para>
    /// <para>
    /// A convention converts implicitly to its <see cref="Name"/>, so a convention may be passed
    /// anywhere a raw instrument-name string is expected.
    /// </para>
    /// <para>
    /// Instances are immutable and therefore thread safe. The <c>With*</c> methods return new
    /// instances rather than mutating.
    /// </para>
    /// </summary>
    public sealed class Convention
    {
        #region Public-Members

        /// <summary>
        /// The instrument name (for example <c>http.server.request.duration</c>). Never null or
        /// empty.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
        }

        /// <summary>
        /// The instrument kind.
        /// </summary>
        public MetricKindEnum Kind
        {
            get
            {
                return _Kind;
            }
        }

        /// <summary>
        /// The UCUM unit (for example <c>s</c>, <c>By</c>, <c>1</c>), or null when dimensionless.
        /// </summary>
        public string? Unit
        {
            get
            {
                return _Unit;
            }
        }

        /// <summary>
        /// A human-readable description, or null.
        /// </summary>
        public string? Description
        {
            get
            {
                return _Description;
            }
        }

        /// <summary>
        /// The label keys this instrument is allowed to carry. Never null; may be empty. Enforced by
        /// the declared catalog's label policy when the convention is registered.
        /// </summary>
        public IReadOnlyList<string> LabelKeys
        {
            get
            {
                return _LabelKeys;
            }
        }

        /// <summary>
        /// Explicit histogram bucket boundaries, applied only when <see cref="Kind"/> is
        /// <see cref="MetricKindEnum.Histogram"/>. Null uses the SDK default boundaries. A defensive
        /// copy is returned.
        /// </summary>
        public double[]? Buckets
        {
            get
            {
                if (_Buckets == null) return null;
                double[] copy = new double[_Buckets.Length];
                Array.Copy(_Buckets, copy, _Buckets.Length);
                return copy;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Name;
        private readonly MetricKindEnum _Kind;
        private readonly string? _Unit;
        private readonly string? _Description;
        private readonly string[] _LabelKeys;
        private readonly double[]? _Buckets;

        #endregion

        #region Constructors-and-Factories

        private Convention(string name, MetricKindEnum kind, string? unit, string? description, string[]? labelKeys, double[]? buckets)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));

            _Name = name;
            _Kind = kind;
            _Unit = unit;
            _Description = description;
            _LabelKeys = labelKeys ?? Array.Empty<string>();
            _Buckets = buckets;
        }

        /// <summary>
        /// Create a monotonic counter convention.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null.</param>
        /// <param name="labelKeys">The allowed label keys.</param>
        /// <returns>A counter convention.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public static Convention Counter(string name, string? unit = null, params string[] labelKeys)
        {
            return new Convention(name, MetricKindEnum.Counter, unit, null, labelKeys, null);
        }

        /// <summary>
        /// Create an up/down counter convention.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null.</param>
        /// <param name="labelKeys">The allowed label keys.</param>
        /// <returns>An up/down counter convention.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public static Convention UpDownCounter(string name, string? unit = null, params string[] labelKeys)
        {
            return new Convention(name, MetricKindEnum.UpDownCounter, unit, null, labelKeys, null);
        }

        /// <summary>
        /// Create a histogram convention.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null.</param>
        /// <param name="buckets">Explicit bucket boundaries, or null for the SDK default. The Radiant core's <c>LatencyBuckets</c> presets are ready-made sets.</param>
        /// <param name="labelKeys">The allowed label keys.</param>
        /// <returns>A histogram convention.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public static Convention Histogram(string name, string? unit = null, double[]? buckets = null, params string[] labelKeys)
        {
            return new Convention(name, MetricKindEnum.Histogram, unit, null, labelKeys, buckets);
        }

        /// <summary>
        /// Create an observable gauge convention, read from state at collection time.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null.</param>
        /// <param name="labelKeys">The allowed label keys.</param>
        /// <returns>An observable gauge convention.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public static Convention Gauge(string name, string? unit = null, params string[] labelKeys)
        {
            return new Convention(name, MetricKindEnum.ObservableGauge, unit, null, labelKeys, null);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Return a copy of this convention with the given description.
        /// </summary>
        /// <param name="description">The description text.</param>
        /// <returns>A new convention.</returns>
        public Convention WithDescription(string description)
        {
            return new Convention(_Name, _Kind, _Unit, description, _LabelKeys, _Buckets);
        }

        /// <summary>
        /// Return a copy of this convention with the given allowed label keys, replacing any current
        /// set.
        /// </summary>
        /// <param name="labelKeys">The allowed label keys.</param>
        /// <returns>A new convention.</returns>
        public Convention WithLabels(params string[] labelKeys)
        {
            return new Convention(_Name, _Kind, _Unit, _Description, labelKeys, _Buckets);
        }

        /// <summary>
        /// Return a copy of this convention with the given histogram bucket boundaries.
        /// </summary>
        /// <param name="buckets">The bucket boundaries.</param>
        /// <returns>A new convention.</returns>
        public Convention WithBuckets(params double[] buckets)
        {
            return new Convention(_Name, _Kind, _Unit, _Description, _LabelKeys, buckets);
        }

        /// <summary>
        /// Return the instrument name.
        /// </summary>
        /// <returns>The value of <see cref="Name"/>.</returns>
        public override string ToString()
        {
            return _Name;
        }

        /// <summary>
        /// Convert a convention to its instrument name so it may be used anywhere a raw name string
        /// is expected.
        /// </summary>
        /// <param name="convention">The convention. May be null, in which case null is returned.</param>
        public static implicit operator string?(Convention? convention)
        {
            return convention?.Name;
        }

        #endregion
    }
}
