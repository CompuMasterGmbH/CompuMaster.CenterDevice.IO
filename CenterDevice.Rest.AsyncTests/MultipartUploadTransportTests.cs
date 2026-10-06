using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using NUnit.Framework;
using RestSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class MultipartUploadTransportTests
    {
        [TestCase(false), TestCase(true)]
        public async Task NewDocumentUploadUsesChunkedMultipartThroughTheRealRequestPipeline(bool fromFile)
        {
            var origin = "https://" + Guid.NewGuid().ToString("N") + ".invalid/";
            var payload = new byte[] { 0, 17, 128, 255 };
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(path, payload);
                var transport = new CaptureTransport();
                using (var http = CenterDeviceHttpTransport.CreateHttpClient(transport))
                using (var rest = new RestClient(http, new RestClientOptions(origin)))
                {
                    var client = new DocumentsRestClient(new Authorization(), new Configuration(origin), null, null, "v2/");
                    ReplaceTransport(client, rest);
                    if (fromFile)
                        await client.UploadDocumentAsync("user", "fixture.bin", path, "collection", "folder", CancellationToken.None);
                    else
                        await client.UploadDocumentAsync("user", "fixture.bin", () => new MemoryStream(payload, false), "collection", "folder", CancellationToken.None);
                    AssertUpload(transport, payload);
                    Assert.That(transport.Path, Is.EqualTo("/v2/documents"));
                }
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void SynchronousVersionUploadUsesTheSameChunkedMultipartTransport()
        {
            var origin = "https://" + Guid.NewGuid().ToString("N") + ".invalid/";
            var payload = new byte[] { 0, 17, 128, 255 };
            var transport = new CaptureTransport();
            using (var http = CenterDeviceHttpTransport.CreateHttpClient(transport))
            using (var rest = new RestClient(http, new RestClientOptions(origin)))
            {
                var client = new DocumentRestClient(new Authorization(), new Configuration(origin), null, null, "v2/");
                ReplaceTransport(client, rest);
                client.UploadNewVersion("user", "document", "fixture.bin", () => new MemoryStream(payload, false), CancellationToken.None);
                AssertUpload(transport, payload);
                Assert.That(transport.Path, Does.Contain("document"));
            }
        }

        private static void AssertUpload(CaptureTransport transport, byte[] payload)
        {
            Assert.That(transport.Calls, Is.EqualTo(1), "An upload must not replay a write.");
            Assert.That(transport.MediaType, Is.EqualTo("multipart/form-data"));
            Assert.That(transport.PartNames, Is.EquivalentTo(new[] { "metadata", "document" }));
            Assert.That(transport.Document, Is.EqualTo(payload));
            Assert.That(transport.Metadata, Does.Contain("fixture.bin"));
            // Public REST API 2.29, section 5.2.1: multipart upload request uses chunked transfer.
            Assert.That(transport.Chunked, Is.True, "The upload request must use the documented streaming transfer framing.");
        }

        private static void ReplaceTransport(CenterDeviceRestClient client, RestClient replacement)
        {
            client.DisableOfflineModeSimulation();
            var field = typeof(CenterDeviceRestClient).GetField("client", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            ((RestClient)field.GetValue(client)).Dispose();
            field.SetValue(client, replacement);
        }

        private sealed class Authorization : IAsyncOAuthInfoProvider
        {
            public OAuthInfo GetOAuthInfo(string userId) => new OAuthInfo { UserId = userId, access_token = "fixture-token" };
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(GetOAuthInfo(userId));
            }
        }

        private sealed class Configuration : IRestClientConfiguration
        {
            internal Configuration(string origin) { BaseAddress = origin; }
            public string BaseAddress { get; }
            public string UserAgent => "Isolated multipart fixture";
        }

        private sealed class CaptureTransport : HttpMessageHandler
        {
            internal int Calls;
            internal bool? Chunked;
            internal string MediaType, Path, Metadata;
            internal string[] PartNames;
            internal byte[] Document;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                Chunked = request.Headers.TransferEncodingChunked;
                MediaType = request.Content.Headers.ContentType.MediaType;
                Path = request.RequestUri.AbsolutePath;
                var parts = (MultipartFormDataContent)request.Content;
                PartNames = parts.Select(part => part.Headers.ContentDisposition.Name.Trim('"')).ToArray();
                Metadata = await parts.Single(part => part.Headers.ContentDisposition.Name.Trim('"') == "metadata").ReadAsStringAsync();
                Document = await parts.Single(part => part.Headers.ContentDisposition.Name.Trim('"') == "document").ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"id\":\"fixture-document\"}", System.Text.Encoding.UTF8, "application/json") };
            }
        }
    }
}
