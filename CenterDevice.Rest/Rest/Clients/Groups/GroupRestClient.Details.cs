using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupRestClient
    {
        /// <summary>Retrieves immutable selected group detail including exposed administrator metadata.</summary>
        /// <inheritdoc cref="GetGroupDetailsAsync"/>
        public GroupDetails GetGroupDetails(string userId, string groupId, CancellationToken cancellationToken = default(CancellationToken))
            => GetGroupDetailsAsync(userId, groupId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable selected group detail using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The exact nonblank selected group identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original nullable type/color/members and exposed administrators, without inferred ownership or permissions.</returns>
        /// <remarks>The server enforces public/private visibility. Hidden administrators remain unknown rather than an empty list. Existing GetGroup and its mutable legacy model remain unchanged, including their defaults.</remarks>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful detail is malformed or identifies another group.</exception>
        public async Task<GroupDetails> GetGroupDetailsAsync(string userId, string groupId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); GroupDetailsParser.RequireId(groupId);
            var request = CreateRestRequest(ApiVersionPrefix + "group/" + System.Uri.EscapeDataString(groupId), Method.Get);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return GroupDetailsParser.Parse(response.Content, root => { var detail = GroupDetailsParser.Record(root); if (GroupDetailsParser.Identity(detail.Id) != GroupDetailsParser.Identity(groupId)) throw new InvalidDataException("The group identity differs from the selection."); return detail; });
        }
    }
}
