namespace Radiant.Internal
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The resolved declared catalog (Tier 2). Holds the definitions keyed by instrument name and
    /// applies the resolved <see cref="LabelPolicyEnum"/> when a measurement carries a label key not
    /// declared for its instrument. Instruments with no definition are pass-through: the plain BCL
    /// emit path is never subject to the policy.
    /// <para>
    /// Thread safe. The warn-once set is guarded by a lock; lookups are read-only after construction.
    /// </para>
    /// </summary>
    internal sealed class MetricCatalog
    {
        #region Private-Members

        private readonly Dictionary<string, MetricDefinition> _Definitions;
        private readonly LabelPolicyEnum _ResolvedPolicy;
        private readonly Action<string>? _Diagnostic;
        private readonly HashSet<string> _Warned = new HashSet<string>();
        private readonly object _WarnLock = new object();

        #endregion

        #region Constructors-and-Factories

        internal MetricCatalog(IEnumerable<MetricDefinition> definitions, LabelPolicyEnum resolvedPolicy, Action<string>? diagnostic)
        {
            _Definitions = new Dictionary<string, MetricDefinition>(StringComparer.Ordinal);
            _ResolvedPolicy = resolvedPolicy;
            _Diagnostic = diagnostic;

            if (definitions != null)
            {
                foreach (MetricDefinition definition in definitions)
                {
                    if (definition == null) continue;
                    if (String.IsNullOrWhiteSpace(definition.Key)) continue;
                    _Definitions[definition.Key] = definition;
                }
            }
        }

        #endregion

        #region Internal-Members

        internal bool IsEmpty
        {
            get
            {
                return _Definitions.Count == 0;
            }
        }

        internal bool TryGet(string key, out MetricDefinition? definition)
        {
            return _Definitions.TryGetValue(key, out definition);
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Apply the label policy to the tags of a measurement against the named instrument's
        /// declaration. Returns the tags to record. When the instrument is not declared, the tags
        /// pass through unchanged.
        /// </summary>
        internal KeyValuePair<string, object?>[] ApplyPolicy(string key, RadiantTag[]? tags)
        {
            if (tags == null || tags.Length == 0) return Array.Empty<KeyValuePair<string, object?>>();

            MetricDefinition? definition;
            if (!_Definitions.TryGetValue(key, out definition) || definition == null)
            {
                return ToPairs(tags, tags.Length);
            }

            List<string> allowed = definition.LabelKeys;
            List<KeyValuePair<string, object?>> kept = new List<KeyValuePair<string, object?>>(tags.Length);

            for (int i = 0; i < tags.Length; i++)
            {
                RadiantTag tag = tags[i];
                if (allowed.Contains(tag.Key))
                {
                    kept.Add(tag.ToKeyValuePair());
                    continue;
                }

                if (_ResolvedPolicy == LabelPolicyEnum.Strict)
                {
                    throw new ArgumentException(
                        "Label key '" + tag.Key + "' is not declared for instrument '" + key + "'. " +
                        "Declare it in the metric catalog or remove it from the call site.",
                        nameof(tags));
                }

                WarnOnce(key, tag.Key);
            }

            return kept.ToArray();
        }

        #endregion

        #region Private-Methods

        private static KeyValuePair<string, object?>[] ToPairs(RadiantTag[] tags, int count)
        {
            KeyValuePair<string, object?>[] pairs = new KeyValuePair<string, object?>[count];
            for (int i = 0; i < count; i++) pairs[i] = tags[i].ToKeyValuePair();
            return pairs;
        }

        private void WarnOnce(string instrument, string label)
        {
            if (_Diagnostic == null) return;

            string composite = instrument + "|" + label;
            bool shouldWarn = false;

            lock (_WarnLock)
            {
                if (!_Warned.Contains(composite))
                {
                    _Warned.Add(composite);
                    shouldWarn = true;
                }
            }

            if (shouldWarn)
            {
                _Diagnostic(
                    "Radiant: dropped undeclared label key '" + label + "' on instrument '" +
                    instrument + "' (lenient label policy).");
            }
        }

        #endregion
    }
}
