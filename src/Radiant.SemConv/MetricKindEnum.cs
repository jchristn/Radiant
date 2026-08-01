namespace Radiant
{
    /// <summary>
    /// The kind of a metric instrument. Determines how the value is aggregated and exported, and
    /// maps onto the corresponding <see cref="System.Diagnostics.Metrics"/> instrument type when an
    /// instrument is materialized. Lives in the emit-side naming package so a <see cref="Convention"/>
    /// can describe an instrument's kind without depending on the OpenTelemetry SDK.
    /// </summary>
    public enum MetricKindEnum
    {
        /// <summary>
        /// A monotonically increasing sum reported by the application (for example a request
        /// count). Maps to <c>Counter&lt;T&gt;</c>.
        /// </summary>
        Counter,

        /// <summary>
        /// A sum that can increase or decrease reported by the application (for example a queue
        /// depth mutated by push/pop). Maps to <c>UpDownCounter&lt;T&gt;</c>.
        /// </summary>
        UpDownCounter,

        /// <summary>
        /// A distribution of recorded values, bucketed for percentile analysis (for example
        /// request duration). Maps to <c>Histogram&lt;T&gt;</c>.
        /// </summary>
        Histogram,

        /// <summary>
        /// A current value sampled at collection time via a callback (for example pool size).
        /// Maps to <c>ObservableGauge&lt;T&gt;</c>.
        /// </summary>
        ObservableGauge,

        /// <summary>
        /// A monotonically increasing sum sampled at collection time via a callback. Maps to
        /// <c>ObservableCounter&lt;T&gt;</c>.
        /// </summary>
        ObservableCounter,

        /// <summary>
        /// A sum that can increase or decrease sampled at collection time via a callback. Maps to
        /// <c>ObservableUpDownCounter&lt;T&gt;</c>.
        /// </summary>
        ObservableUpDownCounter
    }
}
