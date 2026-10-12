using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantRestClient
    {
        /// <summary>Updates explicit tenant profile fields.</summary>
        /// <inheritdoc cref="UpdateTenantAsync"/>
        public void UpdateTenant(OAuthInfo oAuthInfo, string tenantId, TenantUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateTenantAsync(oAuthInfo, tenantId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates explicit tenant profile fields using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The existing login authorization; its tenant and role remain server-authoritative.</param>
        /// <param name="tenantId">The selected tenant identifier, or current to use the token's tenant.</param>
        /// <param name="request">The nonempty profile selection, snapshotted before HTTP I/O.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>Only admins may update tenant details. Omitted fields stay unchanged. Logo data must encode a PNG or JPEG of at most 5 MiB; the server enforces image validity and scales dimensions to 200 by 70 pixels if needed. Null logo data never removes an existing logo. No uncertain write is replayed; existing expired-token refresh is preserved. Cancellation cannot establish that no change occurred.</remarks>
        /// <exception cref="ArgumentException">The selected tenant or update values are invalid.</exception>
        /// <exception cref="ArgumentNullException">Authorization or update selection is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task UpdateTenantAsync(OAuthInfo oAuthInfo, string tenantId, TenantUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            var name = request.Name; var logo = request.LogoData;
            var body = new JObject();
            if (name != null) body["name"] = name;
            if (logo != null)
            {
                byte[] decoded;
                try { decoded = Convert.FromBase64String(logo); }
                catch (FormatException error) { throw new ArgumentException("Logo data must use Base64 encoding.", nameof(request), error); }
                if (decoded.Length == 0 || decoded.Length > 5 * 1024 * 1024)
                    throw new ArgumentException("Logo image data must contain at most 5 MiB of nonempty decoded bytes.", nameof(request));
                body["logo"] = new JObject { ["data"] = logo };
            }
            if (!body.HasValues) throw new ArgumentException("Select at least one tenant profile field.", nameof(request));
            return SendTenantProfileAsync(oAuthInfo, tenantId, body, cancellationToken);
        }

        /// <summary>Removes the selected tenant's logo explicitly.</summary>
        /// <inheritdoc cref="RemoveTenantLogoAsync"/>
        public void RemoveTenantLogo(OAuthInfo oAuthInfo, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveTenantLogoAsync(oAuthInfo, tenantId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes the selected tenant's logo using asynchronous HTTP I/O.</summary>
        /// <param name="oAuthInfo">The existing login authorization.</param>
        /// <param name="tenantId">The selected tenant identifier, or current to use the token's tenant.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>Only admins may remove the logo. This sends an explicit empty logo object and preserves the name. No uncertain write is replayed; existing expired-token refresh is preserved. Cancellation cannot establish that no change occurred.</remarks>
        /// <exception cref="ArgumentException">The selected tenant is unusable.</exception>
        /// <exception cref="ArgumentNullException">Authorization is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task RemoveTenantLogoAsync(OAuthInfo oAuthInfo, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SendTenantProfileAsync(oAuthInfo, tenantId, new JObject { ["logo"] = new JObject() }, cancellationToken);
        }

        private async Task SendTenantProfileAsync(OAuthInfo authorization, string tenantId, JObject body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (authorization == null) throw new ArgumentNullException(nameof(authorization));
            if (string.IsNullOrWhiteSpace(tenantId) || tenantId == "." || tenantId == "..")
                throw new ArgumentException("A selected tenant identifier is required.", nameof(tenantId));
            var request = CreateRestRequest(URI_RESOURCE + Uri.EscapeDataString(tenantId), Method.Put, "application/json");
            request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(authorization, request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
