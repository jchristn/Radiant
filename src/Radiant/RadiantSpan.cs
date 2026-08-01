namespace Radiant
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// A thin <see cref="IDisposable"/> wrapper over a <see cref="System.Diagnostics.Activity"/>
    /// that times a unit of work and carries tags, a status, and recorded exceptions. Disposing the
    /// span stops the underlying activity.
    /// <para>
    /// When no listener is sampling the activity source, the wrapped activity is null and every
    /// method is a safe no-op. Check <see cref="IsRecording"/> before doing expensive work only to
    /// attach it to a span.
    /// </para>
    /// <para>
    /// A span is not thread safe; use it on the logical flow that created it.
    /// </para>
    /// </summary>
    public sealed class RadiantSpan : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Whether the underlying activity exists and is recording. False when no listener is
        /// sampling, in which case tag and status calls do nothing.
        /// </summary>
        public bool IsRecording
        {
            get
            {
                return _Activity != null && _Activity.IsAllDataRequested;
            }
        }

        /// <summary>
        /// The wrapped activity, or null when nothing is sampling. Exposed for interop with code
        /// that consumes <see cref="System.Diagnostics.Activity"/> directly.
        /// </summary>
        public Activity? Activity
        {
            get
            {
                return _Activity;
            }
        }

        /// <summary>
        /// The W3C trace identifier of the span, or null when nothing is sampling.
        /// </summary>
        public string? TraceId
        {
            get
            {
                return _Activity?.TraceId.ToString();
            }
        }

        /// <summary>
        /// The span identifier, or null when nothing is sampling.
        /// </summary>
        public string? SpanId
        {
            get
            {
                return _Activity?.SpanId.ToString();
            }
        }

        #endregion

        #region Private-Members

        private readonly Activity? _Activity;
        private bool _Disposed;

        #endregion

        #region Constructors-and-Factories

        private RadiantSpan(Activity? activity)
        {
            _Activity = activity;
        }

        internal static RadiantSpan Start(ActivitySource source, string name, SpanKindEnum kind)
        {
            Activity? activity = source.StartActivity(name, ToActivityKind(kind));
            return new RadiantSpan(activity);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Set a tag on the span. A no-op when nothing is sampling.
        /// </summary>
        /// <param name="key">The tag key. Must be non-null and non-empty.</param>
        /// <param name="value">The tag value. May be null.</param>
        /// <returns>This span, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
        public RadiantSpan SetTag(string key, object? value)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            _Activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Set a tag on the span from a <see cref="RadiantTag"/>. A no-op when nothing is sampling.
        /// </summary>
        /// <param name="tag">The tag to set.</param>
        /// <returns>This span, for chaining.</returns>
        public RadiantSpan SetTag(RadiantTag tag)
        {
            _Activity?.SetTag(tag.Key, tag.Value);
            return this;
        }

        /// <summary>
        /// Mark the span's status as OK.
        /// </summary>
        /// <param name="description">An optional status description.</param>
        /// <returns>This span, for chaining.</returns>
        public RadiantSpan SetOk(string? description = null)
        {
            _Activity?.SetStatus(ActivityStatusCode.Ok, description);
            return this;
        }

        /// <summary>
        /// Mark the span's status as error.
        /// </summary>
        /// <param name="description">An optional status description explaining the failure.</param>
        /// <returns>This span, for chaining.</returns>
        public RadiantSpan SetError(string? description = null)
        {
            _Activity?.SetStatus(ActivityStatusCode.Error, description);
            return this;
        }

        /// <summary>
        /// Record an exception on the span as an <c>exception</c> event with the standard
        /// <c>exception.type</c>, <c>exception.message</c>, and <c>exception.stacktrace</c>
        /// attributes, and set the span status to error. A no-op when nothing is sampling.
        /// </summary>
        /// <param name="exception">The exception to record. Must be non-null.</param>
        /// <param name="setErrorStatus">Whether to also set the span status to error. Default true.</param>
        /// <returns>This span, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
        public RadiantSpan RecordException(Exception exception, bool setErrorStatus = true)
        {
            if (exception == null) throw new ArgumentNullException(nameof(exception));

            if (_Activity != null)
            {
                ActivityTagsCollection tags = new ActivityTagsCollection();
                tags["exception.type"] = exception.GetType().FullName;
                tags["exception.message"] = exception.Message;
                if (exception.StackTrace != null) tags["exception.stacktrace"] = exception.StackTrace;

                _Activity.AddEvent(new ActivityEvent("exception", default(DateTimeOffset), tags));
                if (setErrorStatus) _Activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            }

            return this;
        }

        /// <summary>
        /// Stop the underlying activity.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Activity?.Dispose();
        }

        #endregion

        #region Private-Methods

        private static ActivityKind ToActivityKind(SpanKindEnum kind)
        {
            switch (kind)
            {
                case SpanKindEnum.Server: return ActivityKind.Server;
                case SpanKindEnum.Client: return ActivityKind.Client;
                case SpanKindEnum.Producer: return ActivityKind.Producer;
                case SpanKindEnum.Consumer: return ActivityKind.Consumer;
                default: return ActivityKind.Internal;
            }
        }

        #endregion
    }
}
