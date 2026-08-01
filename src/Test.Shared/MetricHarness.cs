namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;

    /// <summary>
    /// A self-contained in-memory metric reader for tests. It builds its own meter provider
    /// subscribing to the given meter names and an in-memory exporter, so a test can emit through
    /// Radiant (or the raw BCL) and then read back the aggregated values and tags without a
    /// collector.
    /// </summary>
    public sealed class MetricHarness : IDisposable
    {
        private readonly List<Metric> _Exported = new List<Metric>();
        private readonly MeterProvider _Provider;

        /// <summary>
        /// Create a harness subscribing to the given meter names.
        /// </summary>
        /// <param name="meterNames">The meter names to collect.</param>
        public MetricHarness(params string[] meterNames)
        {
            MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder();
            foreach (string name in meterNames)
            {
                if (!String.IsNullOrWhiteSpace(name)) builder.AddMeter(name);
            }
            builder.AddInMemoryExporter(_Exported);
            _Provider = builder.Build();
        }

        /// <summary>
        /// Force a collection so the in-memory list reflects the latest values.
        /// </summary>
        public void Flush()
        {
            _Provider.ForceFlush(5000);
        }

        /// <summary>
        /// Whether any metric with the given name has been exported.
        /// </summary>
        /// <param name="name">The instrument name.</param>
        /// <returns>True when present.</returns>
        public bool Has(string name)
        {
            Flush();
            for (int i = _Exported.Count - 1; i >= 0; i--)
            {
                if (String.Equals(_Exported[i].Name, name, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// Get the summed value of the latest export of a sum instrument (counter or up/down).
        /// </summary>
        /// <param name="name">The instrument name.</param>
        /// <returns>The sum, or null when the instrument was not found.</returns>
        public double? GetSum(string name)
        {
            Metric? metric = FindLatest(name);
            if (metric == null) return null;

            bool isDouble = metric.MetricType.IsDouble();
            double sum = 0;
            bool any = false;
            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                sum += isDouble ? point.GetSumDouble() : point.GetSumLong();
                any = true;
            }
            return any ? sum : (double?)null;
        }

        /// <summary>
        /// Get the summed value of the latest export of a sum instrument restricted to metric points
        /// carrying the given tag.
        /// </summary>
        /// <param name="name">The instrument name.</param>
        /// <param name="tagKey">The tag key to match.</param>
        /// <param name="tagValue">The tag value to match.</param>
        /// <returns>The sum across matching points, or null when the instrument was not found.</returns>
        public double? GetSumWithTag(string name, string tagKey, object tagValue)
        {
            Metric? metric = FindLatest(name);
            if (metric == null) return null;

            double sum = 0;
            bool any = false;
            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                if (PointHasTag(point, tagKey, tagValue))
                {
                    sum += point.GetSumDouble();
                    any = true;
                }
            }
            return any ? sum : (double?)null;
        }

        /// <summary>
        /// Get the last-value of the latest export of a gauge instrument.
        /// </summary>
        /// <param name="name">The instrument name.</param>
        /// <returns>The gauge value, or null when the instrument was not found.</returns>
        public double? GetGauge(string name)
        {
            Metric? metric = FindLatest(name);
            if (metric == null) return null;

            bool isDouble = metric.MetricType.IsDouble();
            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                return isDouble ? point.GetGaugeLastValueDouble() : point.GetGaugeLastValueLong();
            }
            return null;
        }

        /// <summary>
        /// Get the recorded count of the latest export of a histogram instrument.
        /// </summary>
        /// <param name="name">The instrument name.</param>
        /// <returns>The recorded count, or null when the instrument was not found.</returns>
        public long? GetHistogramCount(string name)
        {
            Metric? metric = FindLatest(name);
            if (metric == null) return null;

            long count = 0;
            bool any = false;
            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                count += point.GetHistogramCount();
                any = true;
            }
            return any ? count : (long?)null;
        }

        /// <summary>
        /// Determine whether any metric point of the named instrument carries the given tag.
        /// </summary>
        /// <param name="name">The instrument name.</param>
        /// <param name="tagKey">The tag key.</param>
        /// <param name="tagValue">The tag value.</param>
        /// <returns>True when a matching tagged point exists.</returns>
        public bool HasTag(string name, string tagKey, object tagValue)
        {
            Metric? metric = FindLatest(name);
            if (metric == null) return false;

            foreach (ref readonly MetricPoint point in metric.GetMetricPoints())
            {
                if (PointHasTag(point, tagKey, tagValue)) return true;
            }
            return false;
        }

        /// <summary>
        /// Dispose the underlying meter provider.
        /// </summary>
        public void Dispose()
        {
            _Provider.Dispose();
        }

        private Metric? FindLatest(string name)
        {
            Flush();
            for (int i = _Exported.Count - 1; i >= 0; i--)
            {
                if (String.Equals(_Exported[i].Name, name, StringComparison.Ordinal)) return _Exported[i];
            }
            return null;
        }

        private static bool PointHasTag(in MetricPoint point, string tagKey, object tagValue)
        {
            foreach (KeyValuePair<string, object?> tag in point.Tags)
            {
                if (String.Equals(tag.Key, tagKey, StringComparison.Ordinal) && Equals(tag.Value, tagValue)) return true;
            }
            return false;
        }
    }
}
