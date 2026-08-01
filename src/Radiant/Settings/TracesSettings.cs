namespace Radiant
{
    using Radiant.Internal;

    /// <summary>
    /// Traces pillar settings: sampling and context propagation.
    /// <para>
    /// This type is not thread safe. Configure it before <see cref="RadiantHost.Start(RadiantSettings)"/>.
    /// </para>
    /// </summary>
    public class TracesSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the traces pillar is enabled. Default true. When false, no tracer provider is
        /// built.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The head-based sampling ratio in the range 0.0 to 1.0. Default 1.0 (sample everything).
        /// A value of 0.25 samples roughly a quarter of root traces. Applied through a
        /// parent-based sampler so a sampled parent keeps its children sampled.
        /// </summary>
        public double SamplingRatio
        {
            get
            {
                return _SamplingRatio;
            }
            set
            {
                _SamplingRatio = RadiantMath.Clamp(value, 0.0, 1.0);
            }
        }

        /// <summary>
        /// Whether to install W3C trace-context propagation for inbound and outbound calls.
        /// Default true.
        /// </summary>
        public bool PropagateContext { get; set; } = true;

        #endregion

        #region Private-Members

        private double _SamplingRatio = 1.0;

        #endregion
    }
}
