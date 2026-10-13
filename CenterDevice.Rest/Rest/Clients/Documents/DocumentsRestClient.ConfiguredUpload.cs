using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Uploads a new document with copied explicit metadata and post-upload actions.</summary>
        /// <inheritdoc cref="UploadConfiguredDocumentAsync(string,string,string,DocumentUploadOptions,CancellationToken)"/>
        public DocumentUploadResult UploadConfiguredDocument(string userId, string filename, string path, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => UploadConfiguredDocumentAsync(userId, filename, path, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Uploads streamed content with copied explicit metadata and post-upload actions.</summary>
        /// <inheritdoc cref="UploadConfiguredDocumentAsync(string,string,Func{Stream},DocumentUploadOptions,CancellationToken)"/>
        public DocumentUploadResult UploadConfiguredDocument(string userId, string filename, Func<Stream> fileStreamData, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => UploadConfiguredDocumentAsync(userId, filename, fileStreamData, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Uploads a local file with explicit metadata and actions using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="filename">The original filename stored in metadata.</param>
        /// <param name="path">The local file path; the upload owns its asynchronously opened stream.</param>
        /// <param name="options">Copied optional metadata/actions, or null to omit them.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The immutable created document, partial folder failures or explicitly requested duplicate result.</returns>
        /// <remarks>API 2.29 section 5.2.1 sends metadata first and chunked document bytes second. Filename and measured size remain explicit. Optional original dates are not converted; old upload overloads are unchanged. No sharing, location, processing, duplicate check or retry is added implicitly. A stored document does not establish preview/index/extraction completion. Explicit duplicate checks can return HTTP 409 without creating a document. Cancellation or transport failure after sending leaves an uncertain outcome; it is not replayed.</remarks>
        /// <exception cref="ArgumentException">The filename or known optional metadata/actions are invalid.</exception>
        /// <exception cref="NotSupportedException">Read-only common extended metadata is selected.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">The confirmed upload or duplicate response is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentUploadResult> UploadConfiguredDocumentAsync(string userId, string filename, string path, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); ConfiguredUpload.ValidateFilename(filename); var snapshot = options?.Snapshot(false) ?? new JObject(); var mimeType = options?.MimeType;
            var request = ConfiguredUpload.Request(URI_RESOURCE, filename, new FileInfo(path).Length, snapshot);
            DocumentStreamUtils.AddFileToUpload(request, "document", path, streamWrapper, cancellationToken, mimeType);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); var duplicate = response.StatusCode == HttpStatusCode.Conflict && snapshot["actions"]?["check-for-duplicate"] != null;
            ValidateResponse(response, duplicate ? new DuplicateUploadResponseHandler() : new StatusCodeResponseHandler(HttpStatusCode.Created)); return DocumentUploadResult.Parse(response.Content, duplicate, false);
        }
        /// <summary>Uploads streamed content with explicit metadata and actions using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="UploadConfiguredDocumentAsync(string,string,string,DocumentUploadOptions,CancellationToken)"/>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="filename">The original filename stored in metadata.</param>
        /// <param name="fileStreamData">Creates readable content with an available length, positioned at the beginning. The SDK owns each returned stream, reuses the first length-inspection stream for transfer, and disposes it on all outcomes. Existing explicit token-refresh behavior may request another stream.</param>
        /// <param name="options">Copied optional metadata/actions, or null to omit them.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        public async Task<DocumentUploadResult> UploadConfiguredDocumentAsync(string userId, string filename, Func<Stream> fileStreamData, DocumentUploadOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); ConfiguredUpload.ValidateFilename(filename); var snapshot = options?.Snapshot(false) ?? new JObject(); var mimeType = options?.MimeType;
            using (var upload = new UploadStreamSource(fileStreamData))
            {
                var request = ConfiguredUpload.Request(URI_RESOURCE, filename, upload.MetadataStream.Length, snapshot);
                DocumentStreamUtils.AddFileToUpload(request, "document", upload.Open, streamWrapper, cancellationToken, mimeType);
                var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested(); var duplicate = response.StatusCode == HttpStatusCode.Conflict && snapshot["actions"]?["check-for-duplicate"] != null;
                ValidateResponse(response, duplicate ? new DuplicateUploadResponseHandler() : new StatusCodeResponseHandler(HttpStatusCode.Created)); return DocumentUploadResult.Parse(response.Content, duplicate, false);
            }
        }
    }
    internal static class ConfiguredUpload
    {
        internal static void ValidateFilename(string filename) { if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("An original upload filename is required.", nameof(filename)); }
        internal static RestRequest Request(string resource, string filename, long length, JObject metadata)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            var document = metadata["document"] as JObject ?? new JObject(); document["filename"] = filename; document["size"] = length; metadata["document"] = document;
            var request = new RestRequest(resource, Method.Post) { RequestFormat = DataFormat.Json, AlwaysMultipartFormData = true, Timeout = TimeSpan.FromMilliseconds(int.MaxValue) };
            request.AddOrUpdateHeader("Accept", "application/json"); request.AddParameter(new BodyParameter("metadata", new JObject { ["metadata"] = metadata }.ToString(Formatting.None), "application/json")); return request;
        }
    }
    internal sealed class DuplicateUploadResponseHandler : StatusCodeResponseHandler
    {
        internal DuplicateUploadResponseHandler() : base(HttpStatusCode.Conflict) { }
        public override void ValidateResponse(RestResponse result)
        {
            // RestSharp's completed HTTP 409 carries its own status-only exception.
            // Ignore only that exact marker; custom hooks have already inspected the original response.
            if (result.StatusCode == HttpStatusCode.Conflict && result.ResponseStatus == ResponseStatus.Error && result.Content != null &&
                result.ErrorException?.GetType() == typeof(HttpRequestException) && result.ErrorException.InnerException == null &&
                result.ErrorException.Message == "Request failed with status code Conflict") return;
            base.ValidateResponse(result);
        }
    }
}
