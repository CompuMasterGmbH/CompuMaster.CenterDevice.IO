using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class ResourceZipDownloadTests
    {
        [TestCase("collection", false, false), TestCase("collection", true, false)]
        [TestCase("collection", false, true), TestCase("collection", true, true)]
        [TestCase("folder", false, false), TestCase("folder", true, false)]
        public async Task ArchivesUseExactPostContractsAndLazyResponseOwnedStreams(string resource, bool sync, bool structure)
        {
            var state = new State();
            using (var stream = await Open(state, resource, sync, "selected", structure))
            {
                Assert.That(state.Uri.AbsoluteUri, Is.EqualTo("https://unused.test/bridge/v2/" + resource + "/selected"));
                Assert.That(state.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(state.Authorization, Is.EqualTo("Bearer isolated-token"));
                Assert.That(state.UserId, Is.EqualTo("selected-context"));
                Assert.That(state.Accept, Is.EqualTo("application/zip"));
                Assert.That(state.RequestType, Does.StartWith("application/json"));
                var body = JObject.Parse(state.Json);
                Assert.That((string)body["action"], Is.EqualTo("download"));
                if (resource == "collection")
                {
                    Assert.That(body.Count, Is.EqualTo(2));
                    Assert.That(((JObject)body["params"]).Count, Is.EqualTo(1));
                    Assert.That((bool)body["params"]["include-folder-structure"], Is.EqualTo(structure));
                }
                else Assert.That(body.Count, Is.EqualTo(1), "The folder contract has no params object.");
                Assert.That(state.Content.Buffered, Is.False);
                Assert.That(state.Content.Source.Reads, Is.Zero);
                Assert.That(stream.Length, Is.EqualTo(5L * 1024 * 1024 * 1024));
                Assert.That(await stream.ReadAsync(new byte[8], 0, 8), Is.EqualTo(8));
                Assert.That(state.Content.Source.Reads, Is.EqualTo(1));
            }
            Assert.That(state.Content.Disposed, Is.True);
            Assert.That(state.Content.Source.Disposed, Is.True);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("collection"), TestCase("folder")]
        public async Task ResourceIdentifiersCannotInjectPathsOrQueries(string resource)
        {
            var state = new State();
            using (await Open(state, resource, false, "part/with?query=one#fragment")) { }
            Assert.That(state.Uri.AbsoluteUri, Is.EqualTo("https://unused.test/bridge/v2/" + resource + "/part%2Fwith%3Fquery%3Done%23fragment"));
            Assert.That(state.Uri.Query, Is.Empty);
            Assert.That(state.Uri.Fragment, Is.Empty);
        }

        [TestCase("collection", null), TestCase("folder", null)]
        [TestCase("collection", ""), TestCase("folder", "")]
        [TestCase("collection", " "), TestCase("folder", " ")]
        [TestCase("collection", "."), TestCase("folder", ".")]
        [TestCase("collection", ".."), TestCase("folder", "..")]
        public void InvalidIdentitiesFailBeforeAuthorization(string resource, string id)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { using (await Open(state, resource, false, id)) { } }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("collection", 204), TestCase("folder", 204)]
        [TestCase("collection", 307), TestCase("folder", 307)]
        [TestCase("collection", 403), TestCase("folder", 403)]
        [TestCase("collection", 404), TestCase("folder", 404)]
        [TestCase("collection", 503), TestCase("folder", 503)]
        public void ErrorsAndRedirectsAreDisposedWithoutReplayOrGenerationAssumptions(string resource, int status)
        {
            var state = new State { Status = (HttpStatusCode)status };
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { using (await Open(state, resource)) { } }));
            Assert.That(state.Calls, Is.EqualTo(1));
            Assert.That(state.Content.Disposed, Is.True);
        }

        [TestCase("collection"), TestCase("folder")]
        public void NonZipSuccessIsRejectedAndDisposed(string resource)
        {
            var state = new State();
            state.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { using (await Open(state, resource)) { } }));
            Assert.That(state.Content.Disposed, Is.True);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("collection", false), TestCase("folder", false)]
        [TestCase("collection", true), TestCase("folder", true)]
        public void PreCancellationDoesNotAuthorizeOrSend(string resource, bool sync)
        {
            var state = new State();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { using (await Open(state, resource, sync, token: cancellation.Token)) { } }));
            }
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("collection"), TestCase("folder")]
        public async Task ActiveCancellationReachesTransport(string resource)
        {
            var state = new State { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                var operation = Open(state, resource, token: cancellation.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { using (await operation) { } }));
            }
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("collection"), TestCase("folder")]
        public async Task CancellationAfterHeadersDisposesBodyAndMakesFutureReadsFail(string resource)
        {
            var state = new State();
            using (var cancellation = new CancellationTokenSource())
            using (var stream = await Open(state, resource, token: cancellation.Token))
            {
                cancellation.Cancel();
                Assert.That(state.Content.Disposed, Is.True);
                Assert.Catch<OperationCanceledException>((Action)(() => { stream.ReadByte(); }));
            }
        }

        private static Task<Stream> Open(State state, string resource, bool sync = false, string id = "selected", bool structure = false, CancellationToken token = default(CancellationToken))
        {
            if (resource == "collection")
            {
                var client = new CollectionClient(state);
                return sync ? Task.FromResult(client.DownloadCollectionAsZip("selected-context", id, structure, token)) : client.DownloadCollectionAsZipAsync("selected-context", id, structure, token);
            }
            var folder = new FolderClient(state);
            return sync ? Task.FromResult(folder.DownloadFolderAsZip("selected-context", id, token)) : folder.DownloadFolderAsZipAsync("selected-context", id, token);
        }

        private sealed class Config : IRestClientConfiguration
        {
            public string BaseAddress => "https://unused.test/bridge/";
            public string UserAgent => "resource-zip-tests";
        }
        private sealed class CollectionClient : CollectionRestClient
        {
            private readonly State state;
            internal CollectionClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token) => state.Send(request, token);
        }
        private sealed class FolderClient : FolderRestClient
        {
            private readonly State state;
            internal FolderClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token) => state.Send(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal int Calls, AuthorizationCalls;
            internal string UserId, Json, Accept, Authorization, RequestType;
            internal Uri Uri;
            internal HttpMethod Method;
            internal CancellationToken Token, AuthorizationToken;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal readonly LazyContent Content = new LazyContent();
            internal bool Pending;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native asynchronous authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                AuthorizationCalls++; UserId = userId; AuthorizationToken = cancellationToken;
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
            internal async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken token)
            {
                Calls++; Uri = request.RequestUri; Method = request.Method; Token = token;
                Accept = request.Headers.Accept.ToString(); Authorization = request.Headers.Authorization.ToString();
                Json = await request.Content.ReadAsStringAsync(); RequestType = request.Content.Headers.ContentType.ToString();
                Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new HttpResponseMessage(Status) { Content = Content };
            }
        }
        private sealed class LazyContent : HttpContent
        {
            internal bool Buffered, Disposed;
            internal readonly LargeStream Source = new LargeStream();
            internal LazyContent() { Headers.ContentType = new MediaTypeHeaderValue("application/zip"); }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) { Buffered = true; return Task.FromResult(0); }
            protected override bool TryComputeLength(out long length) { length = 0; return false; }
            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(Source);
            protected override void Dispose(bool disposing) { if (disposing) { Disposed = true; Source.Dispose(); } base.Dispose(disposing); }
        }
        private sealed class LargeStream : Stream
        {
            internal int Reads;
            internal bool Disposed;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 5L * 1024 * 1024 * 1024;
            public override long Position { get => 0; set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count)
            {
                Reads++; var amount = Math.Min(count, 8);
                for (var i = 0; i < amount; i++) buffer[offset + i] = (byte)'Z';
                return amount;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        }
    }
}
