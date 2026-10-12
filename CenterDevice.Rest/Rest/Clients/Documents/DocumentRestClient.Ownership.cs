using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Transfers ownership of the selected document to an explicit user.</summary>
        /// <inheritdoc cref="TransferOwnershipAsync"/>
        public void TransferOwnership(string userId, string id, string newOwnerId, CancellationToken cancellationToken = default(CancellationToken))
            => TransferOwnershipAsync(userId, id, newOwnerId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Transfers ownership of the selected document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicit selected document identifier.</param>
        /// <param name="newOwnerId">The explicit new owner user ID; no name or e-mail lookup is performed.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>The caller and new owner must have an admin, internal or external role. The caller must own the documents or have the server's transfer-ownership tenant permission. Document and version uploader information is preserved. The former owner retains a direct sharing. The manufacturer declares this operation non-idempotent; the previous owner can lose transfer permission after the first call. Uncertain writes are not replayed; existing expired-token refresh is preserved. Cancellation does not establish that ownership remained unchanged. Existing getters, interfaces and default sharing behavior are unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected resource or new owner is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task TransferOwnershipAsync(string userId, string id, string newOwnerId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateNewOwner(newOwnerId);
            ValidateVersionDocumentId(id);
            var selected = new[] { id };
            var parameters = new JObject { ["new-owner"] = newOwnerId };
            parameters["documents"] = new JArray(selected);
            return ExecuteOwnershipTransferAsync(userId, ApiVersionPrefix + "documents", parameters, cancellationToken);
        }
    }
}
