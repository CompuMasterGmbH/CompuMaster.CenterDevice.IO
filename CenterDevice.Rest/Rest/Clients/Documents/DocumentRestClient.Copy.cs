using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Creates a server-side copy of the selected document.</summary>
        /// <inheritdoc cref="CopyDocumentAsync"/>
        public string CopyDocument(string userId, string id, string filename, string collectionId = null, string folderId = null, CancellationToken cancellationToken = default(CancellationToken))
            => CopyDocumentAsync(userId, id, filename, collectionId, folderId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates a document copy using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The visible source document identifier.</param>
        /// <param name="filename">The filename for the new copy.</param>
        /// <param name="collectionId">The optional destination collection; mutually exclusive with folderId.</param>
        /// <param name="folderId">The optional destination folder; mutually exclusive with collectionId.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O; cancellation does not establish that no copy was created.</param>
        /// <returns>The server-confirmed identifier of the new document.</returns>
        /// <remarks>Requires an admin, internal or external user with source visibility and destination access. Copies the latest original representation without inheriting sharing, tags, comments, version history or extended metadata. The source and its references remain unchanged. Non-idempotent: each successful call creates a new document; uncertain writes are not automatically replayed.</remarks>
        /// <exception cref="ArgumentException">An identifier or filename is missing, or both destination types are selected.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<string> CopyDocumentAsync(string userId, string id, string filename, string collectionId = null, string folderId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = new DocumentCopyRequest(id, filename);
            ValidateCopyDestination(collectionId, folderId);
            var parameters = new JObject { ["filename"] = selected.Filename };
            if (collectionId != null) parameters["collection"] = collectionId;
            if (folderId != null) parameters["folder"] = folderId;
            var ids = await ExecuteDocumentCopyAsync(userId, URI_RESOURCE + Uri.EscapeDataString(selected.DocumentId), parameters, new[] { selected.DocumentId }, false, cancellationToken).ConfigureAwait(false);
            return ids[0];
        }
    }
}
