using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Globalization;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        protected const string AUTHORIZATION = "Authorization";
        private const string CONTENT_TYPE = "Content-Type";
        private const string BEARER = "Bearer ";

        private const string UNKNOWN_OR_EXPIRED_TOKEN = "Unknown or expired token";
        private const string EXPIRED_TENANT = "Tenant has expired";
        private const string OPERATION_TIMED_OUT_EXCEPTION = "The operation has timed out";

        protected readonly RestClient client;

        protected readonly IOAuthInfoProvider oAuthInfoProvider;
        protected readonly TimeSpan defaultDelay = TimeSpan.FromMinutes(1);
        protected readonly string ApiVersionPrefix;

        protected const string RETRY_AFTER = "Retry-After";
        protected const int TOO_MANY_REQUESTS = 429;

        private OfflineModeSimulator offlineModeSimulator = new OfflineModeSimulator();
        private readonly IRestClientErrorHandler errorHandler;

        public void DisableOfflineModeSimulation()
        {
            offlineModeSimulator = null;
        }

        protected CenterDeviceRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
        {
            this.errorHandler = errorHandler;
            this.oAuthInfoProvider = oauthInfo;
            this.ApiVersionPrefix = apiVersionPrefix;
            var options = new RestClientOptions(configuration.BaseAddress)
            {
                UserAgent = configuration.UserAgent,
                ConfigureMessageHandler = handler => new CenterDeviceHttpMessageHandler(handler)
            };
            client = new RestClient(options);

            //OPTIONAL: // Konfiguriert den JSON-Serializer explizit
            //OPTIONAL: client.UseSystemTextJson(); // Verwenden Sie System.Text.Json
            //OPTIONAL: // Alternativ: client.UseNewtonsoftJson(); // Verwenden Sie Newtonsoft.Json

            //remove all XML deserializers since response is always expected as JSON, never XML
            //EX RestSharp till 2023: client.UseJson();
            //NOT REQUIRED USUALLY: // Entfernen von XML-Deserialisierern (obwohl standardmäßig keine XML-Deserialisierer hinzugefügt werden)
            //NOT REQUIRED USUALLY: client.Serializers.Clear();


            CustomOptionBaseAddress = options.BaseUrl.AbsoluteUri;
            CustomOptionUserAgent = options.UserAgent;
        }

        protected readonly string CustomOptionUserAgent;
        
        protected readonly string CustomOptionBaseAddress;

        protected virtual RestResponse Execute(OAuthInfo oAuthInfo, RestRequest request)
        {
            return ExecuteAsync(oAuthInfo, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        protected virtual RestResponse<T> Execute<T>(OAuthInfo oAuthInfo, RestRequest request) where T : new()
        {
            return ExecuteAsync<T>(oAuthInfo, request).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void PrepareRequest(OAuthInfo oAuthInfo, RestRequest request)
        {
            offlineModeSimulator?.ThrowIfOffline();

            AddAuthorizationHeader(oAuthInfo, request);
        }

        protected TimeSpan? ExtractDelay(RestResponse result)
        {
            return ExtractDelay(result.Headers?.FirstOrDefault(parameter => string.Equals(parameter.Name, RETRY_AFTER, StringComparison.OrdinalIgnoreCase))?.Value?.ToString());
        }

        protected TimeSpan? ExtractDelay(string value)
        {
            if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
                return TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.MaxValue.TotalSeconds - 1));
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                return date > DateTimeOffset.UtcNow ? date - DateTimeOffset.UtcNow : TimeSpan.Zero;
            return null;
        }

        private bool IsRateLimitExceeded(RestResponse result)
        {
            return (int)result.StatusCode == TOO_MANY_REQUESTS;
        }

        private bool IsNotConnected(RestResponse result)
        {
            var exception = result.ErrorException as WebException;
            if (exception == null)
            {
                return false;
            }

            return exception.Status == WebExceptionStatus.ConnectFailure || exception.Status == WebExceptionStatus.NameResolutionFailure;
        }

        protected void ValidateResponse(RestResponse result, BaseResponseHandler handler)
        {
            errorHandler?.ValidateResponse(result);

            handler.ValidateResponse(result);
        }

        protected T UnwrapResponse<T>(RestResponse<T> result, DataResponseHandler<T> handler)
        {
            errorHandler?.ValidateResponse(result);

            handler.ValidateResponse(result);

            return handler.UnwrapResponse(result);
        }

        protected RestRequest CreateRestRequest(string path, Method method, string contentType)
        {
            return AddContentTypeHeader(CreateRestRequest(path, method), contentType);
        }

        protected RestRequest CreateRestRequest(string path, Method method)
        {
            RestRequest Result = new RestRequest(path, method);
            Result.RequestFormat = DataFormat.Json;
            return Result;
        }

        protected OAuthInfo GetOAuthInfo(string userId)
        {
            return oAuthInfoProvider.GetOAuthInfo(userId);
        }

        protected RestRequest AddContentTypeHeader(RestRequest request, string contentType)
        {
            if (request.Method == Method.Get) throw new ArgumentException("Adding header Content-Type not supported for GET requests", nameof(request));
            request.AddHeader(CONTENT_TYPE, contentType);
            return request;
        }

        protected string GetAuthorizationBearer(string userId)
        {
            return GetAuthorizationBearer(oAuthInfoProvider.GetOAuthInfo(userId));
        }

        protected string GetAuthorizationBearer(OAuthInfo oAuthInfo)
        {
            return BEARER + oAuthInfo.access_token;
        }

        private void AddAuthorizationHeader(OAuthInfo oAuthInfo, RestRequest request)
        {
            request.AddHeader(AUTHORIZATION, GetAuthorizationBearer(oAuthInfo));
        }

        private void SwapAuthorizationHeader(OAuthInfo newOAuthInfo, RestRequest request)
        {
            request.AddOrUpdateHeader(AUTHORIZATION, GetAuthorizationBearer(newOAuthInfo));
        }

        private bool IsExpiredToken(RestResponse result)
        {
            return result.StatusCode == HttpStatusCode.Unauthorized
                && (result.Content == UNKNOWN_OR_EXPIRED_TOKEN || result.Content == EXPIRED_TENANT);
        }

        private bool IsOperationTimedOut(RestResponse result)
        {
            if (result.ErrorMessage != null)
            {
                return result.ErrorMessage.Contains(OPERATION_TIMED_OUT_EXCEPTION);
            }

            return false;
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
