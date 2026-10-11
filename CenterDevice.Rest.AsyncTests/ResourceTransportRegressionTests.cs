using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class ResourceTransportRegressionTests
    {
        [TestCase(503), TestCase(429)]
        public async Task DefaultTransportReturnsTransientErrorsOnceAndBoundsTheirBody(int status)
        {
            using (var server = new ErrorServer(status, 32768))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var client = new CountingClient(server.Address);
                var operation = client.DownloadPdfAsync("user", "error", cancellationToken: timeout.Token);
                try
                {
                    await server.BodyStarted.Task;
                    Assert.That(await Task.WhenAny(operation, Task.Delay(1500)), Is.SameAs(operation),
                        "The SDK must not await the remaining declared eight-megabyte error body before returning its bounded diagnostic.");
                    Exception failure = null;
                    try { using (await operation) { } }
                    catch (Exception error) { failure = error; }
                    Assert.That(failure, status == 429 ? Is.TypeOf<TooManyRequestsException>() : Is.TypeOf<RestClientException>());
                    Assert.That(client.Content.BytesRead, Is.GreaterThan(0).And.LessThanOrEqualTo(32768));
                    Assert.That(server.Requests, Is.EqualTo(1), "The resource GET must not be replayed.");
                    Assert.That(client.Content.Disposed, Is.True);
                    using (var next = await client.DownloadPdfAsync("user", "ready", cancellationToken: timeout.Token))
                        Assert.That(next.ReadByte(), Is.EqualTo((int)'x'), "The same-origin admission slot must be released after the error.");
                    Assert.That(server.Requests, Is.EqualTo(2));
                }
                finally { timeout.Cancel(); server.Abort(); try { await operation; } catch { } }
            }
        }

        [Test]
        public async Task ActiveCancellationAbortsASlowTransientBodyAndReleasesAdmission()
        {
            using (var server = new ErrorServer(503, 16))
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var client = new CountingClient(server.Address);
                var operation = client.DownloadPdfAsync("user", "error", cancellationToken: source.Token);
                try
                {
                    await server.BodyStarted.Task;
                    // Wait until the actual no-replay transport has returned headers and the reader is active.
                    Assert.That(await Task.WhenAny(client.HeadersReceived.Task, Task.Delay(1500)), Is.SameAs(client.HeadersReceived.Task));
                    source.Cancel();
                    var failure = Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => { using (await operation) { } }));
                    Assert.That(failure, Is.Not.Null);
                    Assert.That(client.Content.Disposed, Is.True);
                    Assert.That(server.Requests, Is.EqualTo(1));
                    using (var nextTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                    using (var next = await client.DownloadPdfAsync("user", "ready", cancellationToken: nextTimeout.Token))
                        Assert.That(next.ReadByte(), Is.EqualTo((int)'x'));
                    Assert.That(server.Requests, Is.EqualTo(2));
                }
                finally { source.Cancel(); server.Abort(); try { await operation; } catch { } }
            }
        }

        private sealed class Config : IRestClientConfiguration
        {
            private readonly string address;
            internal Config(string address) { this.address = address; }
            public string BaseAddress => address;
            public string UserAgent => "resource-transport-regression";
        }
        private sealed class Auth : IAsyncOAuthInfoProvider
        {
            public OAuthInfo GetOAuthInfo(string id) => throw new AssertionException("Async authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string id, CancellationToken cancellationToken = default(CancellationToken))
                => Task.FromResult(new OAuthInfo { UserId = id, access_token = "isolated-fixture" });
        }
        private sealed class CountingClient : DocumentRestClient
        {
            internal CountingContent Content;
            internal readonly TaskCompletionSource<bool> HeadersReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CountingClient(string address) : base(new Auth(), new Config(address), null, null, "v2/") { }
            protected override async Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token)
            {
                // Execute the actual default HttpClient + shared allowance handler, then observe its body reads.
                var response = await base.SendResourceDownloadRequestAsync(request, token);
                Content = new CountingContent(response.Content);
                response.Content = Content;
                HeadersReceived.TrySetResult(true);
                return response;
            }
        }
        private sealed class CountingContent : HttpContent
        {
            private readonly HttpContent inner;
            internal long BytesRead;
            internal bool Disposed;
            internal CountingContent(HttpContent inner)
            {
                this.inner = inner;
                foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => throw new AssertionException("The download must not buffer the HTTP content.");
            protected override bool TryComputeLength(out long length)
            { length = inner.Headers.ContentLength ?? 0; return inner.Headers.ContentLength.HasValue; }
            protected override async Task<Stream> CreateContentReadStreamAsync()
                => new CountingStream(await inner.ReadAsStreamAsync(), count => BytesRead += count);
            protected override void Dispose(bool disposing)
            { if (disposing) { Disposed = true; inner.Dispose(); } base.Dispose(disposing); }
        }
        private sealed class CountingStream : Stream
        {
            private readonly Stream inner;
            private readonly Action<int> record;
            internal CountingStream(Stream inner, Action<int> record) { this.inner = inner; this.record = record; }
            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count)
            { var read = inner.Read(buffer, offset, count); record(read); return read; }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            { var read = await inner.ReadAsync(buffer, offset, count, token); record(read); return read; }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        }
        private sealed class ErrorServer : IDisposable
        {
            private readonly TcpListener listener;
            private readonly Task serving;
            private readonly List<TcpClient> connections = new List<TcpClient>();
            internal readonly TaskCompletionSource<bool> BodyStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal int Requests;
            internal string Address { get; }
            internal ErrorServer(int status, int prefixBytes)
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                Address = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
                serving = Serve(status, prefixBytes);
            }
            private async Task Serve(int status, int prefixBytes)
            {
                try
                {
                    while (true)
                    {
                        using (var connection = await listener.AcceptTcpClientAsync())
                        using (var stream = connection.GetStream())
                        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                        {
                            lock (connections) connections.Add(connection);
                            var first = await reader.ReadLineAsync();
                            if (first == null) continue;
                            var ready = first.Split(' ')[1].EndsWith("/ready", StringComparison.Ordinal);
                            while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                            Interlocked.Increment(ref Requests);
                            var header = ready
                                ? "HTTP/1.1 200 OK\r\nContent-Type: application/pdf\r\nContent-Length: 1\r\nConnection: close\r\n\r\nx"
                                : "HTTP/1.1 " + status + " Error\r\nContent-Type: text/plain\r\nRetry-After: 0\r\nContent-Length: 8388608\r\nConnection: close\r\n\r\n";
                            var bytes = Encoding.ASCII.GetBytes(header);
                            await stream.WriteAsync(bytes, 0, bytes.Length);
                            if (!ready)
                            {
                                var prefix = Encoding.ASCII.GetBytes(new string('e', prefixBytes));
                                await stream.WriteAsync(prefix, 0, prefix.Length);
                                BodyStarted.TrySetResult(true);
                                try { await stream.ReadAsync(new byte[1], 0, 1); } catch (IOException) { }
                            }
                        }
                    }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
            }
            internal void Abort()
            {
                listener.Stop();
                lock (connections) foreach (var connection in connections) connection.Close();
            }
            public void Dispose() { Abort(); serving.GetAwaiter().GetResult(); }
        }
    }
}
