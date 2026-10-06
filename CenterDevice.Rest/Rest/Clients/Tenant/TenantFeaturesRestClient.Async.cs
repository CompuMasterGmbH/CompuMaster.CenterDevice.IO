using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantFeaturesRestClient
    {
        /// <summary>Performs the get tenant features operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="tenantId">The tenant id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<TenantFeatures> GetTenantFeaturesAsync(string userId, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantRequest = CreateRestRequest(string.Format(URI_RESOURCE, tenantId), Method.Get);

            var response = (await ExecuteAsync<TenantFeatures>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), tenantRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<TenantFeatures>(HttpStatusCode.OK));
        }

        /// <summary>Performs the enable tenant feature operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="tenantId">The tenant id for this operation.</param>
        /// <param name="feature">The feature for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task EnableTenantFeatureAsync(string userId, string tenantId, string feature, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(string.Format(URI_RESOURCE, tenantId), Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject
            {
                [RestApiConstants.ACTION] = RestApiConstants.ENABLE,
                [RestApiConstants.PARAMS] = new JObject
                {
                    [RestApiConstants.FEATURE] = feature
                }
            };
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            RestResponse result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
