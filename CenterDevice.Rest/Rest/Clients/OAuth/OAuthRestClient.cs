using log4net;
using RestSharp;
using RestSharp.Extensions;
using System;
using System.Net;
using System.Text;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.OAuth
{
    public partial class OAuthRestClient : IOAuthRestClient
    {
        private static ILog logger = LogManager.GetLogger(typeof(OAuthRestClient));

        protected static readonly string TOKEN_ENDPOINT = "token";

        private const string CONTENT_TYPE = "Content-Type";
        private const string AUTHORIZATION = "Authorization";
        protected readonly IOAuthClientConfiguration configuration;

        protected RestClient Client { get; set; }

        public OAuthRestClient(IOAuthClientConfiguration configuration)
        {
            this.configuration = configuration;

            RestClientOptions options = new RestClientOptions(configuration.BaseAddress);
            options.UserAgent = configuration.UserAgent;
            options.ConfigureMessageHandler = handler => new CenterDeviceHttpMessageHandler(handler);
            Client = new RestClient(options);
        }

        public RestResponse<OAuthInfo> SwapToken(OAuthInfo oAuthInfo, string userId)
        {
            return SwapToken(BuildSwapTokenBodyMessageForUserId(oAuthInfo.access_token, userId));
        }

        public RestResponse<OAuthInfo> SwapToken(OAuthInfo oAuthInfo, string email, string tenantId)
        {
            return SwapToken(BuildSwapTokenBodyMessageForEmailAndTenantId(oAuthInfo.access_token, email, tenantId));
        }

        public RestResponse<OAuthInfo> RefreshToken(OAuthInfo oAuthInfo)
        {
            var request = new RestRequest(TOKEN_ENDPOINT, Method.Post);
            AddAuthHeader(request);

            request.AddParameter(
                ContentType.APPLICATION_FORM_URLENCODED,
                BuildRefreshTokenBodyMessage(oAuthInfo.refresh_token),
                ParameterType.RequestBody);

            return Client.ExecuteAsync<OAuthInfo>(request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public RestResponse<OAuthInfo> DestroyToken(OAuthInfo oAuthInfo)
        {

            var request = new RestRequest(TOKEN_ENDPOINT, Method.Post);
            AddAuthHeader(request);

            request.AddParameter(
                ContentType.APPLICATION_FORM_URLENCODED,
                BuildDestroyTokensBodyMessage(oAuthInfo.access_token, oAuthInfo.refresh_token),
                ParameterType.RequestBody);

            return Client.ExecuteAsync<OAuthInfo>(request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private RestResponse<OAuthInfo> SwapToken(string body)
        {

            var request = new RestRequest(TOKEN_ENDPOINT, Method.Post);
            AddAuthHeader(request);

            request.AddParameter(ContentType.APPLICATION_FORM_URLENCODED, body, ParameterType.RequestBody);

            return Client.ExecuteAsync<OAuthInfo>(request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private string BuildSwapTokenBodyMessageForEmailAndTenantId(string accessToken, string email, string tenantId)
        {
            string body = StartSwapTokenBodyMessage(accessToken);
            if (!string.IsNullOrEmpty(email))
            {
                body += "&username=" + GetEncodedValue(email);
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                body += "&tenant=" + GetEncodedValue(tenantId);
            }

            return body;
        }

        private string BuildSwapTokenBodyMessageForUserId(string accessToken, string userId)
        {
            string body = StartSwapTokenBodyMessage(accessToken);
            if (!string.IsNullOrEmpty(userId))
            {
                body += "&user_id=" + GetEncodedValue(userId);
            }

            return body;
        }

        private string StartSwapTokenBodyMessage(string accessToken)
        {
            string body = "grant_type=swap_token";
            if (!string.IsNullOrEmpty(accessToken))
            {
                body += "&access_token=" + GetEncodedValue(accessToken);
            }

            return body;
        }

        private string BuildRefreshTokenBodyMessage(string refreshToken)
        {
            return "grant_type=refresh_token&refresh_token=" + GetEncodedValue(refreshToken);
        }

        private string BuildDestroyTokensBodyMessage(string accessToken, string refreshToken)
        {
            string body = "grant_type=destroy_token";
            if (!string.IsNullOrEmpty(accessToken))
            {
                body += "&access_token=" + GetEncodedValue(accessToken);
            }

            if (!string.IsNullOrEmpty(refreshToken))
            {
                body += "&refresh_token=" + GetEncodedValue(refreshToken);
            }

            return body;
        }

        private string GetEncodedValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return WebUtility.UrlEncode(value);
        }

        protected void AddAuthHeader(RestRequest request)
        {
            request.AddHeader(CONTENT_TYPE, ContentType.APPLICATION_FORM_URLENCODED);
            request.AddHeader(AUTHORIZATION, "Basic " + GetAuthHeader());
        }

        private string GetAuthHeader()
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Format("{0}:{1}",
                           configuration.ClientId,
                           configuration.ClientSecret)));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
