namespace Radiant
{
    using System;

    /// <summary>
    /// Raised for Radiant-specific host and configuration failures that are neither an argument
    /// error nor a plain framework exception — for example attempting to start a second host on a
    /// scrape port already bound by this process, or an exporter that could not initialize.
    /// </summary>
    public class RadiantException : Exception
    {
        /// <summary>
        /// Create a Radiant exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        public RadiantException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Create a Radiant exception that wraps an underlying cause.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The underlying exception.</param>
        public RadiantException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
