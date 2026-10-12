using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.User;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class UserPictureDownloadTests
    {
        [TestCase(false, "image/png"), TestCase(true, "image/png")]
        [TestCase(false, "image/jpeg"), TestCase(true, "IMAGE/JPEG")]
        public async Task OriginalBytesRemainUnbufferedAndResponseOwned(bool sync, string mime)
        {
            var auth = new Auth(); var content = new PictureContent(mime); var client = new Client(auth) { Content = content };
            using (var stream = sync ? client.DownloadUserPicture("context", "selected/with?query") : await client.DownloadUserPictureAsync("context", "selected/with?query"))
            {
                Assert.That(client.Uri.AbsoluteUri, Is.EqualTo("https://unused.invalid/bridge/v2/user/selected%2Fwith%3Fquery/picture"));
                Assert.That(client.Method, Is.EqualTo(HttpMethod.Get)); Assert.That(client.HasBody, Is.False);
                Assert.That(client.Accept, Is.EqualTo("image/png, image/jpeg")); Assert.That(client.Bearer, Is.EqualTo("Bearer fixture"));
                Assert.That(auth.Context, Is.EqualTo("context")); Assert.That(content.Buffered, Is.False); Assert.That(content.Disposed, Is.False);
                var received = new byte[content.Bytes.Length]; Assert.That(await stream.ReadAsync(received, 0, received.Length), Is.EqualTo(received.Length));
                Assert.That(received, Is.EqualTo(content.Bytes));
            }
            Assert.That(content.Disposed, Is.True); Assert.That(client.Calls, Is.EqualTo(1));
        }

        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task BridgeDirectoryAndPrefixSlashesRemainCompatible(bool trailing, bool leading)
        {
            var client = new Client(new Auth(), "https://unused.invalid/bridge" + (trailing ? "/" : ""), (leading ? "/" : "") + "v2/");
            using (await client.DownloadUserPictureAsync("context", "current")) { }
            Assert.That(client.Uri.AbsoluteUri, Is.EqualTo("https://unused.invalid/bridge/v2/user/current/picture"));
        }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void UnusableSelectedIdentityFailsBeforeAuthorization(string id)
        {
            var auth = new Auth(); var client = new Client(auth);
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { using (await client.DownloadUserPictureAsync("context", id)) { } }));
            Assert.That(auth.Calls, Is.Zero); Assert.That(client.Calls, Is.Zero);
        }

        [TestCase(null), TestCase("application/json"), TestCase("image/gif")]
        public void UndocumentedSuccessfulContentTypesFailAndReleaseResponse(string mime)
        {
            var content = new PictureContent(mime); var client = new Client(new Auth()) { Content = content };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { using (await client.DownloadUserPictureAsync("context", "selected")) { } }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidDataException>()); Assert.That(content.Disposed, Is.True); Assert.That(content.Buffered, Is.False);
        }

        [TestCase(204), TestCase(302), TestCase(403), TestCase(404), TestCase(500)]
        public void HttpFailuresAreNotEmptyPicturesAndAreNotReplayed(int status)
        {
            var content = new PictureContent("image/png"); var client = new Client(new Auth()) { Content = content, Status = (HttpStatusCode)status };
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { using (await client.DownloadUserPictureAsync("context", "selected")) { } }));
            Assert.That(client.Calls, Is.EqualTo(1)); Assert.That(content.Disposed, Is.True);
        }

        [Test]
        public void PreCancellationDoesNotAuthorizeOrSend()
        {
            var auth = new Auth(); var client = new Client(auth);
            using (var source = new CancellationTokenSource())
            {
                source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => { using (await client.DownloadUserPictureAsync("context", "selected", source.Token)) { } }));
            }
            Assert.That(auth.Calls, Is.Zero); Assert.That(client.Calls, Is.Zero);
        }

        [Test]
        public async Task ActiveCancellationReachesNativeHttp()
        {
            var auth = new Auth(); var client = new Client(auth) { Pending = true };
            using (var source = new CancellationTokenSource())
            {
                var pending = client.DownloadUserPictureAsync("context", "selected", source.Token); await client.Entered.Task;
                Assert.That(client.Token, Is.EqualTo(source.Token)); Assert.That(auth.Token, Is.EqualTo(source.Token)); source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { using (await pending) { } }));
            }
        }

        [Test]
        public async Task CancellationAfterOpeningDisposesResponseAndCancelsReads()
        {
            var content = new PictureContent("image/png"); var client = new Client(new Auth()) { Content = content };
            using (var source = new CancellationTokenSource())
            using (var stream = await client.DownloadUserPictureAsync("context", "selected", source.Token))
            {
                source.Cancel(); Assert.That(content.Disposed, Is.True);
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await stream.ReadAsync(new byte[1], 0, 1)));
            }
        }

        [TestCase(false), TestCase(true)]
        public async Task FactoryDownloadsOriginalBytesThroughPrivateTcpLoopback(bool sync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null; var payload = new byte[] { 0, 255, 137, 13, 10, 0 };
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException();
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        headers = Encoding.ASCII.GetString(bytes.ToArray());
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token); await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Auth(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.User; client.DisableOfflineModeSimulation();
                    using (var stream = sync ? client.DownloadUserPicture("context", "selected", source.Token) : await client.DownloadUserPictureAsync("context", "selected", source.Token))
                    using (var copy = new MemoryStream()) { await stream.CopyToAsync(copy); Assert.That(copy.ToArray(), Is.EqualTo(payload)); }
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("GET /bridge/v2/user/selected/picture HTTP/1.1"));
                    Assert.That(headers, Does.Contain("Authorization: Bearer fixture"));
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).All(line => line.EndsWith(" 0")), Is.True);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address) { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-user-picture-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(Auth auth, Config config) : base(auth, config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Auth : IAsyncOAuthInfoProvider
        {
            internal int Calls; internal string Context; internal CancellationToken Token;
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native asynchronous authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            { cancellationToken.ThrowIfCancellationRequested(); Calls++; Context = userId; Token = cancellationToken; return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
        }
        private sealed class Client : UserRestClient
        {
            internal PictureContent Content = new PictureContent("image/png"); internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Uri Uri; internal string Accept, Bearer; internal HttpMethod Method; internal bool HasBody, Pending;
            internal CancellationToken Token; internal int Calls;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Client(Auth auth, string address = "https://unused.invalid/bridge/", string prefix = "v2/") : base(auth, new Config(address), null, prefix) { }
            protected override async Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token)
            {
                Calls++; Uri = request.RequestUri; Method = request.Method; HasBody = request.Content != null;
                Accept = request.Headers.Accept.ToString(); Bearer = request.Headers.Authorization.ToString(); Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(Status) { Content = Content };
            }
        }
        private sealed class PictureContent : HttpContent
        {
            internal readonly byte[] Bytes = { 0, 255, 137, 13, 10, 0 }; internal bool Buffered, Disposed;
            internal PictureContent(string mime) { if (mime != null) Headers.ContentType = new MediaTypeHeaderValue(mime); }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) { Buffered = true; return stream.WriteAsync(Bytes, 0, Bytes.Length); }
            protected override bool TryComputeLength(out long length) { length = Bytes.Length; return true; }
            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(Bytes));
            protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
        }
    }
}
