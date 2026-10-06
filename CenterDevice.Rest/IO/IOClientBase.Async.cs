using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public abstract partial class IOClientBase
    {
        /// <summary>Retrieves visible root collections through the native asynchronous REST client.</summary>
        /// <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        /// <returns>The visible collections with optional child-folder metadata.</returns>
        protected internal virtual async Task<List<Collection>> LookupCollectionsAsync(CancellationToken cancellationToken)
        {
            return (await ApiClient.Collections.GetCollectionsAsync(CurrentAuthenticationContextUserID, null, true, cancellationToken).ConfigureAwait(false))?.Collections;
        }

        /// <summary>Retrieves immediate child folders through the native asynchronous REST client.</summary>
        /// <param name="collectionId">The collection identifier, or null for a nested folder.</param>
        /// <param name="parentFolderId">The parent folder identifier, or the root-folder marker within a collection.</param>
        /// <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        /// <returns>The immediate child folders with their existing metadata fields.</returns>
        protected internal virtual async Task<List<Folder>> LookupChildFoldersAsync(string collectionId, string parentFolderId, CancellationToken cancellationToken)
        {
            return (await ApiClient.Folders.GetFoldersAsync(CurrentAuthenticationContextUserID, collectionId, parentFolderId, null, null,
                new[] { "collection", "id", "name", "parent", "users", "groups", "link" }, cancellationToken).ConfigureAwait(false)).Folders;
        }

        /// <summary>Retrieves immediate documents through the native asynchronous REST client.</summary>
        /// <param name="collectionId">The collection identifier, or null for a nested folder.</param>
        /// <param name="parentFolderId">The parent folder identifier, or the root-folder marker within a collection.</param>
        /// <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        /// <returns>The immediate document metadata.</returns>
        protected internal virtual async Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collectionId, string parentFolderId, CancellationToken cancellationToken)
        {
            return (await ApiClient.Documents.GetAsync<DocumentFullMetadata>(CurrentAuthenticationContextUserID, collectionId, null,
                parentFolderId, 0, int.MaxValue, cancellationToken).ConfigureAwait(false)).Documents;
        }

        /// <summary>Retrieves link details using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="linkId">The link identifier.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>The link details.</returns>
        public Task<Rest.Clients.Link.Link> GetLinkAsync(string linkId, CancellationToken cancellationToken = default(CancellationToken)) =>
            ApiClient.Link.GetLinkAsync(CurrentAuthenticationContextUserID, linkId, cancellationToken);

        /// <summary>Retrieves upload-link details using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="linkId">The upload-link identifier.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>The upload-link details.</returns>
        public Task<Rest.Clients.Link.UploadLink> GetUploadLinkAsync(string linkId, CancellationToken cancellationToken = default(CancellationToken)) =>
            ApiClient.UploadLink.GetLinkAsync(CurrentAuthenticationContextUserID, linkId, cancellationToken);
    }
}
