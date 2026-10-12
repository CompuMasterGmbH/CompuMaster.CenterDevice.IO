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
    public partial class UsersRestClient
    {
        /// <summary>Retrieves immutable server-selected user-list details.</summary>
        /// <inheritdoc cref="GetUsersDetailsAsync"/>
        public IReadOnlyList<UserListingDetails> GetUsersDetails(string userId, IEnumerable<string> userStatuses = null, bool? includeDeleted = null,
            bool? all = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetUsersDetailsAsync(userId, userStatuses, includeDeleted, all, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves immutable user-list details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="userStatuses">The nonempty status selection, or null to omit the status query. Future status strings are retained.</param>
        /// <param name="includeDeleted">Whether to request available deleted profiles, or null to omit the query and retain the server default of false.</param>
        /// <param name="all">The deprecated manufacturer all query, or null to omit it. The server ignores it when statuses are selected; status selection is preferred.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only list in server order with nullable metadata and explicit technical-user values.</returns>
        /// <remarks>The OAuth token selects the tenant. Status/all filtering is available only to internal users and administrators; external users and guests receive all visible users regardless of those filters. No client-side filtering, permission or missing-user inference is performed. Existing GetAllUsers methods, interfaces and models remain unchanged.</remarks>
        /// <exception cref="ArgumentException">A supplied status selection is empty or contains null, blank or comma-containing values.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">The successful response has invalid user-list metadata or structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<IReadOnlyList<UserListingDetails>> GetUsersDetailsAsync(string userId, IEnumerable<string> userStatuses = null,
            bool? includeDeleted = null, bool? all = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var statuses = userStatuses?.ToArray();
            if (statuses != null && (statuses.Length == 0 || statuses.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(","))))
                throw new ArgumentException("A supplied status selection must contain nonblank individual statuses.", nameof(userStatuses));
            var request = CreateRestRequest(ApiVersionPrefix + "users", Method.Get);
            if (statuses != null) request.AddQueryParameter("status", string.Join(",", statuses));
            if (includeDeleted.HasValue) request.AddQueryParameter("include-deleted", includeDeleted.Value ? "true" : "false");
            if (all.HasValue) request.AddQueryParameter("all", all.Value ? "true" : "false");
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return UserListingDetailsParser.Parse(response.Content);
        }
    }
}
