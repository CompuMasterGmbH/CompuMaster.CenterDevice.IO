using CenterDevice.Model.Groups;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupsRestClient
    {
        /// <summary>Retrieves immutable server-selected group details.</summary>
        /// <inheritdoc cref="GetGroupsDetailsAsync"/>
        public IReadOnlyList<GroupDetails> GetGroupsDetails(string userId, GroupsFilter? filter = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetGroupsDetailsAsync(userId, filter, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable group details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="filter">The existing membership/all-visible selection, or null to omit all and retain the membership default.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only server-ordered selection, or an empty list for a confirmed 204 response.</returns>
        /// <remarks>All-visible means visible to this caller, not every tenant group. No paging, total, own privileges or hidden administrator state is inferred. Existing GetAllGroups and its mutable model remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The filter is unsupported.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata is malformed or has repeated group identities.</exception>
        public async Task<IReadOnlyList<GroupDetails>> GetGroupsDetailsAsync(string userId, GroupsFilter? filter = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (filter.HasValue && filter != GroupsFilter.MembershipsOfCurrentUser && filter != GroupsFilter.AllVisibleGroupsForCurrentUser) throw new ArgumentException("A supported group selection is required.", nameof(filter));
            var request = CreateRestRequest(ApiVersionPrefix + "groups", Method.Get); if (filter.HasValue) request.AddQueryParameter("all", filter == GroupsFilter.AllVisibleGroupsForCurrentUser ? "true" : "false");
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (response.StatusCode == HttpStatusCode.NoContent) return Array.AsReadOnly(new GroupDetails[0]);
            return GroupDetailsParser.Parse<IReadOnlyList<GroupDetails>>(response.Content, root =>
            {
                if (!(root["groups"] is JArray entries)) throw new InvalidDataException("A group array is required."); var result = new List<GroupDetails>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries) { if (!(entry is JObject data)) throw new InvalidDataException("A group must be an object."); var detail = GroupDetailsParser.Record(data); if (!ids.Add(GroupDetailsParser.Identity(detail.Id))) throw new InvalidDataException("Repeated group identity."); result.Add(detail); }
                return result.AsReadOnly();
            });
        }
    }
}
