using CenterDevice.Rest.Clients.Common;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FolderRestClient
    {
        /// <summary>Performs the get folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="fields">The fields for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<Folder> GetFolderAsync(string userId, string id, string[] fields, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderRequest = CreateRestRequest(URI_RESOURCE + id, Method.Get);
            if (fields != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.FIELDS, string.Join(",", fields));
            }

            var result = (await ExecuteAsync<Folder>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), folderRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<Folder>(HttpStatusCode.OK));
        }

        /// <summary>Performs the delete folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task DeleteFolderAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderRequest = CreateRestRequest(URI_RESOURCE + id, Method.Delete, ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), folderRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the erase folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="onlyOwnedDocuments">The only owned documents for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FolderEraseResponse> EraseFolderAsync(string userId, string id, bool onlyOwnedDocuments, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eraseFolder = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.ACTION] = RestApiConstants.ERASE;
            parameters[RestApiConstants.PARAMS] = CreateEraseParameters(onlyOwnedDocuments);
            eraseFolder.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<FolderEraseResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), eraseFolder, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<FolderEraseResponse>(new List<HttpStatusCode>() { HttpStatusCode.NoContent, HttpStatusCode.OK }));
        }

        /// <summary>Performs the rename folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="newName">The new name for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task RenameFolderAsync(string userId, string id, string newName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderRequest = CreateRestRequest(URI_RESOURCE + id, Method.Put, ContentType.APPLICATION_JSON);
            folderRequest.AddJsonBody(new { name = newName });

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), folderRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the remove document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="removeFromCollection">The remove from collection for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task RemoveDocumentAsync(string userId, string documentId, string folderId, bool removeFromCollection, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removeDocumentRequest = CreateRestRequest(URI_RESOURCE + folderId, Method.Post, ContentType.APPLICATION_JSON);

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var removeDetails = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            removeDetails[RestApiConstants.DOCUMENTS] = new JArray(documentId);
            removeDetails[RestApiConstants.REMOVE_FROM_COLLECTION] = removeFromCollection;

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.ACTION] = RestApiConstants.REMOVE_DOCUMENTS;
            parameters[RestApiConstants.PARAMS] = removeDetails;
            removeDocumentRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), removeDocumentRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the add document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="targetFolderId">The target folder id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task AddDocumentAsync(string userId, string documentId, string targetFolderId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> documentsIds = new List<string>(new string[] { documentId });
            var addOrRemoveDocumentRequest = CreateRestRequest(URI_RESOURCE + targetFolderId, Method.Post, ContentType.APPLICATION_JSON);
            addOrRemoveDocumentRequest.AddJsonBody(new { action = RestApiConstants.ADD_DOCUMENTS, @params = new { documents = documentsIds } });

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), addOrRemoveDocumentRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the move folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="targetFolderId">The target folder id for this operation.</param>
        /// <param name="targetCollectionId">The target collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task MoveFolderAsync(string userId, string folderId, string targetFolderId, string targetCollectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var moveFolderRequest = CreateRestRequest(URI_RESOURCE + folderId, Method.Put, ContentType.APPLICATION_JSON);

            if (targetFolderId == null || targetFolderId == RestApiConstants.NONE)
            {
                moveFolderRequest.AddJsonBody(new { parent = RestApiConstants.NONE, collection = targetCollectionId });
            }
            else
            {
                moveFolderRequest.AddJsonBody(new { parent = targetFolderId });
            }

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), moveFolderRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the share folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="userIds">The user ids for this operation.</param>
        /// <param name="groupIds">The group ids for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<SharingResponse> ShareFolderAsync(string userId, string folderId, IEnumerable<string> userIds, IEnumerable<string> groupIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await UpdateFolderSharingAsync(userId, folderId, userIds, groupIds, RestApiConstants.SHARE, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the unshare folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="userIds">The user ids for this operation.</param>
        /// <param name="groupIds">The group ids for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<SharingResponse> UnshareFolderAsync(string userId, string folderId, IEnumerable<string> userIds, IEnumerable<string> groupIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await UpdateFolderSharingAsync(userId, folderId, userIds, groupIds, RestApiConstants.UNSHARE, cancellationToken).ConfigureAwait(false));
        }

        private async Task<SharingResponse> UpdateFolderSharingAsync(string userId, string folderId, IEnumerable<string> userIds, IEnumerable<string> groupIds, string sHARE, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var shareFolder = CreateRestRequest(URI_RESOURCE + folderId, Method.Post, ContentType.APPLICATION_JSON);

            var sharingDetails = new JObject();
            if (userIds?.Any() == true)
            {
                sharingDetails[RestApiConstants.USERS] = new JArray(userIds);
            }
            if (groupIds?.Any() == true)
            {
                sharingDetails[RestApiConstants.GROUPS] = new JArray(groupIds);
            }

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.ACTION] = sHARE;
            parameters[RestApiConstants.PARAMS] = sharingDetails;
            shareFolder.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<SharingResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), shareFolder, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<SharingResponse>(new List<HttpStatusCode>() { HttpStatusCode.NoContent, HttpStatusCode.OK }));
        }
    }
}
