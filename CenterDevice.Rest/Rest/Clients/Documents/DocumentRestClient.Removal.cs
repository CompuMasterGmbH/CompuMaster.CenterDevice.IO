using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Moves the selected document to trash.</summary>
        /// <inheritdoc cref="MoveDocumentToTrashAsync"/>
        public DocumentRemovalResult MoveDocumentToTrash(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
            => MoveDocumentToTrashAsync(userId, documentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Moves the selected document to trash using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The explicit document identifier, escaped as one selected path segment.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or exact selected document failures, retaining original detailed errors when requested.</returns>
        /// <remarks>Admin, internal or external roles are required. The server permits owned documents; the document-deletion tenant setting controls other-owner removal. HTTP 200 can report failures despite successful transport; HTTP 204 reports complete success. Documents can be restored through the separate trash API. No uncertain write or failure is automatically retried. Cancellation does not prove that the server made no changes. Existing deletion APIs and their defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selection is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentRemovalResult> MoveDocumentToTrashAsync(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(documentId) || documentId == "." || documentId == "..") throw new ArgumentException("A selected document identifier is required.", nameof(documentId));
            var selected = new[] { documentId };
            return ExecuteDocumentRemovalAsync(userId, URI_RESOURCE + Uri.EscapeDataString(documentId), Method.Delete, null,
                selected, null, true, cancellationToken);
        }

        /// <summary>Permanently erases the selected document.</summary>
        /// <inheritdoc cref="EraseDocumentAsync"/>
        public DocumentRemovalResult EraseDocument(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
            => EraseDocumentAsync(userId, documentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Permanently erases the selected document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The explicit document identifier, escaped as one selected path segment.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or exact selected document failures, retaining original detailed errors when requested.</returns>
        /// <remarks>Admin, internal or external roles are required. The server permits owned documents; the document-deletion tenant setting controls other-owner removal. HTTP 200 can report failures despite successful transport; HTTP 204 reports complete success. Permanent erasure cannot be undone. No uncertain write or failure is automatically retried. Cancellation does not prove that the server made no changes. Existing deletion APIs and their defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selection is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentRemovalResult> EraseDocumentAsync(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(documentId) || documentId == "." || documentId == "..") throw new ArgumentException("A selected document identifier is required.", nameof(documentId));
            var selected = new[] { documentId };
            return ExecuteDocumentRemovalAsync(userId, URI_RESOURCE + Uri.EscapeDataString(documentId), Method.Post, new { action = "erase" },
                selected, null, true, cancellationToken);
        }
    }
}
