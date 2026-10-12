using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantsRestClient
    {
        /// <summary>Sets the login's default tenant explicitly.</summary>
        /// <inheritdoc cref="SetDefaultTenantAsync"/>
        public void SetDefaultTenant(OAuthInfo oAuthInfo, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
            => SetDefaultTenantAsync(oAuthInfo, tenantId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Sets the login's default tenant using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The existing authorization identifying the login.</param>
        /// <param name="tenantId">The explicitly selected new default tenant identifier.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>The server verifies that the selected tenant belongs to the login. This changes the login preference only when expressly called; it does not switch a client's authorization, select an instance automatically or alter existing getters. No uncertain write is replayed; existing expired-token refresh is preserved. Cancellation cannot establish that no change occurred.</remarks>
        /// <exception cref="ArgumentException">The selected tenant is unusable.</exception>
        /// <exception cref="ArgumentNullException">Authorization is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task SetDefaultTenantAsync(OAuthInfo oAuthInfo, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (oAuthInfo == null) throw new ArgumentNullException(nameof(oAuthInfo));
            if (string.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("A new default tenant identifier is required.", nameof(tenantId));
            var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json");
            request.AddJsonBody(new { action = "set-default-tenant", @params = new { tenant = tenantId } });
            var response = await ExecuteAsync(oAuthInfo, request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
