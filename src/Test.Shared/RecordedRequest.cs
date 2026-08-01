namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single HTTP request captured by <see cref="RecordingHttpEndpoint"/>.
    /// </summary>
    public sealed class RecordedRequest
    {
        /// <summary>
        /// The request HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// The request path (for example <c>/v1/metrics</c>).
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// The request headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The number of body bytes received.
        /// </summary>
        public int BodyByteCount { get; set; } = 0;
    }
}
