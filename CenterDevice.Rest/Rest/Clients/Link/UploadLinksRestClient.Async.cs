using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Link
{
    public partial class UploadLinksRestClient
    {
        /// <summary>Performs the get all upload links operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadLinks> GetAllUploadLinksAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var getUploadLinksRequest = CreateRestRequest(URI_RESOURCE, Method.Get);

            var result = (await ExecuteAsync<UploadLinks>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), getUploadLinksRequest, cancellationToken).ConfigureAwait(false));
            UploadLinks resultUnwrapped = UnwrapResponse(result, new StatusCodeResponseHandler<UploadLinks>(HttpStatusCode.OK));
            foreach (UploadLink item in resultUnwrapped.UploadLinksList)
            {
                item.UploadLinkBaseUrl = this.UploadLinkBaseUrl;
            }
            return resultUnwrapped;
        }

        /// <summary>Performs the create collection link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="name">The name for this operation.</param>
        /// <param name="tags">The tags for this operation.</param>
        /// <param name="expiryDate">The expiry date for this operation.</param>
        /// <param name="maxDocuments">The max documents for this operation.</param>
        /// <param name="password">The password for this operation.</param>
        /// <param name="emailCreatorOnUpload">The email creator on upload for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadLinkCreationResponse> CreateCollectionLinkAsync(string userId, string collectionId, string name, List<string> tags, DateTime? expiryDate, int? maxDocuments, string password, bool? emailCreatorOnUpload, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (collectionId == null)
            {
                throw new ArgumentException("Id and field need to be set.");
            }

            var createLinkRequest = CreateRestRequest(URI_RESOURCE, Method.Post);
            JObject parameters = CreateJsonBody(collectionId, name, tags, expiryDate, maxDocuments, password, emailCreatorOnUpload);

            createLinkRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<UploadLinkCreationResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), createLinkRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<UploadLinkCreationResponse>(HttpStatusCode.Created));
        }
    }
}
