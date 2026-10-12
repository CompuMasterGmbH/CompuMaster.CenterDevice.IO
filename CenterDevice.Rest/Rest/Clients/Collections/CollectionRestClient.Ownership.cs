using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Transfers ownership of the selected collection to an explicit user.</summary>
        /// <inheritdoc cref="TransferOwnershipAsync"/>
        public void TransferOwnership(string userId, string id, string newOwnerId, CancellationToken cancellationToken = default(CancellationToken))
            => TransferOwnershipAsync(userId, id, newOwnerId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Transfers ownership of the selected collection using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicit selected collection identifier.</param>
        /// <param name="newOwnerId">The explicit new owner user ID; no name or e-mail lookup is performed.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>The caller and new owner must have an admin, internal or external role. The caller must own the collection. The former owner retains a direct sharing. The client adds no retry or role inference. Uncertain writes are not replayed; existing expired-token refresh is preserved. Cancellation does not establish that ownership remained unchanged. Existing getters, interfaces and default sharing behavior are unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected resource or new owner is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task TransferOwnershipAsync(string userId, string id, string newOwnerId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateNewOwner(newOwnerId);
            if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A selected collection ID is required.", nameof(id));
            var parameters = new JObject { ["new-owner"] = newOwnerId };
            return ExecuteOwnershipTransferAsync(userId, URI_RESOURCE + "/" + Uri.EscapeDataString(id), parameters, cancellationToken);
        }
    }
}
