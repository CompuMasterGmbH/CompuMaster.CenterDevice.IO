using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantFeaturesRestClient
    {
        /// <summary>Retrieves immutable caller-visible tenant feature statuses.</summary>
        /// <inheritdoc cref="GetTenantFeaturesDetailsAsync"/>
        public TenantFeaturesDetails GetTenantFeaturesDetails(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
            => GetTenantFeaturesDetailsAsync(userId, id, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable caller-visible tenant feature statuses using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected tenant identifier, or current.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original feature status strings with nullable missing metadata and future names/values.</returns>
        /// <remarks>Non-administrators cannot distinguish inactive available features from unavailable features. No activation or permission inference is performed. Existing getters and activation behavior remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected tenant is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful feature metadata is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<TenantFeaturesDetails> GetTenantFeaturesDetailsAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); TenantDetailsParser.RequireId(id);
            var request = CreateRestRequest(ApiVersionPrefix + "tenant/" + Uri.EscapeDataString(id) + "/features", Method.Get);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return TenantDetailsParser.Parse(response.Content, root => new TenantFeaturesDetails(root));
        }
    }
}
