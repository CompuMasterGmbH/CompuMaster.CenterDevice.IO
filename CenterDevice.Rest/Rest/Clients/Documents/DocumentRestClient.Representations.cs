using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CenterDevice.Rest.Exceptions;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Downloads the PDF representation of the selected document.</summary>
        /// <inheritdoc cref="DownloadPdfAsync"/>
        public Stream DownloadPdf(string userId, string id, long? version = null, long? range = null, int? waitForGenerationSeconds = null, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadPdfAsync(userId, id, version, range, waitForGenerationSeconds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens the PDF representation using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="version">The positive historical version number, or null for the current version.</param>
        /// <param name="range">The initial byte offset, or null for the complete representation.</param>
        /// <param name="waitForGenerationSeconds">The optional server generation wait between 1 and 30 seconds.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned stream that the caller must dispose.</returns>
        /// <remarks>Guests can download only the latest visible version. Generation and document-format support depend on the server. Pending generation is distinct from an empty successful result; requests are not automatically retried or redirected. A PDF representation can contain OCR changes and need not match the original PDF.</remarks>
        /// <exception cref="ArgumentException">The document identifier is missing.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The version, offset or generation wait is invalid.</exception>
        /// <exception cref="DocumentRepresentationNotReadyException">The server returns pending generation or an expired generation wait.</exception>
        /// <exception cref="NotAcceptableException">The server does not support the requested representation.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<Stream> DownloadPdfAsync(string userId, string id, long? version = null, long? range = null, int? waitForGenerationSeconds = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => OpenRepresentationAsync(userId, id, version, range, waitForGenerationSeconds, "application/pdf", cancellationToken);

        /// <summary>Downloads the MP4 representation of the selected document.</summary>
        /// <inheritdoc cref="DownloadMp4Async"/>
        public Stream DownloadMp4(string userId, string id, long? version = null, long? range = null, int? waitForGenerationSeconds = null, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadMp4Async(userId, id, version, range, waitForGenerationSeconds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens the MP4 representation using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="version">The positive historical version number, or null for the current version.</param>
        /// <param name="range">The initial byte offset, or null for the complete representation.</param>
        /// <param name="waitForGenerationSeconds">The optional server generation wait between 1 and 30 seconds.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned stream that the caller must dispose.</returns>
        /// <remarks>Guests can download only the latest visible version. Generation and document-format support depend on the server. Pending generation is distinct from an empty successful result; requests are not automatically retried or redirected.</remarks>
        /// <exception cref="ArgumentException">The document identifier is missing.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The version, offset or generation wait is invalid.</exception>
        /// <exception cref="DocumentRepresentationNotReadyException">The server returns pending generation or an expired generation wait.</exception>
        /// <exception cref="NotAcceptableException">The server does not support the requested representation.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<Stream> DownloadMp4Async(string userId, string id, long? version = null, long? range = null, int? waitForGenerationSeconds = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => OpenRepresentationAsync(userId, id, version, range, waitForGenerationSeconds, "video/mp4", cancellationToken);

        /// <summary>Downloads the fulltext representation of the selected document.</summary>
        /// <inheritdoc cref="DownloadFulltextAsync"/>
        public Stream DownloadFulltext(string userId, string id, long? version = null, int? waitForGenerationSeconds = null, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadFulltextAsync(userId, id, version, waitForGenerationSeconds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens the fulltext representation using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="version">The positive historical version number, or null for the current version.</param>
        /// <param name="waitForGenerationSeconds">The optional server generation wait between 1 and 30 seconds.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned stream that the caller must dispose.</returns>
        /// <remarks>Guests can download only the latest visible version. Generation and document-format support depend on the server. Pending generation is distinct from an empty successful result; requests are not automatically retried or redirected. Empty fulltext is a valid success, and an original plain-text file is returned as its fulltext.</remarks>
        /// <exception cref="ArgumentException">The document identifier is missing.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The version or generation wait is invalid.</exception>
        /// <exception cref="DocumentRepresentationNotReadyException">The server returns pending generation or an expired generation wait.</exception>
        /// <exception cref="NotAcceptableException">The server does not support the requested representation.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<Stream> DownloadFulltextAsync(string userId, string id, long? version = null, int? waitForGenerationSeconds = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => OpenRepresentationAsync(userId, id, version, null, waitForGenerationSeconds, "text/plain", cancellationToken);

        private async Task<Stream> OpenRepresentationAsync(string userId, string id, long? version, long? range,
            int? waitForGenerationSeconds, string mediaType, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (waitForGenerationSeconds < 1 || waitForGenerationSeconds > 30)
                throw new ArgumentOutOfRangeException(nameof(waitForGenerationSeconds));
            if (id == "." || id == "..") throw new ArgumentException("A document identifier must not be a relative path segment.", nameof(id));
            var path = URI_RESOURCE + Uri.EscapeDataString(id);
            if (version.HasValue) path += ";version=" + version.Value.ToString(CultureInfo.InvariantCulture);
            var uri = BuildResourceDownloadUri(path);
            if (waitForGenerationSeconds.HasValue)
                uri = new Uri(uri.AbsoluteUri + "?wait-for-generation=" + waitForGenerationSeconds.Value.ToString(CultureInfo.InvariantCulture));
            var stream = await OpenResourceDownloadAsync(userId, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
                if (range.HasValue) request.Headers.Range = new RangeHeaderValue(range, null);
                return request;
            }, range.HasValue ? HttpStatusCode.PartialContent : HttpStatusCode.OK, new[] { mediaType }, true, token).ConfigureAwait(false);
            try { return DocumentStreamUtils.WrapDownloadStream(stream, streamWrapper); }
            catch { stream.Dispose(); throw; }
        }
    }
}
