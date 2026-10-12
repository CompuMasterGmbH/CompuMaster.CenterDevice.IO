using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantsRestClient
    {
        /// <summary>Retrieves immutable tenant information associated with the authenticated login.</summary>
        /// <inheritdoc cref="GetLoginTenantsDetailsAsync"/>
        public TenantLoginDetails GetLoginTenantsDetails(string userId, IEnumerable<string> data = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetLoginTenantsDetailsAsync(userId, data, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable login tenant information using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="data">The nonempty individual field selection, or null to retain the server default of all.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original ordered tenant/user metadata and the login default preference when exposed.</returns>
        /// <remarks>The OAuth token identifies the login. Omitted projections remain unknown; listing tenants does not change the current token or instance selection. Existing getters, models and default behavior remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The supplied field selection is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful login tenant metadata is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<TenantLoginDetails> GetLoginTenantsDetailsAsync(string userId, IEnumerable<string> data = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var fields = data?.ToArray();
            if (fields != null && (fields.Length == 0 || fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(","))))
                throw new ArgumentException("A supplied data selection must contain nonblank individual fields.", nameof(data));
            var request = CreateRestRequest(ApiVersionPrefix + "tenants", Method.Get);
            if (fields != null) request.AddQueryParameter("data", string.Join(",", fields));
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return TenantDetailsParser.Parse(response.Content, root => new TenantLoginDetails(root));
        }
    }
}
