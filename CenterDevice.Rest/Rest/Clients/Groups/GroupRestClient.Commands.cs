using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupRestClient
    {
        /// <summary>Adds selected users to a group.</summary>
        /// <inheritdoc cref="AddUsersAsync"/>
        public GroupCommandResult AddUsers(string userId, string groupId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
            => AddUsersAsync(userId, groupId, users, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Adds selected users using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The nonblank selected group identifier.</param>
        /// <param name="users">A nonempty sequence of selected user IDs, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The exact users that failed, without rolling back successful selections.</returns>
        /// <remarks>Public membership requires tenant administration; private membership requires group administration. Server privilege errors remain errors, and uncertain mutations are not replayed. Existing interfaces and default read behavior remain unchanged.</remarks>
        public Task<GroupCommandResult> AddUsersAsync(string userId, string groupId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteUsersAsync(userId, groupId, "add-users", users, cancellationToken);

        /// <summary>Removes selected users from a group.</summary>
        /// <inheritdoc cref="AddUsersAsync"/>
        public GroupCommandResult RemoveUsers(string userId, string groupId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveUsersAsync(userId, groupId, users, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes selected users using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="AddUsersAsync"/>
        public Task<GroupCommandResult> RemoveUsersAsync(string userId, string groupId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteUsersAsync(userId, groupId, "remove-users", users, cancellationToken);

        /// <summary>Updates selected group fields and reports partial member failures.</summary>
        /// <inheritdoc cref="UpdateGroupAsync"/>
        public GroupCommandResult UpdateGroup(string userId, string groupId, GroupUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateGroupAsync(userId, groupId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates selected group fields using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The nonblank selected group identifier.</param>
        /// <param name="request">The nonnull selected update fields, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The exact requested members that failed.</returns>
        /// <remarks>Omitted fields remain unchanged. Supplied Users replaces the complete membership set, including clearing it for an empty list. Privilege rules are documented on GroupUpdateRequest; administrators use dedicated commands. Uncertain outcomes are not replayed.</remarks>
        public Task<GroupCommandResult> UpdateGroupAsync(string userId, string groupId, GroupUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resource = GroupResource(ApiVersionPrefix, groupId);
            var snapshot = SnapshotGroupOptions(request, false);
            return ExecuteGroupCommandAsync(userId, resource, Method.Put, snapshot, false, true,
                (snapshot["users"] as JArray)?.Values<string>().ToArray(), cancellationToken);
        }

        /// <summary>Adds private-group administrators without changing existing administrator privileges.</summary>
        /// <inheritdoc cref="AddAdminsAsync"/>
        public void AddAdmins(string userId, string groupId, IEnumerable<GroupAdministrator> admins, CancellationToken cancellationToken = default(CancellationToken))
            => AddAdminsAsync(userId, groupId, admins, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Adds private-group administrators using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The nonblank selected private-group identifier.</param>
        /// <param name="admins">A nonempty sequence of administrator IDs and privileges, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing only after confirmed success.</returns>
        /// <remarks>Requires group-management privilege. Administrator status does not itself grant shared document visibility. Existing administrators' privileges use UpdateAdminPrivileges. Uncertain outcomes are not replayed.</remarks>
        public Task AddAdminsAsync(string userId, string groupId, IEnumerable<GroupAdministrator> admins, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteAdminsAsync(userId, groupId, "add-admins", admins, cancellationToken);

        /// <summary>Updates existing private-group administrator privileges.</summary>
        /// <inheritdoc cref="AddAdminsAsync"/>
        public void UpdateAdminPrivileges(string userId, string groupId, IEnumerable<GroupAdministrator> admins, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateAdminPrivilegesAsync(userId, groupId, admins, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates existing administrator privileges using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="AddAdminsAsync"/>
        /// <remarks>Requires group-management privilege and existing group administrators. It does not add administrators or change group membership. Uncertain outcomes are not replayed.</remarks>
        public Task UpdateAdminPrivilegesAsync(string userId, string groupId, IEnumerable<GroupAdministrator> admins, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteAdminsAsync(userId, groupId, "update-admin-privileges", admins, cancellationToken);

        /// <summary>Removes private-group administrators.</summary>
        /// <inheritdoc cref="RemoveAdminsAsync"/>
        public void RemoveAdmins(string userId, string groupId, IEnumerable<string> admins, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveAdminsAsync(userId, groupId, admins, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes private-group administrators using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The nonblank selected private-group identifier.</param>
        /// <param name="admins">A nonempty sequence of administrator IDs, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing only after confirmed success.</returns>
        /// <remarks>Requires group-management privilege. Removal is non-idempotent and a private group must retain a management administrator; server state errors remain errors. No local role inference or uncertain replay occurs.</remarks>
        public Task RemoveAdminsAsync(string userId, string groupId, IEnumerable<string> admins, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resource = GroupResource(ApiVersionPrefix, groupId);
            var snapshot = SnapshotGroupIds(admins);
            return ExecuteGroupCommandAsync(userId, resource, Method.Post, new JObject { ["action"] = "remove-admins", ["params"] = new JObject { ["admins"] = snapshot } }, false, false, null, cancellationToken);
        }

        /// <summary>Removes a group and its associated group access grants.</summary>
        /// <inheritdoc cref="RemoveGroupAsync"/>
        public void RemoveGroup(string userId, string groupId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveGroupAsync(userId, groupId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes a group using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="groupId">The nonblank selected group identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing only after confirmed deletion.</returns>
        /// <remarks>Requires tenant administration for public groups or group-management privilege for private groups. Users may lose document visibility after deletion. It does not delete documents; original server failures are preserved.</remarks>
        public Task RemoveGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ExecuteGroupCommandAsync(userId, GroupResource(ApiVersionPrefix, groupId), Method.Delete, null, false, false, null, cancellationToken);
        }

        private Task<GroupCommandResult> ExecuteUsersAsync(string userId, string groupId, string action, IEnumerable<string> users, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var resource = GroupResource(ApiVersionPrefix, groupId);
            var snapshot = SnapshotGroupIds(users);
            return ExecuteGroupCommandAsync(userId, resource, Method.Post, new JObject { ["action"] = action, ["params"] = new JObject { ["users"] = snapshot } }, false, true, snapshot.Values<string>().ToArray(), token);
        }

        private Task ExecuteAdminsAsync(string userId, string groupId, string action, IEnumerable<GroupAdministrator> admins, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var resource = GroupResource(ApiVersionPrefix, groupId);
            var snapshot = SnapshotGroupAdmins(admins);
            return ExecuteGroupCommandAsync(userId, resource, Method.Post, new JObject { ["action"] = action, ["params"] = new JObject { ["admins"] = snapshot } }, false, false, null, token);
        }
    }
}
