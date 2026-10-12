using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Updates explicit version-independent extended document metadata.</summary>
        /// <inheritdoc cref="UpdateExtendedMetadataAsync"/>
        public void UpdateExtendedMetadata(string userId, string documentId, DocumentMetadataUpdateRequest update, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateExtendedMetadataAsync(userId, documentId, update, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates selected document metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The explicitly selected document identifier.</param>
        /// <param name="update">The copied explicit dotted set/unset selections or deprecated complete namespaces.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.2.35 applies changes to the whole document, not one version. The common namespace is read-only; date and decimal strings remain original and server-validated. Known namespace/section/string-array structure is checked without current metadata lookups, invented template fields, overlap precedence, permission changes or new mutation replay. Existing APIs/defaults and sharing remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The update is null.</exception>
        /// <exception cref="ArgumentException">The selected document or known update structure is invalid.</exception>
        /// <exception cref="NotSupportedException">The update targets the read-only common namespace.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no metadata changed.</exception>
        public async Task UpdateExtendedMetadataAsync(string userId, string documentId, DocumentMetadataUpdateRequest update, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (update == null) throw new ArgumentNullException(nameof(update));
            if (string.IsNullOrWhiteSpace(documentId) || documentId == "." || documentId == "..") throw new ArgumentException("A selected document identifier is required.", nameof(documentId));
            var body = new Newtonsoft.Json.Linq.JObject { ["action"] = "set-extended-metadata", ["params"] = update.SnapshotForSend() };
            var request = CreateRestRequest(URI_RESOURCE + Uri.EscapeDataString(documentId), Method.Post, "application/json"); request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
