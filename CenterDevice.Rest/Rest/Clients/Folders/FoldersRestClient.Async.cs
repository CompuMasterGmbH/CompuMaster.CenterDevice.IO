using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FoldersRestClient
    {
        /// <summary>Performs the get folders operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="parentId">The parent id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FoldersResponse> GetFoldersAsync(string userId, string collectionId, string parentId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetFoldersAsync(userId, collectionId, parentId, documentId, null, null, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get folders operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="path">The path for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FoldersResponse> GetFoldersAsync(string userId, string collectionId, string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetFoldersAsync(userId, collectionId, null, null, path, null, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get folders operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="parentId">The parent id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="path">The path for this operation.</param>
        /// <param name="fields">The fields for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FoldersResponse> GetFoldersAsync(string userId, string collectionId, string parentId, string documentId, string path, string[] fields, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetFoldersAsync(userId, collectionId, parentId, documentId, path, null, fields, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get folders operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="path">The path for this operation.</param>
        /// <param name="ids">The ids for this operation.</param>
        /// <param name="fields">The fields for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FoldersResponse> GetFoldersAsync(string userId, string collectionId, string folderId, string documentId, string path, IEnumerable<string> ids, string[] fields, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetFoldersAsync(userId, collectionId, folderId, documentId, path, ids, false, false, fields, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get folders operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="path">The path for this operation.</param>
        /// <param name="ids">The ids for this operation.</param>
        /// <param name="onlySharedFolders">The only shared folders for this operation.</param>
        /// <param name="onlyTopMost">The only top most for this operation.</param>
        /// <param name="fields">The fields for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FoldersResponse> GetFoldersAsync(string userId, string collectionId, string folderId, string documentId, string path, IEnumerable<string> ids, bool onlySharedFolders, bool onlyTopMost, string[] fields, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderRequest = CreateRestRequest(URI_RESOURCE, Method.Get);
            if (collectionId != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.COLLECTION, collectionId);
            }
            if (documentId != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.DOCUMENT, documentId);
            }
            if (folderId != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.PARENT, folderId);
            }
            if (path != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.PATH, path.Replace(Path.DirectorySeparatorChar, '/'));
            }
            if (ids != null && ids.Any())
            {
                folderRequest.AddQueryParameter(RestApiConstants.IDS, string.Join(",", ids));
            }

            if (fields != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.FIELDS, string.Join(",", fields));
            }

            if (onlySharedFolders)
            {
                folderRequest.AddQueryParameter(RestApiConstants.ONLY_SHARED_FOLDERS, true.ToString());
                if (onlyTopMost)
                {
                    folderRequest.AddQueryParameter(RestApiConstants.ONLY_TOP_MOST, true.ToString());
                }
            }

            var response = (await ExecuteAsync<FoldersResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), folderRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new GetFoldersResponseHandler());
        }

        /// <summary>Performs the create folder operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="name">The name for this operation.</param>
        /// <param name="collection">The collection for this operation.</param>
        /// <param name="parent">The parent for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FolderCreationResponse> CreateFolderAsync(string userId, string name, string collection, string parent, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folderRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);
            if (string.IsNullOrWhiteSpace(parent) || parent == RestApiConstants.NONE)
            {
                folderRequest.AddJsonBody(new { name = name, parent = RestApiConstants.NONE, collection = collection });
            }
            else
            {
                folderRequest.AddJsonBody(new { name = name, parent = parent });
            }

            var response = (await ExecuteAsync<FolderCreationResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), folderRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new CreateFolderResponseHandler());
        }
    }
}
