using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UserRestClient
    {
        /// <summary>Retrieves immutable selected user details.</summary>
        /// <inheritdoc cref="GetUserDetailsAsync"/>
        public UserDetails GetUserDetails(string userId, string id, IEnumerable<string> data = null, bool? includeDeleted = null,
            CancellationToken cancellationToken = default(CancellationToken))
            => GetUserDetailsAsync(userId, id, data, includeDeleted, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves immutable selected user details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected user identifier, or current for the token's current user.</param>
        /// <param name="data">The nonempty selection of individual data fields, or null to omit the query and retain the server default of all.</param>
        /// <param name="includeDeleted">Whether to request an available deleted profile, or null to omit the query.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original nullable user metadata, including picture, groups and visible collections when exposed.</returns>
        /// <remarks>The server enforces visibility; groups and collections are provided only for the current user. Omitted fields remain unknown instead of being replaced with defaults. The tenant data selection is returned as tenant-name. Existing getters, interfaces and models remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected user or supplied field selection is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata is malformed or identifies a different selected user.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<UserDetails> GetUserDetailsAsync(string userId, string id, IEnumerable<string> data = null, bool? includeDeleted = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A selected user identifier is required.", nameof(id));
            var fields = data?.ToArray();
            if (fields != null && (fields.Length == 0 || fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(","))))
                throw new ArgumentException("A supplied data selection must contain nonblank individual field names.", nameof(data));
            var request = CreateRestRequest(URI_RESOURCE + "/" + Uri.EscapeDataString(id), Method.Get);
            if (fields != null) request.AddQueryParameter("data", string.Join(",", fields));
            if (includeDeleted.HasValue) request.AddQueryParameter("include-deleted", includeDeleted.Value ? "true" : "false");
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return UserDetailsParser.Parse(response.Content, id);
        }
    }
}
