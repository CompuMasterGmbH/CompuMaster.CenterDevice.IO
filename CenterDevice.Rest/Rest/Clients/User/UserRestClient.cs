using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Tenant;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.User
{
    public partial class UserRestClient : CenterDeviceRestClient, IUserRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "user";
            }
        }

        public UserRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        public ExtendedUserData GetLoggedInUserData(OAuthInfo oAuthInfo)
        {
            var currentUserRequest = CreateRestRequest(URI_RESOURCE + "/current", Method.Get);

            var response = Execute<ExtendedUserData>(oAuthInfo, currentUserRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<ExtendedUserData>(HttpStatusCode.OK));
        }

        public BaseUserData GetUserData(string userId, string userIdToRequest)
        {
            var currentUserRequest = CreateRestRequest(URI_RESOURCE + "/" + userIdToRequest, Method.Get);

            var response = Execute<BaseUserData>(GetOAuthInfo(userId), currentUserRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<BaseUserData>(HttpStatusCode.OK));
        }

        /// <summary>Retrieves selected details for a user.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="userIdToRequest">The explicitly selected user identifier.</param>
        /// <param name="field">The comma-separated manufacturer data fields, or all for all available details.</param>
        /// <returns>The selected user details supplied by the server.</returns>
        /// <remarks>Selection uses the user resource's data query parameter. The server enforces visibility; groups and collections are available only for the current user. Existing signatures, models and other getter defaults remain unchanged.</remarks>
        public FullUserData GetUserDataField(string userId, string userIdToRequest, string field)
        {
            var request = CreateRestRequest(URI_RESOURCE + "/" + userIdToRequest, Method.Get);
            request.AddQueryParameter("data", field);

            var response = Execute<FullUserData>(GetOAuthInfo(userId), request);
            return UnwrapResponse(response, new StatusCodeResponseHandler<FullUserData>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
