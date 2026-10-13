using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Downloads explicitly selected current documents through the deprecated GET ZIP endpoint.</summary>
        /// <inheritdoc cref="DownloadDocumentsAsZipLegacyAsync"/>
        [Obsolete("API 2.29 recommends DownloadDocumentsAsZip to avoid URI-length limits.")]
        public Stream DownloadDocumentsAsZipLegacy(string userId, IEnumerable<string> documentIds,
            DocumentDownloadRepresentation representation = DocumentDownloadRepresentation.Original, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadDocumentsAsZipLegacyAsync(userId, documentIds, representation, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens the deprecated selected GET ZIP endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The nonempty explicit selection, copied once before authorization; identifiers containing the comma delimiter cannot be represented safely.</param>
        /// <param name="representation">Original files or already existing PDF representations.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, active HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned ZIP stream that the caller must dispose.</returns>
        /// <remarks>API 2.29 sections 5.2.17/18 recommend the modern POST ZIP workflow because long selections can exceed URI limits. The server includes only visible current documents; successful transport does not establish complete visibility. PDF archives require existing representations and do not trigger conversion. Original selection order and duplicates are retained. No body, automatic redirect or new replay is added. Existing modern methods and defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The selection is null.</exception>
        /// <exception cref="ArgumentException">The selection is empty or contains a missing identifier or comma delimiter.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The representation is unsupported.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        [Obsolete("API 2.29 recommends DownloadDocumentsAsZipAsync to avoid URI-length limits.")]
        public async Task<Stream> DownloadDocumentsAsZipLegacyAsync(string userId, IEnumerable<string> documentIds,
            DocumentDownloadRepresentation representation = DocumentDownloadRepresentation.Original, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = SnapshotDocumentIds(documentIds, nameof(documentIds));
            if (selected.Any(id => id.Contains(","))) throw new ArgumentException("A comma-delimited legacy selection cannot contain commas in identifiers.", nameof(documentIds));
            if (representation != DocumentDownloadRepresentation.Original && representation != DocumentDownloadRepresentation.Pdf) throw new ArgumentOutOfRangeException(nameof(representation));
            var uri = BuildResourceDownloadUri(URI_RESOURCE + "?ids=" + Uri.EscapeDataString(string.Join(",", selected)));
            var owned = await OpenResourceDownloadAsync(userId, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(representation == DocumentDownloadRepresentation.Pdf ? "application/pdf" : "*/*"));
                return request;
            }, HttpStatusCode.OK, new[] { "application/zip" }, false, cancellationToken).ConfigureAwait(false);
            try { return DocumentStreamUtils.WrapDownloadStream(owned, streamWrapper); }
            catch { owned.Dispose(); throw; }
        }
    }
}
