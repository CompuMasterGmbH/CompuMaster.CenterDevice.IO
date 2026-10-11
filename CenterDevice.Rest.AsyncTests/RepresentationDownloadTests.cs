using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using RestSharp;
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
    public class RepresentationDownloadTests
    {
        [TestCase("pdf", false), TestCase("pdf", true), TestCase("mp4", false), TestCase("mp4", true),
         TestCase("fulltext", false), TestCase("fulltext", true)]
        public async Task SelectedVersionUsesMimeNegotiationAndResponseOwnership(string format, bool sync)
        {
            var auth = new Auth();
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue(Mime(format));
            var client = new Client(auth) { Content = content };
            using (var source = new CancellationTokenSource())
            using (var stream = await Open(client, format, sync, 4294967297L, 30, source.Token))
            {
                Assert.That(client.Uri.AbsoluteUri, Is.EqualTo("https://unused.test/bridge/v2/document/selected;version=4294967297?wait-for-generation=30"));
                Assert.That(client.Accept, Is.EqualTo(Mime(format)));
                Assert.That(client.Method, Is.EqualTo(HttpMethod.Get));
                Assert.That(client.Authorization, Is.EqualTo("Bearer fixture"));
                Assert.That(auth.UserId, Is.EqualTo("auth-context"));
                Assert.That(auth.Calls, Is.EqualTo(1));
                Assert.That(content.Serialized, Is.False);
                Assert.That(stream.Length, Is.EqualTo(5L * 1024 * 1024 * 1024));
                Assert.That(await stream.ReadAsync(new byte[8], 0, 8), Is.EqualTo(8));
            }
            Assert.That(content.Disposed, Is.True);
        }

        [Test]
        public async Task CurrentVersionOmitsGenerationAndVersionParameters()
        {
            var client = PdfClient();
            using (await client.DownloadPdfAsync("auth-context", "selected")) { }
            Assert.That(client.Uri.AbsoluteUri, Is.EqualTo("https://unused.test/bridge/v2/document/selected"));
        }

        [TestCase("pdf"), TestCase("mp4")]
        public async Task PartialRepresentationsUseAndValidateTheSelectedByteOffset(string format)
        {
            var client = new Client(new Auth()) { Status = HttpStatusCode.PartialContent, Content = new StringContent("abcd") };
            client.Content.Headers.ContentType = new MediaTypeHeaderValue(Mime(format));
            client.Content.Headers.ContentRange = new ContentRangeHeaderValue(4, 7, 8);
            using (format == "pdf" ? await client.DownloadPdfAsync("user", "selected", range: 4) :
                await client.DownloadMp4Async("user", "selected", range: 4)) { }
            Assert.That(client.Range, Is.EqualTo("bytes=4-"));
        }

        [TestCase(200, -1), TestCase(206, -1), TestCase(206, 3)]
        public void IncorrectPartialStatusOrRangeIsRejectedAndDisposed(int status, int start)
        {
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            if (start >= 0) content.Headers.ContentRange = new ContentRangeHeaderValue(start, 7, 8);
            var client = new Client(new Auth()) { Content = content, Status = (HttpStatusCode)status };
            ThrowsAsync(status == 200 ? typeof(RestClientException) : typeof(InvalidResponseDataException),
                async () => { using (await client.DownloadPdfAsync("user", "id", range: 4)) { } });
            Assert.That(content.Disposed, Is.True);
            Assert.That(content.Serialized, Is.False);
        }

        [Test]
        public async Task EmptySuccessfulFulltextIsNotPendingGeneration()
        {
            var client = new Client(new Auth()) { Content = new StringContent("") };
            using (var stream = await client.DownloadFulltextAsync("user", "id"))
                Assert.That(stream.ReadByte(), Is.EqualTo(-1));
        }

        [TestCase("pdf", 204), TestCase("pdf", 307), TestCase("mp4", 204),
         TestCase("mp4", 307), TestCase("fulltext", 204), TestCase("fulltext", 307)]
        public void GenerationStatesRemainTypedAndNeverRetry(string format, int status)
        {
            var content = new LazyContent();
            var client = new Client(new Auth()) { Content = content, Status = (HttpStatusCode)status,
                Location = new Uri("/bridge/v2/document/selected?wait-for-generation=12", UriKind.Relative) };
            var error = ThrowsAsync<DocumentRepresentationNotReadyException>(
                async () => { using (await Open(client, format, false)) { } });
            Assert.That(error.StatusCode, Is.EqualTo((HttpStatusCode)status));
            Assert.That(error.RecommendedWaitSeconds, Is.EqualTo(status == 307 ? (int?)12 : null));
            Assert.That(client.Calls, Is.EqualTo(1));
            Assert.That(content.Serialized, Is.False);
            Assert.That(content.Disposed, Is.True);
        }

        [TestCase("https://other.test/bridge/v2/document/selected?wait-for-generation=10")]
        [TestCase("https://unused.test/bridge/v2/document/other?wait-for-generation=10")]
        [TestCase("/bridge/v2/document/selected?wait-for-generation=0")]
        [TestCase("/bridge/v2/document/selected?wait-for-generation=31")]
        [TestCase("/bridge/v2/document/selected?wait-for-generation=1&wait-for-generation=2")]
        [TestCase("/bridge/v2/document/selected?wait-for-generation=10#fragment")]
        public void UnsafeOrInvalidGenerationHintsDoNotBecomeFollowupInstructions(string location)
        {
            var client = PdfClient();
            client.Status = HttpStatusCode.TemporaryRedirect;
            client.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            var error = ThrowsAsync<DocumentRepresentationNotReadyException>(
                async () => { using (await client.DownloadPdfAsync("user", "selected")) { } });
            Assert.That(error.RecommendedWaitSeconds, Is.Null);
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [TestCase("id"), TestCase("version"), TestCase("range"), TestCase("wait0"), TestCase("wait31"), TestCase("path")]
        public void InvalidArgumentsFailBeforeAuthorization(string invalid)
        {
            var auth = new Auth();
            var client = PdfClient(auth);
            ThrowsAsync(invalid == "id" || invalid == "path" ? typeof(ArgumentException) : typeof(ArgumentOutOfRangeException),
                async () => { using (await client.DownloadPdfAsync("user", invalid == "id" ? " " : invalid == "path" ? ".." : "id",
                    invalid == "version" ? (long?)0 : null, invalid == "range" ? (long?)-1 : null,
                    invalid == "wait0" ? (int?)0 : invalid == "wait31" ? 31 : (int?)null)) { } });
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [Test]
        public void PreCanceledDownloadDoesNotAuthorize()
        {
            var auth = new Auth();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                ThrowsAsync<OperationCanceledException>(async () =>
                { using (await PdfClient(auth).DownloadPdfAsync("user", "id", cancellationToken: source.Token)) { } });
            }
            Assert.That(auth.Calls, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public async Task CancellationAfterHeadersAbortsTheOwnedBody(bool sync)
        {
            var client = PdfClient();
            var content = (LazyContent)client.Content;
            using (var source = new CancellationTokenSource())
            using (var stream = sync ? client.DownloadPdf("user", "id", cancellationToken: source.Token)
                : await client.DownloadPdfAsync("user", "id", cancellationToken: source.Token))
            {
                source.Cancel();
                Assert.That(content.Disposed, Is.True);
                Assert.Throws<OperationCanceledException>((Action)(() => stream.ReadByte()));
                ThrowsAsync<OperationCanceledException>(async () => await stream.ReadAsync(new byte[8], 0, 8));
            }
        }

        [Test]
        public void ActiveHeaderCancellationIsForwardedWithoutReplay()
        {
            using (var source = new CancellationTokenSource())
            {
                var client = PdfClient();
                client.OnSend = async token => { source.Cancel(); await Task.Delay(10, token); };
                ThrowsAsync<OperationCanceledException>(async () =>
                { using (await client.DownloadPdfAsync("user", "id", cancellationToken: source.Token)) { } });
                Assert.That(client.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)),
         TestCase(406, typeof(NotAcceptableException)), TestCase(416, typeof(RequestedRangeNotSatisfiableException)),
         TestCase(500, typeof(InternalServerErrorException))]
        public void ServerErrorsKeepTheirClassificationAndDisposeTheBody(int status, Type type)
        {
            var content = new LazyContent(false);
            var client = new Client(new Auth()) { Status = (HttpStatusCode)status, Content = content };
            ThrowsAsync(type, async () => { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(content.Disposed, Is.True);
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [Test]
        public void RateLimitsExposeTheWaitWithoutReplay()
        {
            var client = PdfClient();
            client.Status = (HttpStatusCode)429;
            client.Content = new StringContent("");
            var error = ThrowsAsync<TooManyRequestsException>(async () =>
            { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(error.RetryAfter, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [TestCase(null), TestCase("text/html")]
        public void SuccessfulWrongMediaTypeDoesNotExposeTheWrongRepresentation(string mediaType)
        {
            var client = PdfClient();
            client.Content.Headers.ContentType = mediaType == null ? null : new MediaTypeHeaderValue(mediaType);
            var error = ThrowsAsync<InvalidResponseDataException>(async () =>
            { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(error.InnerException, Is.InstanceOf<InvalidDataException>());
            Assert.That(((LazyContent)client.Content).Disposed, Is.True);
        }

        [Test]
        public void FailedCustomStreamWrappingDisposesTheOwnedResponse()
        {
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            var client = new Client(new Auth(), new FailingWrapper()) { Content = content };
            ThrowsAsync<IOException>(async () => { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(content.Disposed, Is.True);
        }

        [Test]
        public void LargeErrorBodiesAreBoundedWithoutBufferingTheDocument()
        {
            var content = new LazyContent();
            var client = new Client(new Auth()) { Status = HttpStatusCode.InternalServerError, Content = content };
            ThrowsAsync<InternalServerErrorException>(async () => { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(content.Serialized, Is.False);
            Assert.That(content.Source.Position, Is.GreaterThan(0).And.LessThan(65536));
            Assert.That(content.Disposed, Is.True);
        }

        [Test]
        public void TransportFailuresPreserveTheCauseAndDoNotRetry()
        {
            var failure = new IOException("isolated transport failure");
            var client = PdfClient();
            client.OnSend = token => throw failure;
            var error = ThrowsAsync<IOException>(async () => { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(error, Is.SameAs(failure));
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ASecondExpiredTokenResponseDoesNotRefreshAgain()
        {
            var refresh = new Refresh();
            var client = new Client(new Auth(), handler: refresh);
            client.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Unknown or expired token") });
            client.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Unknown or expired token") });
            ThrowsAsync<UnauthorizedException>(async () => { using (await client.DownloadPdfAsync("user", "id")) { } });
            Assert.That(refresh.Calls, Is.EqualTo(1));
            Assert.That(client.Calls, Is.EqualTo(2));
        }

        [Test]
        public async Task ExpiredTokenRefreshReleasesTheRejectedResponseBeforeTheSecondRead()
        {
            var refresh = new Refresh();
            var client = new Client(new Auth(), handler: refresh);
            var expired = new LazyContent(false, "Unknown or expired token");
            client.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = expired });
            var good = new StringContent("pdf");
            good.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            client.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = good });
            refresh.BeforeRefresh = () => Assert.That(expired.Disposed, Is.True);
            using (await client.DownloadPdfAsync("user", "id")) { }
            Assert.That(refresh.Calls, Is.EqualTo(1));
            Assert.That(client.Calls, Is.EqualTo(2));
            Assert.That(client.Authorization, Is.EqualTo("Bearer replacement"));
        }

        [Test]
        public async Task ActualTransportDoesNotFollowGenerationRedirects()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            int requests = 0;
            var server = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        using (var connection = await listener.AcceptTcpClientAsync())
                        using (var stream = connection.GetStream())
                        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                        {
                            var line = await reader.ReadLineAsync();
                            var target = line.Split(' ')[1];
                            while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                            Interlocked.Increment(ref requests);
                            var response = target == "/target"
                                ? "HTTP/1.1 200 OK\r\nContent-Type: application/pdf\r\nContent-Length: 1\r\nConnection: close\r\n\r\nx"
                                : "HTTP/1.1 307 Temporary Redirect\r\nLocation: /target\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                            var bytes = Encoding.ASCII.GetBytes(response);
                            await stream.WriteAsync(bytes, 0, bytes.Length);
                        }
                    }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            });
            try
            {
                using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var client = new DocumentRestClient(new Auth(), new Configuration("http://127.0.0.1:" + port + "/"), null, null, "v2/");
                    var error = ThrowsAsync<DocumentRepresentationNotReadyException>(async () =>
                    { using (await client.DownloadPdfAsync("user", "id", cancellationToken: source.Token)) { } });
                    Assert.That(error.RecommendedWaitSeconds, Is.Null);
                    Assert.That(requests, Is.EqualTo(1));
                }
            }
            finally { listener.Stop(); await server; }
        }

        [TestCase(false, "image/png"), TestCase(true, "image/png"), TestCase(false, "image/jpeg"), TestCase(true, "image/jpeg")]
        public async Task SinglePagePreviewNegotiatesBothImageFormatsAndKeepsServerMetadata(bool sync, string mime)
        {
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue(mime);
            var client = new Client(new Auth()) { Content = content };
            using (var preview = sync ? client.DownloadPreviews("user", "id", PreviewSize._1080, "5", 7, 30, true)
                : await client.DownloadPreviewsAsync("user", "id", PreviewSize._1080, "5", 7, 30, true))
            {
                Assert.That(client.Uri.AbsoluteUri, Is.EqualTo("https://unused.test/bridge/v2/document/id;version=7;preview=1080x1080;pages=5?wait-for-generation=30&include-error-info=true"));
                Assert.That(client.Accept, Is.EqualTo("image/png, image/jpeg"));
                Assert.That(preview.ContentType, Is.EqualTo(mime));
                Assert.That(content.Serialized, Is.False);
                Assert.That(await preview.Content.ReadAsync(new byte[8], 0, 8), Is.EqualTo(8));
            }
            Assert.That(content.Disposed, Is.True);
        }

        [TestCase("all"), TestCase("1,3-7,10,12"), TestCase("1-5")]
        public async Task MultiplePreviewPagesRetainTheirBoundaryAndDoNotInventMissingParts(string pages)
        {
            const string multipart = "--pages\r\nContent-Disposition: form-data; name=3\r\nContent-Type: image/png\r\n\r\nimage\r\n--pages--\r\n";
            var content = new StringContent(multipart);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=\"pages\"");
            var client = new Client(new Auth()) { Content = content };
            using (var preview = await client.DownloadPreviewsAsync("user", "id", PreviewSize._120, pages))
            using (var reader = new StreamReader(preview.Content))
            {
                Assert.That(preview.ContentType, Does.Contain("boundary=\"pages\""));
                Assert.That(await reader.ReadToEndAsync(), Is.EqualTo(multipart), "Only the server-returned page part is exposed.");
                Assert.That(client.Uri.Query, Is.Empty, "Generation/error options retain absent defaults.");
            }
        }

        [TestCase(""), TestCase("0"), TestCase("-1"), TestCase("5-1"), TestCase("1,,2"), TestCase("all,1"),
         TestCase("1?token=bad"), TestCase("1;version=9"), TestCase("2147483648"), TestCase("1-2-3")]
        public void InvalidPreviewPagesFailBeforeAuthorization(string pages)
        {
            var auth = new Auth();
            var client = new Client(auth);
            ThrowsAsync<ArgumentException>(async () => { using (await client.DownloadPreviewsAsync("user", "id", PreviewSize._120, pages)) { } });
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [TestCase(null), TestCase("\"\""), TestCase("\"invalid[boundary\""), TestCase("\"trailing \"")]
        public void InvalidMultipartBoundariesRejectAndDisposePreviewContent(string boundary)
        {
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data");
            if (boundary != null) content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("boundary", boundary));
            var client = new Client(new Auth()) { Content = content };
            ThrowsAsync<InvalidResponseDataException>(async () =>
            { using (await client.DownloadPreviewsAsync("user", "id", PreviewSize._120, "all")) { } });
            Assert.That(content.Disposed, Is.True);
        }

        [TestCase(204), TestCase(307)]
        public void PreviewGenerationStatesAreNotSuccessfulEmptyImages(int status)
        {
            var client = new Client(new Auth()) { Status = (HttpStatusCode)status, Content = new LazyContent() };
            ThrowsAsync<DocumentRepresentationNotReadyException>(async () =>
            { using (await client.DownloadPreviewsAsync("user", "id", PreviewSize._120)) { } });
            Assert.That(client.Calls, Is.EqualTo(1));
            Assert.That(((LazyContent)client.Content).Disposed, Is.True);
        }

        [Test]
        public void PermanentPreviewFailuresRetainTheManufacturersErrorCode()
        {
            var client = new Client(new Auth()) { Status = HttpStatusCode.NotFound,
                Content = new StringContent("{\"code\":307,\"message\":\"preview generation failed\"}") };
            var error = ThrowsAsync<NotFoundException>(async () =>
            { using (await client.DownloadPreviewsAsync("user", "id", PreviewSize._120, includeErrorInfo: true)) { } });
            Assert.That(error.ErrorResponse.Code, Is.EqualTo(307));
            Assert.That(client.Uri.Query, Is.EqualTo("?include-error-info=true"));
        }

        [Test]
        public void InvalidPreviewResolutionFailsBeforeAuthorization()
        {
            var auth = new Auth();
            ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            { using (await new Client(auth).DownloadPreviewsAsync("user", "id", (PreviewSize)119)) { } });
            Assert.That(auth.Calls, Is.Zero);
        }

        [TestCase(false, false, false), TestCase(false, false, true), TestCase(false, true, false), TestCase(false, true, true),
         TestCase(true, false, false), TestCase(true, false, true), TestCase(true, true, false), TestCase(true, true, true)]
        public async Task ZipDownloadPostsSelectedIdsAndOptionsWithResponseOwnedStreaming(bool sync, bool pdf, bool streamed)
        {
            var auth = new Auth();
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            var client = new ZipClient(auth) { Content = content };
            var ids = new[] { "selected-A", "selected-B" };
            var representation = pdf ? DocumentDownloadRepresentation.Pdf : DocumentDownloadRepresentation.Original;
            using (var stream = sync ? client.DownloadDocumentsAsZip("auth-context", ids, representation, streamed)
                : await client.DownloadDocumentsAsZipAsync("auth-context", ids, representation, streamed))
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(client.Json);
                Assert.That(json["action"].Value<string>(), Is.EqualTo("download"));
                Assert.That(json["params"]["documents"].Values<string>().ToArray(), Is.EqualTo(ids));
                Assert.That(json["params"]["representation"].Value<string>(), Is.EqualTo(pdf ? "pdf" : "original"));
                Assert.That(json["params"]["stream"].Value<bool>(), Is.EqualTo(streamed));
                Assert.That(client.Uri.AbsoluteUri, Is.EqualTo("https://unused.test/bridge/v2/documents"));
                Assert.That(client.Method, Is.EqualTo(HttpMethod.Post));
                Assert.That(client.Accept, Is.EqualTo("application/zip"));
                Assert.That(auth.UserId, Is.EqualTo("auth-context"));
                Assert.That(content.Serialized, Is.False);
                Assert.That(await stream.ReadAsync(new byte[8], 0, 8), Is.EqualTo(8));
            }
            Assert.That(content.Disposed, Is.True);
        }

        [Test]
        public async Task ZipIdsAreEnumeratedAndSnapshottedOnceBeforeAuthorization()
        {
            var ids = new List<string> { "first" };
            int enumerations = 0;
            IEnumerable<string> Enumerate()
            {
                enumerations++;
                foreach (var id in ids) yield return id;
            }
            var auth = new Auth { BeforeReturn = () => ids.Add("later") };
            var client = new ZipClient(auth) { Content = new StringContent("zip") };
            client.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            using (await client.DownloadDocumentsAsZipAsync("user", Enumerate())) { }
            var json = Newtonsoft.Json.Linq.JObject.Parse(client.Json);
            Assert.That(enumerations, Is.EqualTo(1));
            Assert.That(json["params"]["documents"].Values<string>().ToArray(), Is.EqualTo(new[] { "first" }));
            Assert.That(json["params"]["stream"].Value<bool>(), Is.False);
            Assert.That(json["params"]["representation"].Value<string>(), Is.EqualTo("original"));
        }

        [TestCase("null"), TestCase("empty"), TestCase("missing"), TestCase("representation"), TestCase("cancel")]
        public void InvalidZipArgumentsAndPreCancellationFailBeforeAuthorization(string invalid)
        {
            var auth = new Auth();
            var client = new ZipClient(auth);
            using (var source = new CancellationTokenSource())
            {
                if (invalid == "cancel") source.Cancel();
                var type = invalid == "null" ? typeof(ArgumentNullException) : invalid == "representation" ? typeof(ArgumentOutOfRangeException)
                    : invalid == "cancel" ? typeof(OperationCanceledException) : typeof(ArgumentException);
                ThrowsAsync(type, async () =>
                {
                    using (await client.DownloadDocumentsAsZipAsync("user", invalid == "null" ? null : invalid == "empty" ? new string[0]
                        : new[] { invalid == "missing" ? " " : "id" }, invalid == "representation" ? (DocumentDownloadRepresentation)9
                        : DocumentDownloadRepresentation.Original, cancellationToken: source.Token)) { }
                });
            }
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [Test]
        public async Task ZipCancellationAfterHeadersReleasesTheResponse()
        {
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            var client = new ZipClient(new Auth()) { Content = content };
            using (var source = new CancellationTokenSource())
            using (var stream = await client.DownloadDocumentsAsZipAsync("user", new[] { "id" }, cancellationToken: source.Token))
            {
                source.Cancel();
                Assert.That(content.Disposed, Is.True);
                ThrowsAsync<OperationCanceledException>(async () => await stream.ReadAsync(new byte[8], 0, 8));
            }
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        private sealed class ZipClient : DocumentsRestClient
        {
            internal HttpContent Content;
            internal string Json, Accept;
            internal Uri Uri;
            internal HttpMethod Method;
            internal int Calls;
            internal ZipClient(Auth auth) : base(auth, new Configuration(), null, null, "v2/") { }
            protected override async Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token)
            {
                Calls++; Json = await request.Content.ReadAsStringAsync(); Uri = request.RequestUri; Method = request.Method;
                Accept = request.Headers.Accept.ToString();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = Content };
            }
        }

        private static T ThrowsAsync<T>(Func<Task> action) where T : Exception => Assert.ThrowsAsync<T>(action);
        private static Exception ThrowsAsync(Type type, Func<Task> action) => Assert.ThrowsAsync(type, action);
        private static string Mime(string format) => format == "pdf" ? "application/pdf" : format == "mp4" ? "video/mp4" : "text/plain";
        private static Client PdfClient(Auth auth = null)
        {
            var content = new LazyContent();
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return new Client(auth ?? new Auth()) { Content = content };
        }

        private static Task<Stream> Open(Client client, string format, bool sync, long? version = null, int? wait = null, CancellationToken token = default(CancellationToken))
        {
            if (sync) return Task.FromResult(format == "pdf" ? client.DownloadPdf("auth-context", "selected", version, null, wait, token) :
                format == "mp4" ? client.DownloadMp4("auth-context", "selected", version, null, wait, token) :
                client.DownloadFulltext("auth-context", "selected", version, wait, token));
            return format == "pdf" ? client.DownloadPdfAsync("auth-context", "selected", version, null, wait, token) :
                format == "mp4" ? client.DownloadMp4Async("auth-context", "selected", version, null, wait, token) :
                client.DownloadFulltextAsync("auth-context", "selected", version, wait, token);
        }

        private sealed class Configuration : IRestClientConfiguration
        {
            private readonly string address;
            internal Configuration(string address = "https://unused.test/bridge/") { this.address = address; }
            public string BaseAddress => address;
            public string UserAgent => "representation-tests";
        }

        private sealed class Auth : IAsyncOAuthInfoProvider
        {
            internal int Calls;
            internal string UserId;
            internal Action BeforeReturn;
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Must use async auth.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            { cancellationToken.ThrowIfCancellationRequested(); Calls++; UserId = userId; BeforeReturn?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
        }

        private sealed class Client : DocumentRestClient
        {
            internal HttpContent Content;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Uri Uri, Location;
            internal string Accept, Authorization, Range;
            internal HttpMethod Method;
            internal int Calls;
            internal Func<CancellationToken, Task> OnSend;
            internal readonly Queue<HttpResponseMessage> Replies = new Queue<HttpResponseMessage>();
            internal Client(Auth auth, IStreamWrapper wrapper = null, IRestClientErrorHandler handler = null)
                : base(auth, new Configuration(), handler, wrapper, "v2/") { }
            protected override async Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token)
            {
                Calls++; Uri = request.RequestUri; Method = request.Method;
                Accept = request.Headers.Accept.ToString(); Authorization = request.Headers.Authorization.ToString(); Range = request.Headers.Range?.ToString();
                if (OnSend != null) await OnSend(token);
                if (Replies.Count > 0) return Replies.Dequeue();
                var response = new HttpResponseMessage(Status) { Content = Content };
                response.Headers.Location = Location;
                if ((int)Status == 429) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
                return response;
            }
        }

        private sealed class Refresh : IAsyncRestClientErrorHandler
        {
            internal int Calls;
            internal Action BeforeRefresh;
            public void ValidateResponse(RestResponse result) { }
            public OAuthInfo RefreshToken(OAuthInfo info) => throw new AssertionException("Must use async refresh.");
            public Task<OAuthInfo> RefreshTokenAsync(OAuthInfo info, CancellationToken cancellationToken = default(CancellationToken))
            { Calls++; BeforeRefresh?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = info.UserId, access_token = "replacement" }); }
        }

        private sealed class FailingWrapper : IStreamWrapper
        {
            public Stream WrapDownloadStream(Stream stream) => throw new IOException("wrapper failure");
            public Stream WrapUploadStream(Stream stream) => stream;
        }

        private sealed class LazyContent : HttpContent
        {
            internal bool Serialized, Disposed;
            internal LargeStream Source;
            private readonly bool large;
            private readonly string text;
            internal LazyContent(bool large = true, string text = "") { this.large = large; this.text = text; }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            { Serialized = true; var bytes = Encoding.UTF8.GetBytes(text); return stream.WriteAsync(bytes, 0, bytes.Length); }
            protected override bool TryComputeLength(out long length) { length = large ? 5L * 1024 * 1024 * 1024 : Encoding.UTF8.GetByteCount(text); return true; }
            protected override Task<Stream> CreateContentReadStreamAsync()
                => Task.FromResult<Stream>(large ? (Stream)(Source = new LargeStream()) : new MemoryStream(Encoding.UTF8.GetBytes(text)));
            protected override void Dispose(bool disposing) { if (disposing) Disposed = true; base.Dispose(disposing); }
        }

        private sealed class LargeStream : Stream
        {
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
        }
    }
}
