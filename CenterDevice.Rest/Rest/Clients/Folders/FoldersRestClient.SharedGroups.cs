using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FoldersRestClient
    {
        /// <summary>Retrieves visible folders explicitly shared via the selected group.</summary>
        /// <inheritdoc cref="GetFoldersSharedWithGroupAsync"/>
        public FoldersResponse GetFoldersSharedWithGroup(string userId, string groupId, string[] fields = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetFoldersSharedWithGroupAsync(userId, groupId, fields, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves folders explicitly shared via a group using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The group identifier whose explicit folder sharings are selected.</param>
        /// <param name="fields">The response field names, snapshotted before authorization, or null for server defaults. Full-path and inherited-sharings are unsupported for this query.</param>
        /// <param name="cancellationToken">Cancels authorization, queue admission and HTTP I/O.</param>
        /// <returns>The visible folders, including an empty list when the server returns no content.</returns>
        /// <remarks>The group filter does not grant visibility or infer group membership. This query does not include folders visible only through public collections. Request has-subfolders for optional child-existence metadata without fetching children.</remarks>
        /// <exception cref="ArgumentException">The group identifier or a field name is missing, or a field contains a comma.</exception>
        /// <exception cref="NotSupportedException">The fields include full-path or inherited-sharings.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<FoldersResponse> GetFoldersSharedWithGroupAsync(string userId, string groupId, string[] fields = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetFilteredFoldersAsync(userId, "group", groupId, fields, cancellationToken);

        /// <summary>Searches visible folders by their partial names.</summary>
        /// <inheritdoc cref="SearchFoldersByNameAsync"/>
        public FoldersResponse SearchFoldersByName(string userId, string text, string[] fields = null, CancellationToken cancellationToken = default(CancellationToken))
            => SearchFoldersByNameAsync(userId, text, fields, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Searches visible folder names using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="text">The nonempty text query for partial folder-name matches, interpreted by the server.</param>
        /// <param name="fields">The response field names, snapshotted before authorization, or null for server defaults. Full-path and inherited-sharings are unsupported for this query.</param>
        /// <param name="cancellationToken">Cancels authorization, queue admission and HTTP I/O.</param>
        /// <returns>The visible matching folders, including an empty list when the server returns no content.</returns>
        /// <remarks>API 2.29 permits only the fields parameter alongside text. This method sends no group, parent, collection, document, public-collection or shared-folder flags and does not fetch children.</remarks>
        /// <exception cref="ArgumentException">The text query or a field name is missing, or a field contains a comma.</exception>
        /// <exception cref="NotSupportedException">The fields include full-path or inherited-sharings.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<FoldersResponse> SearchFoldersByNameAsync(string userId, string text, string[] fields = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetFilteredFoldersAsync(userId, "text", text, fields, cancellationToken);

        private async Task<FoldersResponse> GetFilteredFoldersAsync(string userId, string filter, string value, string[] fields, CancellationToken token)
        {
            var request = CreateFilteredListingRequest(URI_RESOURCE, filter, value, fields, true, token);
            var response = await ExecuteAsync<FoldersResponse>(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            var result = UnwrapResponse(response, new StatusCodeResponseHandler<FoldersResponse>(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (response.StatusCode == HttpStatusCode.NoContent) return new FoldersResponse { Folders = new List<Folder>() };
            if (result?.Folders == null) throw new InvalidResponseDataException("The folder listing has no folders array.", new InvalidDataException("Expected folders in a successful listing."));
            return result;
        }
    }
}
