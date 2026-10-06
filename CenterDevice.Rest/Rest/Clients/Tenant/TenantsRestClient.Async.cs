using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantsRestClient
    {
        /// <summary>Performs the get tenants operation using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The o auth info for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<TenantResponse> GetTenantsAsync(OAuthInfo oAuthInfo, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantsRequest = CreateRestRequest(URI_RESOURCE, Method.Get);

            var response = (await ExecuteAsync<TenantResponse>(oAuthInfo, tenantsRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<TenantResponse>(HttpStatusCode.OK));
        }
    }
}
