using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantRestClient : CenterDeviceRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "tenant/";
            }
        }

        public TenantRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfo, configuration, errorHandler, apiVersionPrefix) { }

        public TenantData GetTenant(OAuthInfo oAuthInfo, string tenantId)
        {
            var tenantRequest = CreateRestRequest(URI_RESOURCE + tenantId, Method.Get);

            var response = Execute<TenantData>(oAuthInfo, tenantRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<TenantData>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element