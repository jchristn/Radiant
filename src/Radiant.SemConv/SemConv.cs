namespace Radiant
{
    /// <summary>
    /// OpenTelemetry semantic-convention instrument definitions and attribute keys for the emit side
    /// of telemetry.
    /// <para>
    /// Each instrument member is a ready-made <see cref="Convention"/> — a name plus its kind, UCUM
    /// unit, and allowed label keys. Your own instruments are declared exactly the same way, with the
    /// <see cref="Convention"/> factories, and flow through the same emit and catalog APIs; there is
    /// nothing special about the built-ins. Attribute keys and units, which are plain strings rather
    /// than instruments, remain <c>const string</c>.
    /// </para>
    /// <para>
    /// Because a <see cref="Convention"/> converts implicitly to its name, a member such as
    /// <see cref="Http.RequestDuration"/> can be passed anywhere an instrument-name string is
    /// expected. Names, units, and attribute keys follow the OpenTelemetry semantic conventions so
    /// instruments render in stock dashboards without custom configuration.
    /// </para>
    /// <para>
    /// This type groups its members into nested static holders that mirror the OpenTelemetry
    /// semantic-convention layout, which is the reason this file deliberately holds more than one
    /// type.
    /// </para>
    /// </summary>
    public static class SemConv
    {
        /// <summary>
        /// HTTP server and client instrument conventions, units, and attribute keys.
        /// </summary>
        public static class Http
        {
            /// <summary>
            /// UCUM unit for request/response duration histograms: seconds.
            /// </summary>
            public const string UnitDuration = "s";

            /// <summary>
            /// UCUM unit for body-size histograms: bytes.
            /// </summary>
            public const string UnitSize = "By";

            /// <summary>
            /// Attribute key for the HTTP request method (for example <c>GET</c>).
            /// </summary>
            public const string AttributeMethod = "http.request.method";

            /// <summary>
            /// Attribute key for the HTTP response status code (for example <c>200</c>).
            /// </summary>
            public const string AttributeStatusCode = "http.response.status_code";

            /// <summary>
            /// Attribute key for the matched route template (for example <c>/api/v1/tenants/{id}</c>).
            /// </summary>
            public const string AttributeRoute = "http.route";

            /// <summary>
            /// Attribute key for the URL scheme (for example <c>http</c> or <c>https</c>).
            /// </summary>
            public const string AttributeScheme = "url.scheme";

            /// <summary>
            /// Duration of inbound HTTP server requests. Histogram, seconds (<c>s</c>), name
            /// <c>http.server.request.duration</c>.
            /// </summary>
            public static readonly Convention ServerRequestDuration =
                Convention.Histogram("http.server.request.duration", UnitDuration, null, AttributeMethod, AttributeStatusCode, AttributeRoute)
                    .WithDescription("Duration of inbound HTTP server requests.");

            /// <summary>
            /// Duration of inbound HTTP server requests. Alias of <see cref="ServerRequestDuration"/>.
            /// </summary>
            public static readonly Convention RequestDuration = ServerRequestDuration;

            /// <summary>
            /// Concurrent HTTP server requests in flight. Up/down counter, <c>{request}</c>, name
            /// <c>http.server.active_requests</c>.
            /// </summary>
            public static readonly Convention ServerActiveRequests =
                Convention.UpDownCounter("http.server.active_requests", "{request}", AttributeMethod, AttributeScheme)
                    .WithDescription("Concurrent HTTP server requests in flight.");

            /// <summary>
            /// Size of inbound HTTP server request bodies. Histogram, bytes (<c>By</c>), name
            /// <c>http.server.request.body.size</c>.
            /// </summary>
            public static readonly Convention ServerRequestBodySize =
                Convention.Histogram("http.server.request.body.size", UnitSize, null, AttributeMethod)
                    .WithDescription("Size of inbound HTTP server request bodies.");

            /// <summary>
            /// Size of outbound HTTP server response bodies. Histogram, bytes (<c>By</c>), name
            /// <c>http.server.response.body.size</c>.
            /// </summary>
            public static readonly Convention ServerResponseBodySize =
                Convention.Histogram("http.server.response.body.size", UnitSize, null, AttributeMethod, AttributeStatusCode)
                    .WithDescription("Size of outbound HTTP server response bodies.");

            /// <summary>
            /// Duration of outbound HTTP client requests. Histogram, seconds (<c>s</c>), name
            /// <c>http.client.request.duration</c>.
            /// </summary>
            public static readonly Convention ClientRequestDuration =
                Convention.Histogram("http.client.request.duration", UnitDuration, null, AttributeMethod, AttributeStatusCode)
                    .WithDescription("Duration of outbound HTTP client requests.");
        }

        /// <summary>
        /// RPC instrument conventions and attribute keys.
        /// </summary>
        public static class Rpc
        {
            /// <summary>
            /// UCUM unit for RPC duration histograms: seconds.
            /// </summary>
            public const string UnitDuration = "s";

            /// <summary>
            /// Attribute key for the RPC system (for example <c>grpc</c>).
            /// </summary>
            public const string AttributeSystem = "rpc.system";

            /// <summary>
            /// Attribute key for the RPC service name.
            /// </summary>
            public const string AttributeService = "rpc.service";

            /// <summary>
            /// Attribute key for the RPC method name.
            /// </summary>
            public const string AttributeMethod = "rpc.method";

            /// <summary>
            /// Duration of inbound RPC server calls. Histogram, seconds (<c>s</c>), name
            /// <c>rpc.server.duration</c>.
            /// </summary>
            public static readonly Convention ServerDuration =
                Convention.Histogram("rpc.server.duration", UnitDuration, null, AttributeSystem, AttributeService, AttributeMethod)
                    .WithDescription("Duration of inbound RPC server calls.");

            /// <summary>
            /// Duration of outbound RPC client calls. Histogram, seconds (<c>s</c>), name
            /// <c>rpc.client.duration</c>.
            /// </summary>
            public static readonly Convention ClientDuration =
                Convention.Histogram("rpc.client.duration", UnitDuration, null, AttributeSystem, AttributeService, AttributeMethod)
                    .WithDescription("Duration of outbound RPC client calls.");
        }

        /// <summary>
        /// Database client instrument conventions and attribute keys.
        /// </summary>
        public static class Db
        {
            /// <summary>
            /// UCUM unit for database operation duration histograms: seconds.
            /// </summary>
            public const string UnitDuration = "s";

            /// <summary>
            /// Attribute key for the database system (for example <c>postgresql</c>).
            /// </summary>
            public const string AttributeSystem = "db.system";

            /// <summary>
            /// Attribute key for the database operation name (for example <c>SELECT</c>).
            /// </summary>
            public const string AttributeOperation = "db.operation.name";

            /// <summary>
            /// Duration of database client operations. Histogram, seconds (<c>s</c>), name
            /// <c>db.client.operation.duration</c>.
            /// </summary>
            public static readonly Convention ClientOperationDuration =
                Convention.Histogram("db.client.operation.duration", UnitDuration, null, AttributeSystem, AttributeOperation)
                    .WithDescription("Duration of database client operations.");

            /// <summary>
            /// Open database connections. Up/down counter, <c>{connection}</c>, name
            /// <c>db.client.connection.count</c>.
            /// </summary>
            public static readonly Convention ClientConnectionCount =
                Convention.UpDownCounter("db.client.connection.count", "{connection}", AttributeSystem)
                    .WithDescription("Open database connections.");
        }

        /// <summary>
        /// Messaging instrument conventions and attribute keys.
        /// </summary>
        public static class Messaging
        {
            /// <summary>
            /// UCUM unit for message processing duration histograms: seconds.
            /// </summary>
            public const string UnitDuration = "s";

            /// <summary>
            /// Attribute key for the messaging system (for example <c>rabbitmq</c>).
            /// </summary>
            public const string AttributeSystem = "messaging.system";

            /// <summary>
            /// Attribute key for the destination name (queue or topic).
            /// </summary>
            public const string AttributeDestination = "messaging.destination.name";

            /// <summary>
            /// Messages produced. Counter, <c>{message}</c>, name
            /// <c>messaging.client.sent.messages</c>.
            /// </summary>
            public static readonly Convention ClientSentMessages =
                Convention.Counter("messaging.client.sent.messages", "{message}", AttributeSystem, AttributeDestination)
                    .WithDescription("Messages produced.");

            /// <summary>
            /// Messages consumed. Counter, <c>{message}</c>, name
            /// <c>messaging.client.consumed.messages</c>.
            /// </summary>
            public static readonly Convention ClientConsumedMessages =
                Convention.Counter("messaging.client.consumed.messages", "{message}", AttributeSystem, AttributeDestination)
                    .WithDescription("Messages consumed.");

            /// <summary>
            /// Duration of message processing. Histogram, seconds (<c>s</c>), name
            /// <c>messaging.process.duration</c>.
            /// </summary>
            public static readonly Convention ProcessDuration =
                Convention.Histogram("messaging.process.duration", UnitDuration, null, AttributeSystem, AttributeDestination)
                    .WithDescription("Duration of message processing.");
        }

        /// <summary>
        /// Background-worker / job instrument conventions for the common "process a unit of work"
        /// loop. Radiant conventions layered on the OpenTelemetry attribute vocabulary.
        /// </summary>
        public static class Worker
        {
            /// <summary>
            /// UCUM unit for work-item duration histograms: seconds.
            /// </summary>
            public const string UnitDuration = "s";

            /// <summary>
            /// Attribute key for the logical job or worker name.
            /// </summary>
            public const string AttributeJob = "worker.job";

            /// <summary>
            /// Work items processed. Counter, <c>{item}</c>, name <c>worker.items.processed</c>.
            /// </summary>
            public static readonly Convention ItemsProcessed =
                Convention.Counter("worker.items.processed", "{item}", AttributeJob)
                    .WithDescription("Work items processed.");

            /// <summary>
            /// Work items that failed processing. Counter, <c>{item}</c>, name
            /// <c>worker.items.failed</c>.
            /// </summary>
            public static readonly Convention ItemsFailed =
                Convention.Counter("worker.items.failed", "{item}", AttributeJob)
                    .WithDescription("Work items that failed processing.");

            /// <summary>
            /// Duration spent processing a single work item. Histogram, seconds (<c>s</c>), name
            /// <c>worker.item.duration</c>.
            /// </summary>
            public static readonly Convention ItemDuration =
                Convention.Histogram("worker.item.duration", UnitDuration, null, AttributeJob)
                    .WithDescription("Duration spent processing a single work item.");

            /// <summary>
            /// Work items waiting to be processed. Observable gauge, <c>{item}</c>, name
            /// <c>worker.queue.depth</c>.
            /// </summary>
            public static readonly Convention QueueDepth =
                Convention.Gauge("worker.queue.depth", "{item}", AttributeJob)
                    .WithDescription("Work items waiting to be processed.");
        }

        /// <summary>
        /// Process / runtime instrument conventions for baseline host health.
        /// </summary>
        public static class Process
        {
            /// <summary>
            /// UCUM unit for byte-valued process gauges: bytes.
            /// </summary>
            public const string UnitBytes = "By";

            /// <summary>
            /// UCUM unit for second-valued process gauges: seconds.
            /// </summary>
            public const string UnitSeconds = "s";

            /// <summary>
            /// Resident memory of the process. Observable gauge, bytes (<c>By</c>), name
            /// <c>process.memory.usage</c>.
            /// </summary>
            public static readonly Convention MemoryUsage =
                Convention.Gauge("process.memory.usage", UnitBytes)
                    .WithDescription("Resident working set of the process.");

            /// <summary>
            /// Process CPU utilization in the range 0..1. Observable gauge, dimensionless (<c>1</c>),
            /// name <c>process.cpu.utilization</c>.
            /// </summary>
            public static readonly Convention CpuUtilization =
                Convention.Gauge("process.cpu.utilization", "1")
                    .WithDescription("Process CPU utilization (0..1).");

            /// <summary>
            /// OS threads in the process. Observable gauge, <c>{thread}</c>, name
            /// <c>process.thread.count</c>.
            /// </summary>
            public static readonly Convention ThreadCount =
                Convention.Gauge("process.thread.count", "{thread}")
                    .WithDescription("Number of OS threads in the process.");

            /// <summary>
            /// Seconds since process start. Observable gauge, seconds (<c>s</c>), name
            /// <c>process.uptime</c>.
            /// </summary>
            public static readonly Convention Uptime =
                Convention.Gauge("process.uptime", UnitSeconds)
                    .WithDescription("Seconds since the host started.");
        }

        /// <summary>
        /// Cross-cutting resource and correlation attribute keys. These are label/attribute keys,
        /// not instruments, so they remain plain string constants.
        /// </summary>
        public static class Attributes
        {
            /// <summary>
            /// Resource attribute key for the logical service name.
            /// </summary>
            public const string ServiceName = "service.name";

            /// <summary>
            /// Resource attribute key for the service instance identifier.
            /// </summary>
            public const string ServiceInstanceId = "service.instance.id";

            /// <summary>
            /// Attribute key naming the transport/protocol dimension adapters stamp on their
            /// instruments (for example <c>http</c>, <c>ws</c>, <c>s3</c>, <c>resp</c>, <c>tcp</c>).
            /// </summary>
            public const string Protocol = "protocol";
        }
    }
}
