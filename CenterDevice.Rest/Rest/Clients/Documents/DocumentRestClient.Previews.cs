using CenterDevice.Rest.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Downloads selected document preview pages with their MIME metadata.</summary>
        /// <inheritdoc cref="DownloadPreviewsAsync"/>
        public DocumentPreviewDownload DownloadPreviews(string userId, string id, PreviewSize size, string pages = "1",
            long? version = null, int? waitForGenerationSeconds = null, bool? includeErrorInfo = null, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadPreviewsAsync(userId, id, size, pages, version, waitForGenerationSeconds, includeErrorInfo, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens selected preview pages using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="size">One of the supported preview resolutions.</param>
        /// <param name="pages">Positive one-based pages or ascending ranges separated by commas, or all.</param>
        /// <param name="version">The positive historical version, or null for the current version.</param>
        /// <param name="waitForGenerationSeconds">The optional server generation wait between 1 and 30 seconds.</param>
        /// <param name="includeErrorInfo">The optional request for permanent preview-generation error information; null preserves the server default.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, HTTP I/O and the returned body.</param>
        /// <returns>A response-owned image or multipart download that the caller must dispose.</returns>
        /// <remarks>Accepts PNG, JPEG and multipart responses according to actual server availability. Multipart responses retain their boundary and page-number part names; missing requested parts are possible and must not be treated as empty images. A single returned image does not establish the identities of all requested pages. Guests can access only the latest version. Pending generation is explicit and never retried automatically. This additive method leaves the original DownloadPreview defaults unchanged.</remarks>
        /// <exception cref="ArgumentException">The document ID or page selection is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The resolution, version or generation wait is invalid.</exception>
        /// <exception cref="DocumentRepresentationNotReadyException">Generation is pending or its server wait expired.</exception>
        /// <exception cref="NotAcceptableException">Preview generation is unsupported.</exception>
        /// <exception cref="NotFoundException">The document is unavailable or the server reports a permanent generation failure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentPreviewDownload> DownloadPreviewsAsync(string userId, string id, PreviewSize size, string pages = "1",
            long? version = null, int? waitForGenerationSeconds = null, bool? includeErrorInfo = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (id == "." || id == "..") throw new ArgumentException("A document identifier must not be a relative path segment.", nameof(id));
            if (!Enum.IsDefined(typeof(PreviewSize), size)) throw new ArgumentOutOfRangeException(nameof(size));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (waitForGenerationSeconds < 1 || waitForGenerationSeconds > 30) throw new ArgumentOutOfRangeException(nameof(waitForGenerationSeconds));
            ValidatePreviewPages(pages);
            var path = URI_RESOURCE + Uri.EscapeDataString(id);
            if (version.HasValue) path += ";version=" + version.Value.ToString(CultureInfo.InvariantCulture);
            path += ";preview=" + size.ToApiParameter() + ";pages=" + pages;
            var query = new List<string>();
            if (waitForGenerationSeconds.HasValue) query.Add("wait-for-generation=" + waitForGenerationSeconds.Value.ToString(CultureInfo.InvariantCulture));
            if (includeErrorInfo.HasValue) query.Add("include-error-info=" + (includeErrorInfo.Value ? "true" : "false"));
            if (query.Count > 0) path += "?" + string.Join("&", query);
            var uri = new Uri(new Uri(CustomOptionBaseAddress), path);
            var stream = await OpenResourceDownloadAsync(userId, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
                return request;
            }, HttpStatusCode.OK, new[] { "image/png", "image/jpeg", "multipart/form-data" }, true, cancellationToken).ConfigureAwait(false);
            try
            {
                var contentType = ((ResponseDownloadStream)stream).ContentType;
                if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                {
                    var parsed = MediaTypeHeaderValue.Parse(contentType);
                    var boundaries = parsed.Parameters.Where(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase)).ToArray();
                    var boundary = boundaries.Length == 1 ? boundaries[0].Value?.Trim('"') : null;
                    if (string.IsNullOrWhiteSpace(boundary) || boundary.Length > 70 || boundary.EndsWith(" ", StringComparison.Ordinal) ||
                        boundary.Any(c => c > 126 || (!char.IsLetterOrDigit(c) && !" '()+_,-./:=?".Contains(c))))
                        throw new InvalidResponseDataException("The multipart preview has no usable boundary.", new InvalidDataException("Exactly one valid multipart boundary is required."));
                }
                return new DocumentPreviewDownload(DocumentStreamUtils.WrapDownloadStream(stream, streamWrapper), contentType);
            }
            catch { stream.Dispose(); throw; }
        }

        private static void ValidatePreviewPages(string pages)
        {
            if (pages == "all") return;
            if (string.IsNullOrEmpty(pages)) throw new ArgumentException("A preview page selection is required.", nameof(pages));
            foreach (var part in pages.Split(','))
            {
                var range = part.Split('-');
                if (range.Length > 2 || range.Any(n => !int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0) ||
                    (range.Length == 2 && int.Parse(range[0], CultureInfo.InvariantCulture) > int.Parse(range[1], CultureInfo.InvariantCulture)))
                    throw new ArgumentException("Preview pages must be positive page numbers, ascending ranges or all.", nameof(pages));
            }
        }
    }
}
