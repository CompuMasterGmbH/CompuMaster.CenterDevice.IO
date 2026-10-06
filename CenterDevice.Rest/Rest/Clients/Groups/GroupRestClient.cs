using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupRestClient : CenterDeviceRestClient, IGroupRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "group/";
            }
        }

        public GroupRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        public Group GetGroup(string userId, string groupId)
        {
            var request = CreateRestRequest(URI_RESOURCE + groupId, Method.Get);

            var result = Execute<Group>(GetOAuthInfo(userId), request);
            return UnwrapResponse(result, new StatusCodeResponseHandler<Group>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element