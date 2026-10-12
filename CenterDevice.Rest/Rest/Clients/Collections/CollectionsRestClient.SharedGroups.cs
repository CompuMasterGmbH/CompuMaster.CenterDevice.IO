using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionsRestClient
    {
        /// <summary>Retrieves visible collections explicitly shared via the selected group.</summary>
        /// <inheritdoc cref="GetCollectionsSharedWithGroupAsync"/>
        public CollectionsResults GetCollectionsSharedWithGroup(string userId, string groupId, string[] fields = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetCollectionsSharedWithGroupAsync(userId, groupId, fields, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves collections explicitly shared via a group using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The group identifier whose explicit collection sharings are selected.</param>
        /// <param name="fields">The response field names, snapshotted before authorization, or null for server defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, queue admission and HTTP I/O.</param>
        /// <returns>The visible collections, including an empty list when the server returns no content.</returns>
        /// <remarks>The group filter does not grant visibility or infer group membership or ownership. Hidden sharing identities and the hidden owner sentinel remain server controlled.</remarks>
        /// <exception cref="ArgumentException">The group identifier or a field name is missing, or a field contains a comma.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<CollectionsResults> GetCollectionsSharedWithGroupAsync(string userId, string groupId, string[] fields = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var request = CreateFilteredListingRequest(URI_RESOURCE, "group", groupId, fields, false, cancellationToken);
            var response = await ExecuteAsync<CollectionsResults>(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var result = UnwrapResponse(response, new StatusCodeResponseHandler<CollectionsResults>(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (response.StatusCode == HttpStatusCode.NoContent) return new CollectionsResults { Collections = new List<Collection>() };
            if (result?.Collections == null) throw new InvalidResponseDataException("The collection listing has no collections array.", new InvalidDataException("Expected collections in a successful listing."));
            return result;
        }
    }
}
