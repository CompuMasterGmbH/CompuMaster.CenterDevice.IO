using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Uploads a configured new document version from a local file.</summary>
        /// <inheritdoc cref="UploadConfiguredVersionAsync(string,string,string,string,DocumentUploadOptions,CancellationToken)"/>
        public DocumentUploadResult UploadConfiguredVersion(string userId, string id, string filename, string path, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => UploadConfiguredVersionAsync(userId, id, filename, path, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Uploads a configured new document version from streamed content.</summary>
        /// <inheritdoc cref="UploadConfiguredVersionAsync(string,string,string,Func{Stream},DocumentUploadOptions,CancellationToken)"/>
        public DocumentUploadResult UploadConfiguredVersion(string userId, string id, string filename, Func<Stream> fileStreamData, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => UploadConfiguredVersionAsync(userId, id, filename, fileStreamData, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Uploads explicit new-version metadata from a local file using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The original selected document identifier, escaped as one path segment.</param>
        /// <param name="filename">The original stored filename.</param>
        /// <param name="path">The local file path; the upload owns its asynchronously opened stream.</param>
        /// <param name="options">Copied optional title, author, original date and extended metadata; upload actions are unsupported.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The original confirmed positive new-version integer text without width narrowing.</returns>
        /// <remarks>API 2.29 section 5.2.2 retains metadata-first chunked framing and existing stream ownership. Original string dates are not converted. Visibility/lock restrictions remain server-enforced. No version selection, metadata inheritance, duplicate check or mutation replay is inferred; original upload overloads and date defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected identifier, filename or known metadata is invalid.</exception>
        /// <exception cref="NotSupportedException">Upload actions or read-only common extended metadata are supplied.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">The successful response has invalid version metadata.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested; a sent write may already have been applied.</exception>
        public async Task<DocumentUploadResult> UploadConfiguredVersionAsync(string userId, string id, string filename, string path, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var selected = ConfiguredVersionSelection(id); ConfiguredUpload.ValidateFilename(filename); var snapshot = options?.Snapshot(true) ?? new JObject(); var mimeType = options?.MimeType;
            var request = ConfiguredUpload.Request(URI_RESOURCE + selected, filename, new FileInfo(path).Length, snapshot); DocumentStreamUtils.AddFileToUpload(request, "document", path, streamWrapper, cancellationToken, mimeType);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.Created)); return DocumentUploadResult.Parse(response.Content, false, true);
        }
        /// <summary>Uploads explicit new-version metadata from streamed content using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="UploadConfiguredVersionAsync(string,string,string,string,DocumentUploadOptions,CancellationToken)"/>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The original selected document identifier.</param>
        /// <param name="filename">The original stored filename.</param>
        /// <param name="fileStreamData">Creates readable content with an available length, positioned at the beginning. The SDK owns and disposes all returned streams, reusing the first inspected stream for transfer. Existing explicit token refresh may request another stream.</param>
        /// <param name="options">Copied optional document metadata; upload actions are unsupported.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        public async Task<DocumentUploadResult> UploadConfiguredVersionAsync(string userId, string id, string filename, Func<Stream> fileStreamData, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var selected = ConfiguredVersionSelection(id); ConfiguredUpload.ValidateFilename(filename); var snapshot = options?.Snapshot(true) ?? new JObject(); var mimeType = options?.MimeType;
            using (var upload = new UploadStreamSource(fileStreamData))
            {
                var request = ConfiguredUpload.Request(URI_RESOURCE + selected, filename, upload.MetadataStream.Length, snapshot); DocumentStreamUtils.AddFileToUpload(request, "document", upload.Open, streamWrapper, cancellationToken, mimeType);
                var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.Created)); return DocumentUploadResult.Parse(response.Content, false, true);
            }
        }
        private static string ConfiguredVersionSelection(string id) { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("An original document identifier is required.", nameof(id)); return Uri.EscapeDataString(id); }
    }
}
