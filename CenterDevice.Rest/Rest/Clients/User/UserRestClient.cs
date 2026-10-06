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

        public FullUserData GetUserDataField(string userId, string userIdToRequest, string field)
        {
            var request = CreateRestRequest(URI_RESOURCE + "/" + userIdToRequest, Method.Get);
            request.AddQueryParameter(RestApiConstants.FIELDS, field);

            var response = Execute<FullUserData>(GetOAuthInfo(userId), request);
            return UnwrapResponse(response, new StatusCodeResponseHandler<FullUserData>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element