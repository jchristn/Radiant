namespace Radiant
{
    /// <summary>
    /// The wire protocol used by the OTLP exporter to reach a collector.
    /// </summary>
    public enum OtlpProtocolEnum
    {
        /// <summary>
        /// OTLP over gRPC, the default. Typically served on port 4317.
        /// </summary>
        Grpc,

        /// <summary>
        /// OTLP over HTTP with protobuf-encoded payloads. Typically served on port 4318.
        /// </summary>
        HttpProtobuf
    }
}
