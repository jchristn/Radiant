namespace Radiant
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Radiant.Internal;

    /// <summary>
    /// An optional convenience emitter over the host's <see cref="Meter"/>. It hands back cached
    /// base-class-library instrument handles so a call site records against a handle rather than a
    /// re-typed string, and offers record helpers that apply the declared catalog's label policy.
    /// <para>
    /// This type wraps, but does not replace, the BCL emit path. Application and library code are
    /// free to create their own <see cref="Meter"/> instruments and skip this entirely; the host
    /// still collects them by name.
    /// </para>
    /// <para>
    /// Thread safe. Instrument handles are cached and shared; creating or recording from many
    /// threads is safe.
    /// </para>
    /// </summary>
    public sealed class RadiantClient
    {
        #region Private-Members

        private readonly Meter _Meter;
        private readonly MetricCatalog _Catalog;
        private readonly ConcurrentDictionary<string, Counter<double>> _Counters = new ConcurrentDictionary<string, Counter<double>>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Histogram<double>> _Histograms = new ConcurrentDictionary<string, Histogram<double>>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, UpDownCounter<double>> _UpDownCounters = new ConcurrentDictionary<string, UpDownCounter<double>>(StringComparer.Ordinal);
        private readonly List<object> _ObservableInstruments = new List<object>();
        private readonly object _ObservableLock = new object();

        #endregion

        #region Constructors-and-Factories

        internal RadiantClient(RadiantHost host, MetricCatalog catalog)
        {
            _Meter = host.Meter;
            _Catalog = catalog;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get or create a monotonic counter handle. Repeated calls with the same name return the
        /// same cached handle. When the name is declared in the catalog, the declared unit and
        /// description fill in for any not passed here.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null to use the catalog's or none.</param>
        /// <param name="description">A description, or null to use the catalog's or none.</param>
        /// <returns>A cached <see cref="Counter{T}"/> of <see cref="double"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public Counter<double> Counter(string name, string? unit = null, string? description = null)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            return _Counters.GetOrAdd(name, key => _Meter.CreateCounter<double>(key, ResolveUnit(key, unit), ResolveDescription(key, description)));
        }

        /// <summary>
        /// Get or create a histogram handle. Repeated calls with the same name return the same
        /// cached handle. Bucket boundaries come from the catalog definition (if any) via a view
        /// configured at host start, not from this call.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null to use the catalog's or none.</param>
        /// <param name="description">A description, or null to use the catalog's or none.</param>
        /// <returns>A cached <see cref="Histogram{T}"/> of <see cref="double"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public Histogram<double> Histogram(string name, string? unit = null, string? description = null)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            return _Histograms.GetOrAdd(name, key => _Meter.CreateHistogram<double>(key, ResolveUnit(key, unit), ResolveDescription(key, description)));
        }

        /// <summary>
        /// Get or create an up/down counter handle. Repeated calls with the same name return the
        /// same cached handle.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="unit">The UCUM unit, or null to use the catalog's or none.</param>
        /// <param name="description">A description, or null to use the catalog's or none.</param>
        /// <returns>A cached <see cref="UpDownCounter{T}"/> of <see cref="double"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public UpDownCounter<double> UpDownCounter(string name, string? unit = null, string? description = null)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            return _UpDownCounters.GetOrAdd(name, key => _Meter.CreateUpDownCounter<double>(key, ResolveUnit(key, unit), ResolveDescription(key, description)));
        }

        /// <summary>
        /// Increment a counter by <paramref name="value"/>, applying the catalog label policy to the
        /// supplied tags.
        /// </summary>
        /// <param name="name">The counter name. Must be non-null and non-empty.</param>
        /// <param name="value">The increment. Should be non-negative for a counter.</param>
        /// <param name="tags">The measurement labels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a tag carries a label key not declared for <paramref name="name"/> and the
        /// resolved label policy is strict.
        /// </exception>
        public void Increment(string name, double value = 1.0, params RadiantTag[] tags)
        {
            Counter<double> counter = Counter(name);
            counter.Add(value, _Catalog.ApplyPolicy(name, tags));
        }

        /// <summary>
        /// Record a value into a histogram, applying the catalog label policy to the supplied tags.
        /// </summary>
        /// <param name="name">The histogram name. Must be non-null and non-empty.</param>
        /// <param name="value">The value to record.</param>
        /// <param name="tags">The measurement labels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a tag carries a label key not declared for <paramref name="name"/> and the
        /// resolved label policy is strict.
        /// </exception>
        public void Record(string name, double value, params RadiantTag[] tags)
        {
            Histogram<double> histogram = Histogram(name);
            histogram.Record(value, _Catalog.ApplyPolicy(name, tags));
        }

        /// <summary>
        /// Add a delta (which may be negative) to an up/down counter, applying the catalog label
        /// policy to the supplied tags.
        /// </summary>
        /// <param name="name">The up/down counter name. Must be non-null and non-empty.</param>
        /// <param name="value">The delta to add.</param>
        /// <param name="tags">The measurement labels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a tag carries a label key not declared for <paramref name="name"/> and the
        /// resolved label policy is strict.
        /// </exception>
        public void Add(string name, double value, params RadiantTag[] tags)
        {
            UpDownCounter<double> counter = UpDownCounter(name);
            counter.Add(value, _Catalog.ApplyPolicy(name, tags));
        }

        /// <summary>
        /// Register an observable gauge read from application state at collection time. The value
        /// callback runs on the collection thread; keep it fast and non-throwing.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="observe">The value provider. Must be non-null.</param>
        /// <param name="unit">The UCUM unit, or null to use the catalog's or none.</param>
        /// <param name="tags">Static labels attached to every observation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="observe"/> is null.</exception>
        public void RegisterGauge(string name, Func<double> observe, string? unit = null, params RadiantTag[] tags)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (observe == null) throw new ArgumentNullException(nameof(observe));

            KeyValuePair<string, object?>[] pairs = _Catalog.ApplyPolicy(name, tags);

            object instrument = _Meter.CreateObservableGauge<double>(
                name,
                () => new Measurement<double>(observe(), pairs),
                ResolveUnit(name, unit),
                ResolveDescription(name, null));

            KeepAlive(instrument);
        }

        /// <summary>
        /// Register an observable gauge that emits one measurement per entity at collection time —
        /// the per-entity fleet form (for example queue depth by queue name). The callback runs on
        /// the collection thread; keep it fast and non-throwing.
        /// </summary>
        /// <param name="name">The instrument name. Must be non-null and non-empty.</param>
        /// <param name="observe">
        /// A provider returning one <see cref="Measurement{T}"/> per entity, each with its own tags.
        /// Must be non-null.
        /// </param>
        /// <param name="unit">The UCUM unit, or null to use the catalog's or none.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="observe"/> is null.</exception>
        public void RegisterGauge(string name, Func<IEnumerable<Measurement<double>>> observe, string? unit = null)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (observe == null) throw new ArgumentNullException(nameof(observe));

            object instrument = _Meter.CreateObservableGauge<double>(
                name,
                observe,
                ResolveUnit(name, unit),
                ResolveDescription(name, null));

            KeepAlive(instrument);
        }

        /// <summary>
        /// Get or create a counter handle from a <see cref="Convention"/>, applying its unit and
        /// description.
        /// </summary>
        /// <param name="convention">The convention. Must be non-null.</param>
        /// <returns>A cached <see cref="Counter{T}"/> of <see cref="double"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        public Counter<double> Counter(Convention convention)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            return Counter(convention.Name, convention.Unit, convention.Description);
        }

        /// <summary>
        /// Get or create a histogram handle from a <see cref="Convention"/>, applying its unit and
        /// description. Bucket boundaries come from the catalog view when the convention was
        /// registered via <see cref="MetricsSettings.Define(Convention)"/>.
        /// </summary>
        /// <param name="convention">The convention. Must be non-null.</param>
        /// <returns>A cached <see cref="Histogram{T}"/> of <see cref="double"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        public Histogram<double> Histogram(Convention convention)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            return Histogram(convention.Name, convention.Unit, convention.Description);
        }

        /// <summary>
        /// Get or create an up/down counter handle from a <see cref="Convention"/>, applying its
        /// unit and description.
        /// </summary>
        /// <param name="convention">The convention. Must be non-null.</param>
        /// <returns>A cached <see cref="UpDownCounter{T}"/> of <see cref="double"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        public UpDownCounter<double> UpDownCounter(Convention convention)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            return UpDownCounter(convention.Name, convention.Unit, convention.Description);
        }

        /// <summary>
        /// Increment a counter described by a <see cref="Convention"/>, applying the catalog label
        /// policy. The built-in and custom conventions use this identical call.
        /// </summary>
        /// <param name="convention">The counter convention. Must be non-null.</param>
        /// <param name="value">The increment. Should be non-negative for a counter.</param>
        /// <param name="tags">The measurement labels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a tag carries an undeclared label key and the resolved label policy is strict.
        /// </exception>
        public void Increment(Convention convention, double value = 1.0, params RadiantTag[] tags)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            Counter<double> counter = Counter(convention.Name, convention.Unit, convention.Description);
            counter.Add(value, _Catalog.ApplyPolicy(convention.Name, tags));
        }

        /// <summary>
        /// Record a value into a histogram described by a <see cref="Convention"/>, applying the
        /// catalog label policy.
        /// </summary>
        /// <param name="convention">The histogram convention. Must be non-null.</param>
        /// <param name="value">The value to record.</param>
        /// <param name="tags">The measurement labels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a tag carries an undeclared label key and the resolved label policy is strict.
        /// </exception>
        public void Record(Convention convention, double value, params RadiantTag[] tags)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            Histogram<double> histogram = Histogram(convention.Name, convention.Unit, convention.Description);
            histogram.Record(value, _Catalog.ApplyPolicy(convention.Name, tags));
        }

        /// <summary>
        /// Add a delta (which may be negative) to an up/down counter described by a
        /// <see cref="Convention"/>, applying the catalog label policy.
        /// </summary>
        /// <param name="convention">The up/down counter convention. Must be non-null.</param>
        /// <param name="value">The delta to add.</param>
        /// <param name="tags">The measurement labels.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when a tag carries an undeclared label key and the resolved label policy is strict.
        /// </exception>
        public void Add(Convention convention, double value, params RadiantTag[] tags)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            UpDownCounter<double> counter = UpDownCounter(convention.Name, convention.Unit, convention.Description);
            counter.Add(value, _Catalog.ApplyPolicy(convention.Name, tags));
        }

        /// <summary>
        /// Register an observable gauge described by a <see cref="Convention"/>, read from state at
        /// collection time.
        /// </summary>
        /// <param name="convention">The gauge convention. Must be non-null.</param>
        /// <param name="observe">The value provider. Must be non-null.</param>
        /// <param name="tags">Static labels attached to every observation.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="convention"/> or <paramref name="observe"/> is null.</exception>
        public void RegisterGauge(Convention convention, Func<double> observe, params RadiantTag[] tags)
        {
            if (convention == null) throw new ArgumentNullException(nameof(convention));
            if (observe == null) throw new ArgumentNullException(nameof(observe));

            KeyValuePair<string, object?>[] pairs = _Catalog.ApplyPolicy(convention.Name, tags);

            object instrument = _Meter.CreateObservableGauge<double>(
                convention.Name,
                () => new Measurement<double>(observe(), pairs),
                convention.Unit,
                convention.Description);

            KeepAlive(instrument);
        }

        #endregion

        #region Private-Methods

        private string? ResolveUnit(string name, string? explicitUnit)
        {
            if (!String.IsNullOrEmpty(explicitUnit)) return explicitUnit;
            MetricDefinition? definition;
            if (_Catalog.TryGet(name, out definition) && definition != null) return definition.Unit;
            return null;
        }

        private string? ResolveDescription(string name, string? explicitDescription)
        {
            if (!String.IsNullOrEmpty(explicitDescription)) return explicitDescription;
            MetricDefinition? definition;
            if (_Catalog.TryGet(name, out definition) && definition != null) return definition.Description;
            return null;
        }

        private void KeepAlive(object instrument)
        {
            lock (_ObservableLock)
            {
                _ObservableInstruments.Add(instrument);
            }
        }

        #endregion
    }
}
