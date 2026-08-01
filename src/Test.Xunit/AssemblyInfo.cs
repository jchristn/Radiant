// Radiant's Touchstone descriptors emit into meters keyed by service name and read them back with
// an in-memory reader subscribed to that same name. The fact-style and theory-style runners execute
// the identical descriptor set, so running them in parallel would double-count measurements on the
// shared meter names. Serialize test execution to keep each descriptor's telemetry isolated.
[assembly: global::Xunit.CollectionBehavior(DisableTestParallelization = true)]
