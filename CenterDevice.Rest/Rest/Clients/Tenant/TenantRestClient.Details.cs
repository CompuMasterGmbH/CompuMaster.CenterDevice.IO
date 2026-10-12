using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantRestClient
    {
        /// <summary>Retrieves immutable selected tenant details.</summary>
        /// <inheritdoc cref="GetTenantDetailsAsync"/>
        public TenantDetails GetTenantDetails(string userId, string id, IEnumerable<string> data = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetTenantDetailsAsync(userId, id, data, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable selected tenant details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected tenant identifier, or current.</param>
        /// <param name="data">The nonempty individual field selection, or null to retain the server default of all.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original nullable tenant state and encoded logo metadata without default-state inference.</returns>
        /// <remarks>The server controls tenant access. Existing getters, models and current instance selection remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected tenant or supplied field selection is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata is malformed or identifies a different tenant.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<TenantDetails> GetTenantDetailsAsync(string userId, string id, IEnumerable<string> data = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); TenantDetailsParser.RequireId(id);
            var fields = data?.ToArray();
            if (fields != null && (fields.Length == 0 || fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(","))))
                throw new ArgumentException("A supplied data selection must contain nonblank individual fields.", nameof(data));
            var request = CreateRestRequest(ApiVersionPrefix + "tenant/" + Uri.EscapeDataString(id), Method.Get);
            if (fields != null) request.AddQueryParameter("data", string.Join(",", fields));
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return TenantDetailsParser.Parse(response.Content, root =>
            {
                var result = new TenantDetails(root);
                if (result.Id != null && (string.IsNullOrWhiteSpace(result.Id) || id != "current" && TenantDetailsParser.Identity(result.Id) != TenantDetailsParser.Identity(id)))
                    throw new InvalidDataException("The returned tenant identity differs from the selection.");
                return result;
            });
        }
    }
}
