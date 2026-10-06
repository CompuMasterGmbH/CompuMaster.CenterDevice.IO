using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Link
{
    public partial class LinksRestClient
    {
        /// <summary>Performs the create document link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="accessControl">The access control for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<LinkCreationResponse> CreateDocumentLinkAsync(string userId, string id, LinkAccessControl accessControl, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await CreateLinkAsync(userId, RestApiConstants.DOCUMENT_ID, id, accessControl, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the create folder link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="accessControl">The access control for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<LinkCreationResponse> CreateFolderLinkAsync(string userId, string id, LinkAccessControl accessControl, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await CreateLinkAsync(userId, RestApiConstants.FOLDER, id, accessControl, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the create collection link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="accessControl">The access control for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<LinkCreationResponse> CreateCollectionLinkAsync(string userId, string id, LinkAccessControl accessControl, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await CreateLinkAsync(userId, RestApiConstants.COLLECTION, id, accessControl, cancellationToken).ConfigureAwait(false));
        }

        private async Task<LinkCreationResponse> CreateLinkAsync(string userId, string field, string id, LinkAccessControl accessControl, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (id == null || field == null)
            {
                throw new ArgumentException("Id and field need to be set.");
            }

            var createLinkRequest = CreateRestRequest(URI_RESOURCE, Method.Post);
            JObject parameters = CreateJsonBody(field, id, accessControl);

            createLinkRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<LinkCreationResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), createLinkRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<LinkCreationResponse>(HttpStatusCode.Created));
        }
    }
}
