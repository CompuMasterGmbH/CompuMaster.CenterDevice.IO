using CenterDevice.Rest.Clients.Common;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using CenterDevice.Rest.Utils;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Performs the remove document from collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSharingResponse> RemoveDocumentFromCollectionAsync(string userId, string documentId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> documents = new List<string>();
            documents.Add(documentId);

            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);
            request.AddJsonBody(new { action = RestApiConstants.REMOVE_DOCUMENTS, @params = new { documents = documents } });

            return UnwrapResponse((await ExecuteAsync<DocumentSharingResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false)), new StatusCodeResponseHandler<DocumentSharingResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        /// <summary>Performs the add document to collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSharingResponse> AddDocumentToCollectionAsync(string userId, string documentId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> documents = new List<string>();
            documents.Add(documentId);

            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);
            request.AddJsonBody(new { action = RestApiConstants.ADD_DOCUMENTS, @params = new { documents = documents } });

            return UnwrapResponse((await ExecuteAsync<DocumentSharingResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false)), new StatusCodeResponseHandler<DocumentSharingResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        /// <summary>Performs the erase collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="onlyOwnedDocuments">The only owned documents for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<CollectionEraseResponse> EraseCollectionAsync(string userId, string collectionId, bool onlyOwnedDocuments, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject();
            parameters[RestApiConstants.ACTION] = RestApiConstants.ERASE;
            parameters[RestApiConstants.PARAMS] = CreateEraseParameters(onlyOwnedDocuments);
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            return UnwrapResponse((await ExecuteAsync<CollectionEraseResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false)), new StatusCodeResponseHandler<CollectionEraseResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        /// <summary>Performs the share collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="users">The users for this operation.</param>
        /// <param name="groups">The groups for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<SharingResponse> ShareCollectionAsync(string userId, string collectionId, IEnumerable<string> users, IEnumerable<string> groups, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await UpdateCollectionSharingAsync(userId, collectionId, users, groups, RestApiConstants.SHARE, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the unshare collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="users">The users for this operation.</param>
        /// <param name="groups">The groups for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<SharingResponse> UnshareCollectionAsync(string userId, string collectionId, IEnumerable<string> users, IEnumerable<string> groups, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await UpdateCollectionSharingAsync(userId, collectionId, users, groups, RestApiConstants.UNSHARE, cancellationToken).ConfigureAwait(false));
        }

        private async Task<SharingResponse> UpdateCollectionSharingAsync(string userId, string collectionId, IEnumerable<string> users, IEnumerable<string> groups, string sHARE, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject();
            parameters[RestApiConstants.ACTION] = sHARE;
            parameters[RestApiConstants.PARAMS] = CreateSharingParams(users, groups);
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            return UnwrapResponse((await ExecuteAsync<SharingResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false)), new StatusCodeResponseHandler<SharingResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        /// <summary>Performs the rename collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="newName">The new name for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task RenameCollectionAsync(string userId, string collectionId, string newName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var collectionRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Put, ContentType.APPLICATION_JSON);
            collectionRequest.AddJsonBody(new { name = newName });

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), collectionRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the rename collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The o auth info for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="newName">The new name for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task RenameCollectionAsync(OAuthInfo oAuthInfo, string collectionId, string newName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var collectionRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Put, ContentType.APPLICATION_JSON);
            collectionRequest.AddJsonBody(new { name = newName });

            var result = (await ExecuteAsync(oAuthInfo, collectionRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the get collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<Collection> GetCollectionAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetCollectionAsync(userId, collectionId, RestRequestFields.DEFAULT, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="fields">The fields for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<Collection> GetCollectionAsync(string userId, string collectionId, RestRequestFields fields, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var collectionRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Get);

            if (fields == RestRequestFields.ALL)
            {
                collectionRequest.AddQueryParameter(RestApiConstants.FIELDS, FieldUtils.GetFieldIncludes(typeof(Collection)));
            }
            else if (fields == RestRequestFields.ID)
            {
                collectionRequest.AddQueryParameter(RestApiConstants.FIELDS, RestApiConstants.ID);
            }

            var result = (await ExecuteAsync<Collection>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), collectionRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<Collection>(HttpStatusCode.OK));
        }

        /// <summary>Performs the delete collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task DeleteCollectionAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var createCollectionsRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Delete);

            var result = (await ExecuteAsync<CreateCollectionResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), createCollectionsRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the delete collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The o auth info for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task DeleteCollectionAsync(OAuthInfo oAuthInfo, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var createCollectionsRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Delete);

            var result = (await ExecuteAsync<CreateCollectionResponse>(oAuthInfo, createCollectionsRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the archive collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task ArchiveCollectionAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject
            {
                [RestApiConstants.ACTION] = RestApiConstants.ARCHIVE
            };
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            RestResponse result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
