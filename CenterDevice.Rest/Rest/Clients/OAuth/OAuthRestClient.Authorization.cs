using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.OAuth
{
    /// <summary>Provides CenterDevice OAuth token and authorization operations.</summary>
    public partial class OAuthRestClient
    {
        /// <summary>Creates an authorization URI for caller-owned browser navigation.</summary>
        /// <param name="state">A nonempty, unpredictable value bound to this authorization attempt and validated on return.</param>
        /// <param name="tenantId">The preferred tenant identifier, or null to use the server's default tenant.</param>
        /// <returns>The authorization URI without client secrets.</returns>
        /// <remarks>The caller owns browser navigation and callback acquisition. Retains the exact configured redirect URI. BaseAddress identifies the directory containing authorize and token. Requires manufacturer client and redirect registration. Unlike the protocol's optional state parameter, this helper requires state to protect the callback.</remarks>
        /// <exception cref="ArgumentException">State, client ID, auth address or redirect configuration is invalid.</exception>
        public Uri CreateAuthorizationUri(string state, string tenantId = null)
        {
            RequireValue(state, nameof(state));
            RequireValue(configuration.ClientId, "ClientId");
            var endpoint = GetAuthorizationEndpoint();
            GetRegisteredRedirect();
            string query = "client_id=" + GetEncodedValue(configuration.ClientId)
                + "&redirect_uri=" + GetEncodedValue(configuration.RedirectUri)
                + "&response_type=code&state=" + GetEncodedValue(state);
            if (!string.IsNullOrEmpty(tenantId)) query += "&tenant=" + GetEncodedValue(tenantId);
            return new Uri(endpoint.AbsoluteUri + "?" + query);
        }

        /// <summary>Exchanges a validated authorization callback for tokens.</summary>
        /// <param name="authorizationResponse">The absolute callback URI received by the caller.</param>
        /// <param name="expectedState">The nonempty state supplied for this authorization attempt.</param>
        /// <returns>The token response, including server error diagnostics.</returns>
        /// <remarks>Validates redirect identity and state before HTTP I/O. Does not open a browser, cache tokens, infer user identity, or replay a failed exchange. The caller must allow only one exchange per authorization attempt and keep configuration unchanged during that attempt.</remarks>
        /// <exception cref="ArgumentException">The expected state or configuration is invalid.</exception>
        /// <exception cref="AuthorizationException">The callback is invalid, denied, ambiguous or belongs to another authorization attempt.</exception>
        public RestResponse<OAuthInfo> ExchangeAuthorizationCode(Uri authorizationResponse, string expectedState)
            => ExchangeAuthorizationCodeAsync(authorizationResponse, expectedState).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Exchanges a validated authorization callback for tokens using asynchronous HTTP I/O.</summary>
        /// <param name="authorizationResponse">The absolute callback URI received by the caller.</param>
        /// <param name="expectedState">The nonempty state supplied for this authorization attempt.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The token response, including server error diagnostics.</returns>
        /// <remarks>Uses the exact configured redirect string for exchange. Does not assign UserId, Email or TenantId from a preferred tenant or authorization code; resolve authenticated identity separately. Token POSTs are not automatically replayed. The caller owns browser navigation, callback acquisition and one-time state consumption, and must keep configuration unchanged during the attempt.</remarks>
        /// <exception cref="ArgumentException">The expected state or configuration is invalid.</exception>
        /// <exception cref="AuthorizationException">The callback is invalid, denied, ambiguous or belongs to another authorization attempt.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<RestResponse<OAuthInfo>> ExchangeAuthorizationCodeAsync(Uri authorizationResponse, string expectedState, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireValue(expectedState, nameof(expectedState));
            RequireValue(configuration.ClientId, "ClientId");
            GetAuthorizationEndpoint();
            var registered = GetRegisteredRedirect();
            if (authorizationResponse == null || !authorizationResponse.IsAbsoluteUri
                || !string.Equals(authorizationResponse.GetLeftPart(UriPartial.Path), registered.GetLeftPart(UriPartial.Path), StringComparison.Ordinal)
                || !string.IsNullOrEmpty(authorizationResponse.Fragment))
                throw new AuthorizationException("The authorization redirect does not match the registered redirect URI.");
            var query = ParseCallbackQuery(authorizationResponse.Query);
            foreach (var parameter in ParseCallbackQuery(registered.Query))
                if (!query.TryGetValue(parameter.Key, out var value) || value != parameter.Value)
                    throw new AuthorizationException("The authorization redirect does not retain its registered query parameters.");
            if (!query.TryGetValue("state", out var actualState) || !string.Equals(actualState, expectedState, StringComparison.Ordinal))
                throw new AuthorizationException("The authorization state does not match this authorization attempt.");
            if (query.ContainsKey("error"))
                throw new AuthorizationException("The authorization server rejected the authorization request.");
            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
                throw new AuthorizationException("The authorization redirect does not contain an authorization code.");
            return SendTokenAsync("grant_type=authorization_code&redirect_uri=" + GetEncodedValue(configuration.RedirectUri)
                + "&code=" + GetEncodedValue(code), cancellationToken);
        }

        /// <summary>Refreshes an access token with explicit lifecycle options.</summary>
        /// <param name="oAuthInfo">The authorization information containing the refresh token.</param>
        /// <param name="force">Generates a new access token even if the current token has not expired.</param>
        /// <param name="prolong">Extends an existing access token's expiry; requires an existing token.</param>
        /// <returns>The token response, including server error diagnostics.</returns>
        /// <remarks>False options are omitted to retain the existing server defaults. Does not mutate the supplied token information or replay the token POST.</remarks>
        /// <exception cref="ArgumentException">Both force and prolong are true, or the refresh token is missing.</exception>
        /// <exception cref="ArgumentNullException">Authorization information is null.</exception>
        public RestResponse<OAuthInfo> RefreshToken(OAuthInfo oAuthInfo, bool force, bool prolong)
            => RefreshTokenAsync(oAuthInfo, force, prolong).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Refreshes an access token asynchronously with explicit lifecycle options.</summary>
        /// <param name="oAuthInfo">The authorization information containing the refresh token.</param>
        /// <param name="force">Generates a new access token even if the current token has not expired.</param>
        /// <param name="prolong">Extends an existing access token's expiry; requires an existing token.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>The token response, including server error diagnostics.</returns>
        /// <remarks>Force and prolong are mutually exclusive. False options are omitted. Does not mutate the supplied token information or replay the token POST.</remarks>
        /// <exception cref="ArgumentException">Both force and prolong are true, or the refresh token is missing.</exception>
        /// <exception cref="ArgumentNullException">Authorization information is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<RestResponse<OAuthInfo>> RefreshTokenAsync(OAuthInfo oAuthInfo, bool force, bool prolong, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (oAuthInfo == null) throw new ArgumentNullException(nameof(oAuthInfo));
            if (force && prolong) throw new ArgumentException("Force and prolong cannot both be enabled.");
            RequireValue(oAuthInfo.refresh_token, nameof(oAuthInfo));
            string body = BuildRefreshTokenBodyMessage(oAuthInfo.refresh_token);
            if (force) body += "&force=true";
            if (prolong) body += "&prolong=true";
            return SendTokenAsync(body, cancellationToken);
        }

        private Uri GetAuthorizationEndpoint()
        {
            if (!Uri.TryCreate(configuration.BaseAddress, UriKind.Absolute, out var address)
                || address.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(address.UserInfo)
                || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment))
                throw new ArgumentException("The configured authorization address must be an absolute HTTPS URI without user information, query or fragment.");
            return new Uri(address.AbsoluteUri.TrimEnd('/') + "/authorize");
        }

        private Uri GetRegisteredRedirect()
        {
            if (!Uri.TryCreate(configuration.RedirectUri, UriKind.Absolute, out var redirect)
                || (redirect.Scheme != Uri.UriSchemeHttps && redirect.Scheme != Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(redirect.UserInfo) || !string.IsNullOrEmpty(redirect.Fragment))
                throw new ArgumentException("The configured redirect must be an absolute HTTP or HTTPS URI without user information or fragment.");
            var query = ParseCallbackQuery(redirect.Query);
            foreach (var reserved in new[] { "code", "state", "error", "error_description" })
                if (query.ContainsKey(reserved)) throw new ArgumentException("The configured redirect query contains an OAuth response parameter.");
            return redirect;
        }

        private static Dictionary<string, string> ParseCallbackQuery(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(query)) return values;
            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                int separator = pair.IndexOf('=');
                var key = WebUtility.UrlDecode(separator < 0 ? pair : pair.Substring(0, separator));
                var value = WebUtility.UrlDecode(separator < 0 ? "" : pair.Substring(separator + 1));
                if (string.IsNullOrEmpty(key) || values.ContainsKey(key))
                    throw new AuthorizationException("The authorization redirect contains ambiguous query parameters.");
                values.Add(key, value);
            }
            return values;
        }

        private static void RequireValue(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A nonempty value is required.", name);
        }
    }
}
