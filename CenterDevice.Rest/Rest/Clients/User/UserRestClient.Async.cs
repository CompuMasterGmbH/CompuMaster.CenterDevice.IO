using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Tenant;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UserRestClient
    {
        /// <summary>Retrieves the authenticated user's details using the configured authorization provider and native asynchronous I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, and active HTTP I/O.</param>
        /// <returns>The authenticated user's details.</returns>
        public async Task<ExtendedUserData> GetAuthenticatedUserDataAsync(string userId, CancellationToken cancellationToken = default(CancellationToken)) =>
            await GetLoggedInUserDataAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

        /// <summary>Performs the get logged in user data operation using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The o auth info for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<ExtendedUserData> GetLoggedInUserDataAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentUserRequest = CreateRestRequest(URI_RESOURCE + "/current", Method.Get);

            var response = (await ExecuteAsync<ExtendedUserData>(oAuthInfo, currentUserRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<ExtendedUserData>(HttpStatusCode.OK));
        }

        /// <summary>Performs the get user data operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="userIdToRequest">The user id to request for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<BaseUserData> GetUserDataAsync(string userId, string userIdToRequest, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentUserRequest = CreateRestRequest(URI_RESOURCE + "/" + userIdToRequest, Method.Get);

            var response = (await ExecuteAsync<BaseUserData>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), currentUserRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<BaseUserData>(HttpStatusCode.OK));
        }

        /// <summary>Performs the get user data field operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="userIdToRequest">The user id to request for this operation.</param>
        /// <param name="field">The field for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<FullUserData> GetUserDataFieldAsync(string userId, string userIdToRequest, string field, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE + "/" + userIdToRequest, Method.Get);
            request.AddQueryParameter(RestApiConstants.FIELDS, field);

            var response = (await ExecuteAsync<FullUserData>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<FullUserData>(HttpStatusCode.OK));
        }
    }
}
