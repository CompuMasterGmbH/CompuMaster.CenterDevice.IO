using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using RestSharp;
using System;

namespace CenterDevice.BrowserDemo
{
    internal sealed class CapturedTokenProvider : IOAuthInfoProvider, IRestClientConfiguration, IRestClientErrorHandler
    {
        private readonly OAuthInfo tokens;
        internal CapturedTokenProvider(OAuthInfo tokens) { this.tokens = tokens; }

        /// <inheritdoc/>
        public string BaseAddress => SessionTokens.ApiUrl;
        /// <inheritdoc/>
        public string UserAgent => "CompuMaster.CenterDevice.BrowserDemo/1.0";
        /// <inheritdoc/>
        public OAuthInfo GetOAuthInfo(string userId) => tokens;
        /// <inheritdoc/>
        public OAuthInfo RefreshToken(OAuthInfo oAuthInfo)
            => throw new InvalidOperationException("Die Websitzung ist abgelaufen. Bitte im Browser erneut anmelden; die Demo erneuert keine Tokens.");
        /// <inheritdoc/>
        public void ValidateResponse(RestResponse result)
        {
            if (!result.IsSuccessful) throw new InvalidOperationException("Die API-Anfrage ist fehlgeschlagen (HTTP " + (int)result.StatusCode + ").", result.ErrorException);
        }
    }
}
