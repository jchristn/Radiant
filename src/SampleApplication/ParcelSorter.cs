namespace SampleApplication
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading;

    /// <summary>
    /// The instrumented domain type from INTEGRATION.md. It "sorts" parcels by region and emits
    /// telemetry entirely through the .NET base class library — a <see cref="Meter"/> for metrics and
    /// an <see cref="ActivitySource"/> for spans. It takes no dependency on Radiant; a host observes
    /// it by subscribing to the meter and activity-source names below. When nothing subscribes, every
    /// measurement here is a cheap no-op.
    /// </summary>
    public sealed class ParcelSorter
    {
        #region Public-Members

        /// <summary>
        /// The number of parcels currently waiting to be sorted.
        /// </summary>
        public int QueueDepth
        {
            get
            {
                return Volatile.Read(ref _QueueDepth);
            }
        }

        #endregion

        #region Private-Members

        // These two names are the contract between this code and any observing host. Keep them stable.
        private static readonly Meter _Meter = new Meter("ParcelSorter.Core");
        private static readonly ActivitySource _Activity = new ActivitySource("ParcelSorter.Core");

        private static readonly Counter<long> _Sorted =
            _Meter.CreateCounter<long>("parcels.sorted", "{parcel}", "Parcels sorted.");

        private static readonly Histogram<double> _SortDuration =
            _Meter.CreateHistogram<double>("parcels.sort.duration", "s", "Time to sort one parcel.");

        private static readonly UpDownCounter<long> _InFlight =
            _Meter.CreateUpDownCounter<long>("parcels.inflight", "{parcel}", "Parcels being sorted right now.");

        private int _QueueDepth;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a parcel sorter and register its queue-depth gauge. The gauge callback is read at
        /// collection time, not when the depth changes.
        /// </summary>
        public ParcelSorter()
        {
            _Meter.CreateObservableGauge<int>(
                "parcels.queue.depth",
                () => Volatile.Read(ref _QueueDepth),
                "{parcel}",
                "Parcels waiting to be sorted.");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add parcels to the backlog.
        /// </summary>
        /// <param name="count">The number of parcels to enqueue. Must be non-negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
        public void Enqueue(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            Interlocked.Add(ref _QueueDepth, count);
        }

        /// <summary>
        /// Sort one parcel destined for the given region, emitting a span and metrics for the work.
        /// </summary>
        /// <param name="region">The destination region. Low cardinality — must be non-null and non-empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="region"/> is null or empty.</exception>
        public void Sort(string region)
        {
            if (String.IsNullOrWhiteSpace(region)) throw new ArgumentNullException(nameof(region));

            using (Activity? activity = _Activity.StartActivity("sort-parcel", ActivityKind.Internal))
            {
                activity?.SetTag("region", region);

                _InFlight.Add(1);
                long startTicks = Stopwatch.GetTimestamp();
                try
                {
                    DoWork();

                    double seconds = (Stopwatch.GetTimestamp() - startTicks) / (double)Stopwatch.Frequency;

                    // Low-cardinality tags only. A parcel id would belong on the span or in a log,
                    // never here — see the cardinality note in INTEGRATION.md.
                    TagList tags = new TagList
                    {
                        { "region", region },
                        { "outcome", "ok" }
                    };
                    _Sorted.Add(1, tags);
                    _SortDuration.Record(seconds, tags);

                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception e)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, e.Message);
                    throw;
                }
                finally
                {
                    Interlocked.Add(ref _QueueDepth, -1);
                    _InFlight.Add(-1);
                }
            }
        }

        #endregion

        #region Private-Methods

        private static void DoWork()
        {
            Thread.Sleep(Random.Shared.Next(2, 40));
        }

        #endregion
    }
}
