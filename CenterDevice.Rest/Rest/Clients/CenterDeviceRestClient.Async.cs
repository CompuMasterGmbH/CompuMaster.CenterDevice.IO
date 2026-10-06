using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> AuthenticationGates =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Executes an authorized request using native asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The authorization information for this request.</param>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The HTTP response after at most one expired-token refresh.</returns>
        protected virtual Task<RestResponse> ExecuteAsync(OAuthInfo oAuthInfo, RestRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteAuthorizedAsync(oAuthInfo, request, cancellationToken,
                token => client.ExecuteAsync(request, token));
        }

        /// <summary>Executes an authorized request and deserializes its response using native asynchronous HTTP I/O.</summary>
        /// <typeparam name="T">The response data type.</typeparam>
        /// <param name="oAuthInfo">The authorization information for this request.</param>
        /// <param name="request">The request to send.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The HTTP response after at most one expired-token refresh.</returns>
        protected virtual Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo oAuthInfo, RestRequest request, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            return ExecuteAuthorizedAsync(oAuthInfo, request, cancellationToken,
                token => client.ExecuteAsync<T>(request, token));
        }

        private async Task<TResponse> ExecuteAuthorizedAsync<TResponse>(OAuthInfo oAuthInfo, RestRequest request,
            CancellationToken cancellationToken, Func<CancellationToken, Task<TResponse>> send) where TResponse : RestResponse
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareRequest(oAuthInfo, request);
            var response = await send(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsExpiredToken(response))
            {
                var refreshed = await RefreshAuthorizationAsync(oAuthInfo, cancellationToken).ConfigureAwait(false);
                if (refreshed != null)
                {
                    SwapAuthorizationHeader(refreshed, request);
                    response = await send(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            if (IsRateLimitExceeded(response)) throw new TooManyRequestsException(ExtractDelay(response));
            if (IsNotConnected(response)) throw new NotConnectedException(response.ErrorMessage, response.ErrorException);
            if (IsOperationTimedOut(response)) throw new OperationTimedOutException(response.ErrorMessage, response.ErrorException);
            return response;
        }

        /// <summary>Retrieves authorization information without blocking the calling thread.</summary>
        /// <param name="userId">The existing authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels waiting; legacy synchronous providers cannot interrupt an active authorization callback.</param>
        /// <returns>The authorization information supplied by the configured provider.</returns>
        protected async Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (oAuthInfoProvider is IAsyncOAuthInfoProvider asyncProvider)
                return await asyncProvider.GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false);
            return await RunLegacyAuthorizationAsync(() => GetOAuthInfo(userId), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Refreshes authorization using the configured asynchronous or legacy handler.</summary>
        /// <param name="info">The rejected authorization information.</param>
        /// <param name="cancellationToken">Cancels asynchronous refresh or waiting for a legacy callback.</param>
        /// <returns>The replacement authorization information, or null when refresh is unavailable.</returns>
        protected async Task<OAuthInfo> RefreshAuthorizationAsync(OAuthInfo info, CancellationToken cancellationToken)
        {
            if (errorHandler == null) return null;
            if (errorHandler is IAsyncRestClientErrorHandler asyncHandler)
                return await asyncHandler.RefreshTokenAsync(info, cancellationToken).ConfigureAwait(false);
            return await RunLegacyAuthorizationAsync(() => errorHandler.RefreshToken(info), cancellationToken).ConfigureAwait(false);
        }

        private async Task<OAuthInfo> RunLegacyAuthorizationAsync(Func<OAuthInfo> operation, CancellationToken cancellationToken)
        {
            var key = new Uri(CustomOptionBaseAddress).GetLeftPart(UriPartial.Authority);
            var gate = AuthenticationGates.GetOrAdd(key, value => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // This compatibility callback is the only worker fallback; document HTTP I/O is native async.
                return await Task.Run(operation).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
    }
}
