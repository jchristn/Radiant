namespace SampleApplication
{
    using Radiant;

    /// <summary>
    /// The application's own metric conventions, declared exactly the way the built-in
    /// <see cref="SemConv"/> conventions are — same <see cref="Convention"/> type, same factories.
    /// These names match the instruments <see cref="ParcelSorter"/> emits into; registering them via
    /// <c>settings.Metrics.DefineAll(ParcelConventions.All)</c> gives the host their units, histogram
    /// buckets, and allowed label keys, and turns on the label-policy guardrail for them.
    /// </summary>
    public static class ParcelConventions
    {
        /// <summary>
        /// Parcels sorted, keyed by region and outcome.
        /// </summary>
        public static readonly Convention Sorted =
            Convention.Counter("parcels.sorted", "{parcel}", "region", "outcome")
                .WithDescription("Parcels sorted.");

        /// <summary>
        /// Time to sort one parcel, with fine-grained sub-second buckets.
        /// </summary>
        public static readonly Convention SortDuration =
            Convention.Histogram("parcels.sort.duration", "s", LatencyBuckets.Fast, "region", "outcome")
                .WithDescription("Time to sort one parcel.");

        /// <summary>
        /// Every convention this application declares, for one-call registration.
        /// </summary>
        public static readonly Convention[] All = new Convention[] { Sorted, SortDuration };
    }
}
