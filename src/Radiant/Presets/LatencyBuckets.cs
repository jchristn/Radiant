namespace Radiant
{
    /// <summary>
    /// Ready-made histogram bucket boundaries for latency instruments, expressed in seconds to
    /// match the UCUM <c>s</c> unit used by the semantic-convention duration instruments. Assign a
    /// set to <see cref="MetricDefinition.Buckets"/> instead of hand-typing boundaries.
    /// </summary>
    public static class LatencyBuckets
    {
        /// <summary>
        /// The default latency boundaries, aligned with the OpenTelemetry recommended set for HTTP
        /// duration histograms: 5ms through 10s. A reasonable choice for most request and job
        /// timings.
        /// </summary>
        public static double[] Default
        {
            get
            {
                return new double[]
                {
                    0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0
                };
            }
        }

        /// <summary>
        /// Fine-grained boundaries for sub-second, in-process operations: 100µs through 1s. Use for
        /// cache hits, serialization, and other fast paths where the default buckets are too coarse.
        /// </summary>
        public static double[] Fast
        {
            get
            {
                return new double[]
                {
                    0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0
                };
            }
        }

        /// <summary>
        /// Wide boundaries for slow, network- or IO-bound operations: 10ms through 2 minutes. Use
        /// for downstream calls, large uploads, and batch jobs.
        /// </summary>
        public static double[] Network
        {
            get
            {
                return new double[]
                {
                    0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0, 120.0
                };
            }
        }
    }
}
