using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using RestSharp;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class NativeClientTests
    {
        [Test]
        public async Task UploadReusesItsLengthProbeAndDisposesTheOwnedStream()
        {
            var stream = new LargeStream();
            var client = new FakeDocumentClient(new AsyncAuthorization());
            var calls = 0;
            using (var cancellation = new CancellationTokenSource())
            {
                await client.UploadNewVersionAsync("user", "document", "a\"quoted.txt", () => { calls++; return stream; }, cancellation.Token);
                Assert.That(client.RequestToken, Is.EqualTo(cancellation.Token));
            }
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(stream.Disposals, Is.EqualTo(1));
            var metadata = JObject.Parse(client.Request.Parameters.First(p => p.Name == "metadata").Value.ToString());
            Assert.That(metadata["metadata"]["document"]["filename"].Value<string>(), Is.EqualTo("a\"quoted.txt"));
            Assert.That(metadata["metadata"]["document"]["size"].Value<long>(), Is.EqualTo(5L * 1024 * 1024 * 1024));
        }

        [Test]
        public void CancelledUploadDoesNotOpenAStream()
        {
            var calls = 0;
            var client = new FakeDocumentClient(new AsyncAuthorization());
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () =>
                {
                    await client.UploadNewVersionAsync("user", "document", "file", () => { calls++; return new LargeStream(); }, source.Token);
                }));
            }
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public async Task MetadataUsesAsyncAuthorizationAndForwardsTheRequestToken()
        {
            var auth = new AsyncAuthorization();
            var client = new FakeDocumentClient(auth);
            using (var source = new CancellationTokenSource())
            {
                await client.GetDocumentMetadataAsync("user", "document", source.Token);
                Assert.That(auth.AsyncCalls, Is.EqualTo(1));
                Assert.That(client.RequestToken, Is.EqualTo(source.Token));
                Assert.That(client.Request.Resource, Is.EqualTo("v2/document/document"));
                Assert.That(client.Request.Method, Is.EqualTo(Method.Get));
            }
        }

        [Test]
        public async Task DownloadsReturnAnUnbufferedLargeStreamAndOwnTheResponse()
        {
            var content = new LazyLargeContent();
            var client = new FakeDocumentClient(new AsyncAuthorization()) { DownloadContent = content };
            using (var stream = await client.DownloadDocumentAsync("user", "document"))
            {
                Assert.That(content.Serialized, Is.False, "Opening a download must not buffer its body.");
                Assert.That(stream.Length, Is.EqualTo(5L * 1024 * 1024 * 1024));
                var buffer = new byte[16];
                Assert.That(await stream.ReadAsync(buffer, 0, buffer.Length), Is.EqualTo(buffer.Length));
            }
            Assert.That(content.Disposed, Is.True);
        }

        [Test]
        public async Task CancellationAfterDownloadHeadersAbortsTheResponseAndStream()
        {
            var content = new LazyLargeContent();
            var client = new FakeDocumentClient(new AsyncAuthorization()) { DownloadContent = content };
            using (var source = new CancellationTokenSource())
            using (var stream = await client.DownloadDocumentAsync("user", "document", cancellationToken: source.Token))
            {
                source.Cancel();
                Assert.That(content.Disposed, Is.True);
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () =>
                {
                    await stream.ReadAsync(new byte[16], 0, 16);
                }));
            }
        }

        [TestCase(404, typeof(NotFoundException)), TestCase(406, typeof(NotAcceptableException)),
         TestCase(416, typeof(RequestedRangeNotSatisfiableException)), TestCase(500, typeof(InternalServerErrorException))]
        public void DownloadErrorsDisposeTheirResponses(int status, Type exception)
        {
            var content = new LazyLargeContent(false);
            var client = new FakeDocumentClient(new AsyncAuthorization()) { DownloadContent = content, DownloadStatus = (HttpStatusCode)status };
            Assert.ThrowsAsync(exception, (Func<Task>)(async () =>
            {
                using (await client.DownloadPreviewAsync("user", "document", PreviewSize._120, null)) { }
            }));
            Assert.That(content.Disposed, Is.True);
        }

        private sealed class Configuration : IRestClientConfiguration
        {
            public string BaseAddress => "https://unused.test/";
            public string UserAgent => "isolated-native-tests";
        }

        private sealed class AsyncAuthorization : IAsyncOAuthInfoProvider
        {
            internal int AsyncCalls;
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("The synchronous authorization callback was used.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AsyncCalls++;
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-fixture" });
            }
        }

        private sealed class FakeDocumentClient : DocumentRestClient
        {
            internal CancellationToken RequestToken;
            internal RestRequest Request;
            internal HttpContent DownloadContent;
            internal HttpStatusCode DownloadStatus = HttpStatusCode.OK;
            internal FakeDocumentClient(IOAuthInfoProvider auth) : base(auth, new Configuration(), null, null, "v2/") { }
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                Request = request;
                RequestToken = token;
                if (request.Files.Any())
                {
                    using (var body = request.Files.First().GetFile()) body.Read(new byte[16], 0, 16);
                }
                return Task.FromResult(new RestResponse<T>(request) { StatusCode = request.Method == Method.Post ? HttpStatusCode.Created : HttpStatusCode.OK, Data = new T() });
            }
            protected override Task<HttpResponseMessage> SendDownloadRequestAsync(HttpRequestMessage request, CancellationToken token)
                => Task.FromResult(new HttpResponseMessage(DownloadStatus) { Content = DownloadContent });
        }

        private sealed class LazyLargeContent : HttpContent
        {
            internal bool Serialized, Disposed;
            private readonly bool large;
            internal LazyLargeContent(bool large = true) { this.large = large; }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            { Serialized = true; return stream.WriteAsync(new byte[] { 1 }, 0, 1); }
            protected override bool TryComputeLength(out long length) { length = large ? 5L * 1024 * 1024 * 1024 : 1; return true; }
            protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new LargeStream());
            protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
        }

        private sealed class LargeStream : Stream
        {
            internal int Disposals;
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 5L * 1024 * 1024 * 1024;
            public override long Position { get; set; }
            public override int Read(byte[] buffer, int offset, int count) { Position += count; return count; }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            { token.ThrowIfCancellationRequested(); return Task.FromResult(Read(buffer, offset, count)); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
        }
    }
}
