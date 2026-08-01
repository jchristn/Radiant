namespace Radiant
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single label (dimension) attached to a measurement or span: a key and its value. Used in
    /// <c>params</c> position on the convenience emitter so call sites read
    /// <c>new RadiantTag("protocol", "http")</c> without resorting to <c>ValueTuple</c>, which the
    /// code standard prohibits.
    /// <para>
    /// This is an immutable value type. It is inherently thread safe.
    /// </para>
    /// </summary>
    public readonly struct RadiantTag : IEquatable<RadiantTag>
    {
        #region Public-Members

        /// <summary>
        /// The label key. Never null or empty. Follow OpenTelemetry attribute naming
        /// (dotted, lowercase) so the dimension renders in stock dashboards.
        /// </summary>
        public string Key
        {
            get
            {
                return _Key;
            }
        }

        /// <summary>
        /// The label value. May be null; a null value is exported as an absent attribute by the
        /// underlying SDK. Prefer low-cardinality values (enumerable states, not identifiers) to
        /// avoid unbounded time-series growth.
        /// </summary>
        public object? Value
        {
            get
            {
                return _Value;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Key;
        private readonly object? _Value;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a tag.
        /// </summary>
        /// <param name="key">The label key. Must be non-null and non-empty.</param>
        /// <param name="value">The label value. May be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
        public RadiantTag(string key, object? value)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            _Key = key;
            _Value = value;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Convert this tag to the <see cref="KeyValuePair{TKey, TValue}"/> shape the underlying
        /// <see cref="System.Diagnostics.Metrics"/> and <see cref="System.Diagnostics.Activity"/>
        /// APIs consume.
        /// </summary>
        /// <returns>A key/value pair equivalent to this tag.</returns>
        public KeyValuePair<string, object?> ToKeyValuePair()
        {
            return new KeyValuePair<string, object?>(_Key, _Value);
        }

        /// <summary>
        /// Determine whether this tag equals another by key and value.
        /// </summary>
        /// <param name="other">The other tag.</param>
        /// <returns>True when both key and value are equal.</returns>
        public bool Equals(RadiantTag other)
        {
            return String.Equals(_Key, other._Key, StringComparison.Ordinal)
                && Equals(_Value, other._Value);
        }

        /// <summary>
        /// Determine whether this tag equals another object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True when <paramref name="obj"/> is a <see cref="RadiantTag"/> that is equal.</returns>
        public override bool Equals(object? obj)
        {
            return obj is RadiantTag other && Equals(other);
        }

        /// <summary>
        /// Get a hash code derived from the key and value.
        /// </summary>
        /// <returns>A hash code for this tag.</returns>
        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 31) + (_Key != null ? _Key.GetHashCode() : 0);
            hash = (hash * 31) + (_Value != null ? _Value.GetHashCode() : 0);
            return hash;
        }

        #endregion
    }
}
