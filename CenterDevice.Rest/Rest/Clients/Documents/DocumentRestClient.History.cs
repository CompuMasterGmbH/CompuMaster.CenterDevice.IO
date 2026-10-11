using CenterDevice.Rest.Clients.Documents.Metadata;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Retrieves the selected document's current version and visible version history.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <returns>The document identity, current version and server-visible version details.</returns>
        /// <remarks>Requests only id, version and version-details. Does not change existing metadata request defaults, infer missing versions, reorder history or resolve uploader visibility placeholders. Guests cannot retrieve older-version details; omitted details do not establish an empty history.</remarks>
        /// <exception cref="ArgumentException">The selected document identifier is missing.</exception>
        public DocumentVersionHistoryMetadata GetDocumentVersionHistory(string userId, string id)
            => GetDocumentVersionHistoryAsync(userId, id).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the selected document's visible version history using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, queue admission and active HTTP I/O.</param>
        /// <returns>The document identity, current version and server-visible version details.</returns>
        /// <remarks>Preserves the server's version numbers, order and visibility information. Null details mean the field was omitted, including guest or empty-field cases. Existing metadata overloads and interfaces retain their defaults. Server visibility failures retain the existing exception handling.</remarks>
        /// <exception cref="ArgumentException">The selected document identifier is missing.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentVersionHistoryMetadata> GetDocumentVersionHistoryAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A selected document identifier is required.", nameof(id));
            return GetDocumentMetadataAsync<DocumentVersionHistoryMetadata>(userId, id, null, cancellationToken);
        }
    }
}
