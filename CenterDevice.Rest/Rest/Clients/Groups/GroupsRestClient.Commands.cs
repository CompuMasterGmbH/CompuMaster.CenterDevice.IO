using Newtonsoft.Json.Linq;
using RestSharp;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupsRestClient
    {
        /// <summary>Creates a group and reports exact failed initial member selections.</summary>
        /// <inheritdoc cref="CreateGroupAsync"/>
        public GroupCommandResult CreateGroup(string userId, GroupCreateRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => CreateGroupAsync(userId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates a group using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="request">The nonnull creation options, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new group ID and exact initial users that failed, if any.</returns>
        /// <remarks>Creation is non-idempotent. A partial member failure still creates the group; retain its ID for reconciliation and cleanup. Public/private access restrictions are documented on GroupCreateRequest and enforced by the server. Existing token refresh is preserved; uncertain outcomes are not replayed.</remarks>
        public Task<GroupCommandResult> CreateGroupAsync(string userId, GroupCreateRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = SnapshotGroupOptions(request, true);
            return ExecuteGroupCommandAsync(userId, ApiVersionPrefix + "groups", Method.Post, snapshot, true, true,
                (snapshot["users"] as JArray)?.Values<string>().ToArray(), cancellationToken);
        }
    }
}
