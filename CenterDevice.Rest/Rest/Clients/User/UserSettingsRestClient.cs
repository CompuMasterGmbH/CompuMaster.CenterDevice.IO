using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.User
{
    public partial class UserSettingsRestClient : CenterDeviceRestClient, IUserSettingsRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "user/{0}/settings";
            }
        }

        public UserSettingsRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfo, configuration, errorHandler, apiVersionPrefix)
        {
        }

        public UserSettings GetUserSettings(string userId)
        {
            var tenantRequest = CreateRestRequest(string.Format(URI_RESOURCE, userId), Method.Get);

            var response = Execute<UserSettings>(GetOAuthInfo(userId), tenantRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<UserSettings>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element