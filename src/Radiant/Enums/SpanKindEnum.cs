namespace Radiant
{
    /// <summary>
    /// The role a span plays in a trace. Maps onto <see cref="System.Diagnostics.ActivityKind"/>.
    /// </summary>
    public enum SpanKindEnum
    {
        /// <summary>
        /// An internal operation with no remote counterpart (the default).
        /// </summary>
        Internal,

        /// <summary>
        /// Handling of an inbound request; the server side of a remote call.
        /// </summary>
        Server,

        /// <summary>
        /// Issuing of an outbound request; the client side of a remote call.
        /// </summary>
        Client,

        /// <summary>
        /// Creation of a message for asynchronous processing by a consumer.
        /// </summary>
        Producer,

        /// <summary>
        /// Processing of a message created by a producer.
        /// </summary>
        Consumer
    }
}
