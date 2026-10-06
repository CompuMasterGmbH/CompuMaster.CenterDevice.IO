using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        private static readonly HttpClient DownloadTransport = new HttpClient(
            new CenterDeviceHttpMessageHandler(new HttpClientHandler { UseCookies = false })) { Timeout = Timeout.InfiniteTimeSpan };

        /// <summary>Sends a streaming download request through the shared transport policy.</summary>
        /// <param name="request">The authorized download request.</param>
        /// <param name="cancellationToken">Cancels queue admission and the active HTTP request.</param>
        /// <returns>The response with its body still unbuffered.</returns>
        protected virtual Task<HttpResponseMessage> SendDownloadRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return DownloadTransport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }

        /// <summary>Opens a document download using asynchronous HTTP I/O without buffering the file.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The document identifier.</param>
        /// <param name="version">The requested version, or null for the current version.</param>
        /// <param name="range">The initial byte offset, or null for the complete document.</param>
        /// <param name="cancellationToken">Cancels admission, the HTTP request, and the returned stream.</param>
        /// <returns>A stream that owns the response and must be disposed by the caller.</returns>
        public Task<Stream> DownloadDocumentAsync(string userId, string id, long? version = null, long? range = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            return OpenDownloadAsync(userId, GetDocumentDownloadUri(id, version), range, false, cancellationToken);
        }

        /// <summary>Opens a preview download using asynchronous HTTP I/O without buffering the image.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The document identifier.</param>
        /// <param name="size">The requested preview size.</param>
        /// <param name="version">The requested version, or null for the current version.</param>
        /// <param name="cancellationToken">Cancels admission, the HTTP request, and the returned stream.</param>
        /// <returns>A stream that owns the response and must be disposed by the caller.</returns>
        public Task<Stream> DownloadPreviewAsync(string userId, string id, PreviewSize size, long? version,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var path = URI_RESOURCE + id;
            if (version != null) path += ";" + RestApiConstants.VERSION + "=" + version;
            path += ";preview=" + size.ToApiParameter() + ";pages=1?wait-for-generation=10&include-error-info=false";
            return OpenDownloadAsync(userId, new Uri(new Uri(CustomOptionBaseAddress), path), null, true, cancellationToken);
        }

        private async Task<Stream> OpenDownloadAsync(string userId, Uri uri, long? range, bool preview, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var authorization = await GetOAuthInfoAsync(userId, token).ConfigureAwait(false);
            for (var attempt = 0; ; attempt++)
            {
                HttpResponseMessage response = null;
                CancellationTokenRegistration registration = default(CancellationTokenRegistration);
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        request.Headers.TryAddWithoutValidation(AUTHORIZATION, GetAuthorizationBearer(authorization));
                        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                        if (range != null) request.Headers.Range = new RangeHeaderValue(range, null);
                        if (preview)
                        {
                            request.Headers.TryAddWithoutValidation("Accept", "image/png, image/jpeg");
                            timeout.CancelAfter(PREVIEW_TIMEOUT);
                        }
                        response = await SendDownloadRequestAsync(request, timeout.Token).ConfigureAwait(false);
                    }
                    // Keep cancellation alive after headers have been received. Disposing the response
                    // aborts an active socket read and releases the shared request allowance.
                    registration = token.Register(response.Dispose);
                    token.ThrowIfCancellationRequested();
                    var expected = range == null ? HttpStatusCode.OK : HttpStatusCode.PartialContent;
                    if (response.StatusCode != expected)
                    {
                        var content = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        if (attempt == 0 && response.StatusCode == HttpStatusCode.Unauthorized &&
                            (content == "Unknown or expired token" || content == "Tenant has expired"))
                        {
                            var replacement = await RefreshAuthorizationAsync(authorization, token).ConfigureAwait(false);
                            if (replacement != null)
                            {
                                authorization = replacement;
                                registration.Dispose();
                                response.Dispose();
                                continue;
                            }
                        }
                        if ((int)response.StatusCode == TOO_MANY_REQUESTS)
                            throw new TooManyRequestsException(ExtractDelay(response.Headers.RetryAfter?.ToString()));
                        if (preview && response.StatusCode == HttpStatusCode.NotFound) throw new NotFoundException(content, null);
                        if (preview && response.StatusCode == HttpStatusCode.NotAcceptable) throw new NotAcceptableException(content);
                        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                            throw new RequestedRangeNotSatisfiableException("The requested range cannot be downloaded.");
                        throw RestClientExceptionUtils.CreateDefaultException(new List<HttpStatusCode> { expected }, response.StatusCode, content, null);
                    }
                    var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    var owned = new ResponseDownloadStream(source, response, registration, token);
                    response = null;
                    registration = default(CancellationTokenRegistration);
                    try { return DocumentStreamUtils.WrapDownloadStream(owned, streamWrapper); }
                    catch { owned.Dispose(); throw; }
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    registration.Dispose();
                    response?.Dispose();
                    throw new OperationCanceledException(token);
                }
                catch
                {
                    registration.Dispose();
                    response?.Dispose();
                    throw;
                }
            }
        }
    }

    internal sealed class ResponseDownloadStream : Stream
    {
        private readonly Stream inner;
        private readonly CancellationToken token;
        private HttpResponseMessage response;
        private CancellationTokenRegistration registration;
        internal ResponseDownloadStream(Stream inner, HttpResponseMessage response, CancellationTokenRegistration registration, CancellationToken token)
        { this.inner = inner; this.response = response; this.registration = registration; this.token = token; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            token.ThrowIfCancellationRequested();
            try { return inner.Read(buffer, offset, count); }
            catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken))
            {
                linked.Token.ThrowIfCancellationRequested();
                try { return await inner.ReadAsync(buffer, offset, count, linked.Token).ConfigureAwait(false); }
                catch (Exception) when (linked.IsCancellationRequested) { throw new OperationCanceledException(linked.Token); }
            }
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                registration.Dispose();
                Interlocked.Exchange(ref response, null)?.Dispose();
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
