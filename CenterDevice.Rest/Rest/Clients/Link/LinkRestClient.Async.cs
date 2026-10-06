using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Link
{
    public partial class LinkRestClient
    {
        /// <summary>Performs the get link operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="linkId">The link id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<Link> GetLinkAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var linkRequest = CreateRestRequest(URI_RESOURCE + linkId, Method.Get);

            var result = (await ExecuteAsync<Link>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), linkRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<Link>(HttpStatusCode.OK));
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
        /// <param name="accessControl">The access control for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task UpdateLinkAsync(string userId, string linkId, LinkAccessControl accessControl, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var updateLinkRequest = CreateRestRequest(URI_RESOURCE + linkId, Method.Put, ContentType.APPLICATION_JSON);

            var parameters = new JObject();
            parameters[RestApiConstants.ACCESS_CONTROL] = AccessControlConverter.ToJsonObject(accessControl);

            updateLinkRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<Link>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), updateLinkRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
