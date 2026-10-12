using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Downloads selected current documents as a ZIP archive.</summary>
        /// <inheritdoc cref="DownloadDocumentsAsZipAsync"/>
        public Stream DownloadDocumentsAsZip(string userId, IEnumerable<string> documentIds,
            DocumentDownloadRepresentation representation = DocumentDownloadRepresentation.Original, bool stream = false, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadDocumentsAsZipAsync(userId, documentIds, representation, stream, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens a ZIP archive of selected current documents using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">A nonempty sequence of selected document IDs, enumerated once before authorization.</param>
        /// <param name="representation">Original contents or existing PDF representations.</param>
        /// <param name="stream">True to create the archive while streaming without a known content length; false to assemble it on the server first.</param>
        /// <param name="cancellationToken">Cancels authorization, shared admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned ZIP stream that the caller must dispose.</returns>
        /// <remarks>The server includes only visible documents and uses their current versions. Duplicate filenames can be renamed by the server. PDF archives require every requested PDF to exist already and do not trigger conversion. Standard downloads can take time while the server prepares the archive. No automatic redirect or ambiguous-request replay is performed.</remarks>
        /// <exception cref="ArgumentNullException">The ID sequence is null.</exception>
        /// <exception cref="ArgumentException">The sequence is empty or contains a missing ID.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The representation is unsupported.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<Stream> DownloadDocumentsAsZipAsync(string userId, IEnumerable<string> documentIds,
            DocumentDownloadRepresentation representation = DocumentDownloadRepresentation.Original, bool stream = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (documentIds == null) throw new ArgumentNullException(nameof(documentIds));
            var selected = documentIds.ToArray();
            if (selected.Length == 0 || selected.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("At least one selected document ID is required.", nameof(documentIds));
            if (representation != DocumentDownloadRepresentation.Original && representation != DocumentDownloadRepresentation.Pdf)
                throw new ArgumentOutOfRangeException(nameof(representation));
            cancellationToken.ThrowIfCancellationRequested();
            var json = JsonConvert.SerializeObject(new { action = "download", @params = new
                { documents = selected, representation = representation == DocumentDownloadRepresentation.Pdf ? "pdf" : "original", stream } });
            var uri = BuildResourceDownloadUri(URI_RESOURCE);
            var owned = await OpenResourceDownloadAsync(userId, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return request;
            }, HttpStatusCode.OK, new[] { "application/zip" }, false, cancellationToken).ConfigureAwait(false);
            try { return DocumentStreamUtils.WrapDownloadStream(owned, streamWrapper); }
            catch { owned.Dispose(); throw; }
        }
    }
}
