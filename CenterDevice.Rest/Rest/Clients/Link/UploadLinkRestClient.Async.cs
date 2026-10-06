using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System.Net;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Link
{
    public partial class UploadLinkRestClient
    {
        /// <summary>Performs the get link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="linkId">The link id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadLink> GetLinkAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linkRequest = CreateRestRequest(URI_RESOURCE + linkId, Method.Get);

            var result = (await ExecuteAsync<UploadLink>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), linkRequest, cancellationToken).ConfigureAwait(false));
            UploadLink resultUnwrapped = UnwrapResponse(result, new StatusCodeResponseHandler<UploadLink>(HttpStatusCode.OK));
            resultUnwrapped.UploadLinkBaseUrl = this.UploadLinkBaseUrl;
            return resultUnwrapped;
        }

        /// <summary>Performs the delete link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="linkId">The link id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task DeleteLinkAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linkRequest = CreateRestRequest(URI_RESOURCE + linkId, Method.Delete, ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<Link>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), linkRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the update link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="linkId">The link id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="name">The name for this operation.</param>
        /// <param name="tags">The tags for this operation.</param>
        /// <param name="expiryDate">The expiry date for this operation.</param>
        /// <param name="maxDocuments">The max documents for this operation.</param>
        /// <param name="password">The password for this operation.</param>
        /// <param name="emailCreatorOnUpload">The email creator on upload for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task UpdateLinkAsync(string userId, string linkId, string collectionId, string name, List<string> tags, DateTime? expiryDate, int? maxDocuments, string password, bool? emailCreatorOnUpload, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updateLinkRequest = CreateRestRequest(URI_RESOURCE + linkId, Method.Put, ContentType.APPLICATION_JSON);

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            if (collectionId != null) parameters[RestApiConstants.COLLECTION] = collectionId;
            if (name != null) parameters[RestApiConstants.NAME] = name;
            if (tags != null) parameters[RestApiConstants.TAGS] = JsonConvert.SerializeObject(tags);
            if (expiryDate.HasValue) parameters[RestApiConstants.EXPIRY_DATE] = expiryDate;
            if (maxDocuments.HasValue) parameters[RestApiConstants.MAX_DOCUMENTS] = maxDocuments;
            if (password != null) parameters[RestApiConstants.PASSWORD] = password;
            if (emailCreatorOnUpload.HasValue) parameters[RestApiConstants.EMAIL_CREATOR_ON_UPLOAD] = emailCreatorOnUpload;

            updateLinkRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<Link>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), updateLinkRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
