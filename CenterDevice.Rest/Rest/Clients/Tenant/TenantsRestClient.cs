using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantsRestClient : CenterDeviceRestClient, ITenantsRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "tenants";
            }
        }

        public TenantsRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfo, configuration, errorHandler, apiVersionPrefix) { }

        public TenantResponse GetTenants(OAuthInfo oAuthInfo)
        {
            var tenantsRequest = CreateRestRequest(URI_RESOURCE, Method.Get);

            var response = Execute<TenantResponse>(oAuthInfo, tenantsRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<TenantResponse>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element