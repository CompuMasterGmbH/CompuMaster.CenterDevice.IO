using CenterDevice.Rest.Clients.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FolderRestClient
    {
        /// <summary>Adds explicitly selected document references to a folder.</summary>
        /// <inheritdoc cref="AddDocumentsAsync"/>
        public DocumentMembershipResult AddDocuments(string userId, string folderId, IEnumerable<string> documents, CancellationToken cancellationToken = default(CancellationToken))
            => AddDocumentsAsync(userId, folderId, documents, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Adds selected document references to a folder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="folderId">The explicitly selected folder identifier.</param>
        /// <param name="documents">The nonempty explicit document selection copied before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>An immutable empty failure list after confirmed HTTP 204.</returns>
        /// <remarks>The server permits administrators, internal and external users and enforces visibility. Adding also establishes collection membership when absent and moves existing folder references within the same collection. It does not copy or erase document identities. No speculative retry or permission inference is added. Existing single-document methods remain unchanged.</remarks>
        /// <exception cref="System.ArgumentException">The folder or document selection is unusable.</exception>
        /// <exception cref="System.ArgumentNullException">The document selection is null.</exception>
        /// <exception cref="System.OperationCanceledException">Cancellation is requested and cannot establish that no membership changed.</exception>
        public Task<DocumentMembershipResult> AddDocumentsAsync(string userId, string folderId, IEnumerable<string> documents, CancellationToken cancellationToken = default(CancellationToken))
            => ChangeDocumentMembershipAsync(userId, "folder", folderId, documents, true, null, false, cancellationToken);
        /// <summary>Removes explicitly selected document references from a folder.</summary>
        /// <inheritdoc cref="RemoveDocumentsAsync"/>
        public DocumentMembershipResult RemoveDocuments(string userId, string folderId, IEnumerable<string> documents, bool? removeFromCollection = null, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveDocumentsAsync(userId, folderId, documents, removeFromCollection, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes selected document references from a folder using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="folderId">The explicitly selected folder identifier.</param>
        /// <param name="documents">The nonempty explicit document selection copied before authorization.</param>
        /// <param name="removeFromCollection">Whether also to remove collection membership, or null to omit the option and retain collection membership.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original collection-removal failure identifiers when that additional operation is selected, or an empty list after HTTP 204.</returns>
        /// <remarks>The server permits administrators, internal and external users and enforces visibility. False/omission removes only folder references. True also removes collection references; collection-removal failures do not undo prior folder removal. Documents and references in other collections are not erased. No speculative replay is added; cancellation cannot establish that no membership changed. Existing methods remain unchanged.</remarks>
        /// <exception cref="System.ArgumentException">The folder or document selection is unusable.</exception>
        /// <exception cref="System.ArgumentNullException">The document selection is null.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful partial-failure metadata is malformed or identifies an unselected document.</exception>
        /// <exception cref="System.OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentMembershipResult> RemoveDocumentsAsync(string userId, string folderId, IEnumerable<string> documents, bool? removeFromCollection = null, CancellationToken cancellationToken = default(CancellationToken))
            => ChangeDocumentMembershipAsync(userId, "folder", folderId, documents, false, removeFromCollection, removeFromCollection == true, cancellationToken);
    }
}
