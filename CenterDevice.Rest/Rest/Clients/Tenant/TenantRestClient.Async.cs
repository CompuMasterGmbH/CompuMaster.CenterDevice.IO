using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantRestClient
    {
        /// <summary>Performs the get tenant operation using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The o auth info for this operation.</param>
        /// <param name="tenantId">The tenant id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<TenantData> GetTenantAsync(OAuthInfo oAuthInfo, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantRequest = CreateRestRequest(URI_RESOURCE + tenantId, Method.Get);

            var response = (await ExecuteAsync<TenantData>(oAuthInfo, tenantRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<TenantData>(HttpStatusCode.OK));
        }
    }
}
