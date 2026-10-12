using CenterDevice.Rest.Clients.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Adds explicitly selected document references to a collection.</summary>
        /// <inheritdoc cref="AddDocumentsToCollectionAsync"/>
        public DocumentMembershipResult AddDocumentsToCollection(string userId, string collectionId, IEnumerable<string> documents, CancellationToken cancellationToken = default(CancellationToken))
            => AddDocumentsToCollectionAsync(userId, collectionId, documents, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Adds selected document references to a collection using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="documents">The nonempty explicit document selection copied before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original immutable partial-failure identifiers, or an empty failure list after confirmed success.</returns>
        /// <remarks>The server enforces collection/document visibility and sharing rights; guests may change only owned collections. IDs and original document content are preserved. No speculative retry or permission inference is added; cancellation cannot establish that no membership changed. Existing single-document APIs remain unchanged.</remarks>
        /// <exception cref="System.ArgumentException">The collection or document selection is unusable.</exception>
        /// <exception cref="System.ArgumentNullException">The document selection is null.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful partial-failure metadata is malformed or identifies an unselected document.</exception>
        /// <exception cref="System.OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentMembershipResult> AddDocumentsToCollectionAsync(string userId, string collectionId, IEnumerable<string> documents, CancellationToken cancellationToken = default(CancellationToken))
            => ChangeDocumentMembershipAsync(userId, "collection", collectionId, documents, true, null, true, cancellationToken);
        /// <summary>Removes explicitly selected document references from a collection.</summary>
        /// <inheritdoc cref="RemoveDocumentsFromCollectionAsync"/>
        public DocumentMembershipResult RemoveDocumentsFromCollection(string userId, string collectionId, IEnumerable<string> documents, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveDocumentsFromCollectionAsync(userId, collectionId, documents, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes selected document references from a collection using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="AddDocumentsToCollectionAsync"/>
        /// <remarks>Removing collection references also removes the respective folder references within that collection; it does not erase the documents or their references in other collections. Server visibility/sharing/guest restrictions apply. Existing methods remain unchanged.</remarks>
        public Task<DocumentMembershipResult> RemoveDocumentsFromCollectionAsync(string userId, string collectionId, IEnumerable<string> documents, CancellationToken cancellationToken = default(CancellationToken))
            => ChangeDocumentMembershipAsync(userId, "collection", collectionId, documents, false, null, true, cancellationToken);
    }
}
