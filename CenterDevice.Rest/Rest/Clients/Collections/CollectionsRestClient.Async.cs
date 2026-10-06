using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionsRestClient
    {
        /// <summary>Performs the get collections operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<CollectionsResults> GetCollectionsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetCollectionsAsync(userId, null, false, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get collections operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="ids">The ids for this operation.</param>
        /// <param name="includeHasFolders">The include has folders for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<CollectionsResults> GetCollectionsAsync(string userId, IEnumerable<string> ids, bool includeHasFolders, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Get);
            if (ids != null && ids.Any())
            {
                searchRequest.AddQueryParameter(RestApiConstants.IDS, string.Join(",", ids));
            }

            if (includeHasFolders)
            {
                // has folders is the only on default field
                searchRequest.AddQueryParameter(RestApiConstants.FIELDS, Utils.FieldUtils.GetFieldIncludes(typeof(Collection)));
            }

            //following lines might fail with HttpStatusCode.BadRequest when the user is missing an assigned license
            var result = (await ExecuteAsync<CollectionsResults>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), searchRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<CollectionsResults>((new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent })));
        }

        /// <summary>Performs the create collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionName">The collection name for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<CreateCollectionResponse> CreateCollectionAsync(string userId, string collectionName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await CreateCollectionAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), collectionName, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the create collection operation using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The o auth info for this operation.</param>
        /// <param name="collectionName">The collection name for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<CreateCollectionResponse> CreateCollectionAsync(OAuthInfo oAuthInfo, string collectionName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var createCollectionsRequest = CreateRestRequest(URI_RESOURCE, Method.Post);

            createCollectionsRequest.AddJsonBody(new { name = collectionName });

            var result = (await ExecuteAsync<CreateCollectionResponse>(oAuthInfo, createCollectionsRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<CreateCollectionResponse>(HttpStatusCode.Created));
        }

        /// <summary>Performs the get collection ids operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="includePublic">The include public for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<IEnumerable<string>> GetCollectionIdsAsync(string userId, bool includePublic, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Get);
            searchRequest.AddQueryParameter(RestApiConstants.INCLUDE_PUBLIC, includePublic.ToString());
            searchRequest.AddQueryParameter(RestApiConstants.FIELDS, RestApiConstants.ID);

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), searchRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (string.IsNullOrWhiteSpace(result.Content))
            {
                return new string[] { };
            }
            return JObject.Parse(result.Content)[RestApiConstants.COLLECTIONS]?.Select(i => i[RestApiConstants.ID]).Values<string>();
        }
    }
}
