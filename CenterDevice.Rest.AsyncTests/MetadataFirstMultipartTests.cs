using CenterDevice.Rest.Clients;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class MetadataFirstMultipartTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OrdersUploadWithoutTakingOwnershipAndRestoresRequestAfterSuccessOrCancellation(bool cancel)
        {
            var document = new TrackedContent("document");
            var metadata = new TrackedContent("metadata");
            var original = new MultipartFormDataContent("fixture-boundary");
            original.Add(document);
            original.Add(metadata);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://" + Guid.NewGuid().ToString("N") + ".invalid/documents") { Content = original };
            using (var cancellation = new CancellationTokenSource())
            using (var transport = new InspectingHandler(message =>
            {
                var parts = ((MultipartFormDataContent)message.Content).ToArray();
                Assert.That(parts.Select(p => p.Headers.ContentDisposition.Name.Trim('"')), Is.EqualTo(new[] { "metadata", "document" }));
                if (cancel) { cancellation.Cancel(); throw new OperationCanceledException(cancellation.Token); }
            }))
            using (var invoker = new HttpMessageInvoker(new CenterDeviceHttpMessageHandler(transport)))
            {
                try
                {
                    if (cancel) Assert.ThrowsAsync<OperationCanceledException>(new Func<Task>(async () => await invoker.SendAsync(request, cancellation.Token)));
                    else using (var response = await invoker.SendAsync(request, cancellation.Token)) { }
                    Assert.That(transport.Calls, Is.EqualTo(1), "A document write must never replay.");
                    Assert.That(request.Content, Is.SameAs(original));
                    Assert.That(document.Disposals, Is.Zero);
                    Assert.That(metadata.Disposals, Is.Zero);
                }
                finally { request.Dispose(); }
                Assert.That(document.Disposals, Is.EqualTo(1));
                Assert.That(metadata.Disposals, Is.EqualTo(1));
            }
        }

        [TestCase("unrelated"), TestCase("extra"), TestCase("ordered")]
        public async Task LeavesOtherMultipartContractsUntouched(string shape)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://" + Guid.NewGuid().ToString("N") + ".invalid/" + (shape == "unrelated" ? "other" : "documents")))
            {
                var original = new MultipartFormDataContent("fixture-boundary");
                request.Content = original;
                if (shape == "ordered") { original.Add(new TrackedContent("metadata")); original.Add(new TrackedContent("document")); }
                else { original.Add(new TrackedContent("document")); original.Add(new TrackedContent("metadata")); }
                if (shape == "extra") original.Add(new TrackedContent("extra"));
                using (var transport = new InspectingHandler(message => Assert.That(message.Content, Is.SameAs(original))))
                using (var invoker = new HttpMessageInvoker(new CenterDeviceHttpMessageHandler(transport)))
                using (var response = await invoker.SendAsync(request, CancellationToken.None))
                    Assert.That(transport.Calls, Is.EqualTo(1));
            }
        }

        private sealed class InspectingHandler : HttpMessageHandler
        {
            private readonly Action<HttpRequestMessage> inspect;
            internal int Calls;
            internal InspectingHandler(Action<HttpRequestMessage> inspect) { this.inspect = inspect; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                inspect(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}") });
            }
        }

        private sealed class TrackedContent : HttpContent
        {
            internal int Disposals;
            internal TrackedContent(string name)
            {
                Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = name };
                Headers.ContentType = new MediaTypeHeaderValue(name == "metadata" ? "application/json" : "application/octet-stream");
            }
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) => stream.WriteAsync(new byte[] { 1 }, 0, 1);
            protected override bool TryComputeLength(out long length) { length = 1; return true; }
            protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); }
        }
    }
}
