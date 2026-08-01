namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A minimal in-process HTTP endpoint that records every request it receives and answers 200.
    /// Used as a stand-in OTLP/HTTP or Loki endpoint so export wiring can be asserted without a real
    /// collector: point an exporter at <see cref="BaseUrl"/>, emit, force a flush, then inspect
    /// <see cref="Requests"/>.
    /// </summary>
    public sealed class RecordingHttpEndpoint : IDisposable
    {
        private readonly HttpListener _Listener;
        private readonly List<RecordedRequest> _Requests = new List<RecordedRequest>();
        private readonly object _Lock = new object();
        private readonly CancellationTokenSource _Cts = new CancellationTokenSource();

        /// <summary>
        /// The TCP port the endpoint is listening on.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// The base URL of the endpoint (for example <c>http://localhost:53412</c>).
        /// </summary>
        public string BaseUrl
        {
            get
            {
                return "http://localhost:" + Port.ToString();
            }
        }

        /// <summary>
        /// Create and start the endpoint on a free local port.
        /// </summary>
        public RecordingHttpEndpoint()
        {
            Port = FindFreePort();
            _Listener = new HttpListener();
            _Listener.Prefixes.Add("http://localhost:" + Port.ToString() + "/");
            _Listener.Start();
            Task.Run(() => AcceptLoopAsync(_Cts.Token));
        }

        /// <summary>
        /// A snapshot of the requests recorded so far.
        /// </summary>
        public IReadOnlyList<RecordedRequest> Requests
        {
            get
            {
                lock (_Lock)
                {
                    return new List<RecordedRequest>(_Requests);
                }
            }
        }

        /// <summary>
        /// Block until at least one request has been recorded or the timeout elapses.
        /// </summary>
        /// <param name="timeoutMs">The maximum time to wait, in milliseconds.</param>
        /// <returns>True when at least one request arrived within the timeout.</returns>
        public bool WaitForAnyRequest(int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (_Lock)
                {
                    if (_Requests.Count > 0) return true;
                }
                Thread.Sleep(25);
            }
            lock (_Lock)
            {
                return _Requests.Count > 0;
            }
        }

        /// <summary>
        /// Stop the endpoint and release the port.
        /// </summary>
        public void Dispose()
        {
            try { _Cts.Cancel(); } catch { /* ignore */ }
            try { _Listener.Stop(); } catch { /* ignore */ }
            try { _Listener.Close(); } catch { /* ignore */ }
            _Cts.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                RecordedRequest recorded = new RecordedRequest();
                recorded.Method = context.Request.HttpMethod;
                recorded.Path = context.Request.Url != null ? context.Request.Url.AbsolutePath : String.Empty;

                foreach (string? key in context.Request.Headers.AllKeys)
                {
                    if (key == null) continue;
                    recorded.Headers[key] = context.Request.Headers[key] ?? String.Empty;
                }

                using (MemoryStream buffer = new MemoryStream())
                {
                    await context.Request.InputStream.CopyToAsync(buffer).ConfigureAwait(false);
                    recorded.BodyByteCount = (int)buffer.Length;
                }

                lock (_Lock)
                {
                    _Requests.Add(recorded);
                }

                try
                {
                    context.Response.StatusCode = 200;
                    context.Response.Close();
                }
                catch
                {
                    // ignore response failures
                }
            }
        }

        private static int FindFreePort()
        {
            TcpListener probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
