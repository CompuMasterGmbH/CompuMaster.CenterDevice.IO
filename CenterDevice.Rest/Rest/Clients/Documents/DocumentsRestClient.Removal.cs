using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Moves the selected documents to trash.</summary>
        /// <inheritdoc cref="MoveDocumentsToTrashAsync"/>
        public DocumentRemovalResult MoveDocumentsToTrash(string userId, IEnumerable<string> documentIds, bool? includeErrorInfo = null, CancellationToken cancellationToken = default(CancellationToken))
            => MoveDocumentsToTrashAsync(userId, documentIds, includeErrorInfo, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Moves the selected documents to trash using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The explicit nonempty document selection, copied before authorization with original spelling, order and duplicates.</param>
        /// <param name="includeErrorInfo">Null omits the server-default query; false requests failed IDs; true requests original detailed failures.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or exact selected document failures, retaining original detailed errors when requested.</returns>
        /// <remarks>Admin, internal or external roles are required. The server permits owned documents; the document-deletion tenant setting controls other-owner removal. HTTP 200 can report failures despite successful transport; HTTP 204 reports complete success. Documents can be restored through the separate trash API. No uncertain write or failure is automatically retried. Cancellation does not prove that the server made no changes. Existing deletion APIs and their defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selection is invalid.</exception>
        /// <exception cref="ArgumentNullException">The selection is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentRemovalResult> MoveDocumentsToTrashAsync(string userId, IEnumerable<string> documentIds, bool? includeErrorInfo = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = SnapshotDocumentIds(documentIds, nameof(documentIds));
            return ExecuteDocumentRemovalAsync(userId, URI_RESOURCE, Method.Post, new { action = "delete", @params = new { documents = selected } },
                selected, includeErrorInfo, false, cancellationToken);
        }

        /// <summary>Permanently erases the selected documents.</summary>
        /// <inheritdoc cref="EraseDocumentsAsync"/>
        public DocumentRemovalResult EraseDocuments(string userId, IEnumerable<string> documentIds, bool? includeErrorInfo = null, CancellationToken cancellationToken = default(CancellationToken))
            => EraseDocumentsAsync(userId, documentIds, includeErrorInfo, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Permanently erases the selected documents using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The explicit nonempty document selection, copied before authorization with original spelling, order and duplicates.</param>
        /// <param name="includeErrorInfo">Null omits the server-default query; false requests failed IDs; true requests original detailed failures.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or exact selected document failures, retaining original detailed errors when requested.</returns>
        /// <remarks>Admin, internal or external roles are required. The server permits owned documents; the document-deletion tenant setting controls other-owner removal. HTTP 200 can report failures despite successful transport; HTTP 204 reports complete success. Permanent erasure cannot be undone. No uncertain write or failure is automatically retried. Cancellation does not prove that the server made no changes. Existing deletion APIs and their defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selection is invalid.</exception>
        /// <exception cref="ArgumentNullException">The selection is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentRemovalResult> EraseDocumentsAsync(string userId, IEnumerable<string> documentIds, bool? includeErrorInfo = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = SnapshotDocumentIds(documentIds, nameof(documentIds));
            return ExecuteDocumentRemovalAsync(userId, URI_RESOURCE, Method.Post, new { action = "erase", @params = new { documents = selected } },
                selected, includeErrorInfo, false, cancellationToken);
        }
    }
}
