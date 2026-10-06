using RestSharp;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.OAuth
{
    public partial class OAuthRestClient
    {
        /// <summary>Exchanges an access token for a user context using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The current authorization information.</param>
        /// <param name="userId">The target user identifier.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The token-exchange response.</returns>
        public Task<RestResponse<OAuthInfo>> SwapTokenAsync(OAuthInfo oAuthInfo, string userId, CancellationToken cancellationToken = default(CancellationToken))
            => SendTokenAsync(BuildSwapTokenBodyMessageForUserId(oAuthInfo.access_token, userId), cancellationToken);

        /// <summary>Exchanges an access token for a tenant context using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The current authorization information.</param>
        /// <param name="email">The target user's email address.</param>
        /// <param name="tenantId">The target tenant identifier.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The token-exchange response.</returns>
        public Task<RestResponse<OAuthInfo>> SwapTokenAsync(OAuthInfo oAuthInfo, string email, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
            => SendTokenAsync(BuildSwapTokenBodyMessageForEmailAndTenantId(oAuthInfo.access_token, email, tenantId), cancellationToken);

        /// <summary>Refreshes an access token using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The authorization information containing the refresh token.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The token-refresh response.</returns>
        public Task<RestResponse<OAuthInfo>> RefreshTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken = default(CancellationToken))
            => SendTokenAsync(BuildRefreshTokenBodyMessage(oAuthInfo.refresh_token), cancellationToken);

        /// <summary>Revokes authorization tokens using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The authorization tokens to revoke.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The revocation response.</returns>
        public Task<RestResponse<OAuthInfo>> DestroyTokenAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken = default(CancellationToken))
            => SendTokenAsync(BuildDestroyTokensBodyMessage(oAuthInfo.access_token, oAuthInfo.refresh_token), cancellationToken);

        private async Task<RestResponse<OAuthInfo>> SendTokenAsync(string body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = new RestRequest(TOKEN_ENDPOINT, Method.Post);
            AddAuthHeader(request);
            request.AddParameter(ContentType.APPLICATION_FORM_URLENCODED, body, ParameterType.RequestBody);
            var response = await Client.ExecuteAsync<OAuthInfo>(request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return response;
        }
    }
}
