using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Groups
{
    /// <summary>Specifies the creation of a public or private group.</summary>
    /// <remarks>Omitted Type retains the public server default. Public creation requires tenant administration; private creation requires an internal user or tenant administrator. Private groups require a management administrator. Admin status does not itself grant visibility of shared documents.</remarks>
    public sealed class GroupCreateRequest
    {
        /// <summary>Gets or sets the required display name.</summary>
        /// <remarks>Public names must be unique within the tenant, ignoring case; private names need not be unique.</remarks>
        [JsonProperty("name")] public string Name { get; set; }
        /// <summary>Gets or sets the optional provider display color code.</summary>
        [JsonProperty("color")] public int? Color { get; set; }
        /// <summary>Gets or sets public or private, or null for the public server default.</summary>
        [JsonProperty("type")] public string Type { get; set; }
        /// <summary>Gets or sets the optional initial group members.</summary>
        [JsonProperty("users")] public List<string> Users { get; set; }
        /// <summary>Gets or sets private-group administrators and their privileges.</summary>
        [JsonProperty("admins")] public List<GroupAdministrator> Admins { get; set; }
    }

    /// <summary>Specifies selected group fields to update without replacing omitted fields.</summary>
    /// <remarks>Users replaces the entire membership set; an empty list clears members and null preserves them. Administrator changes use dedicated commands. Public updates require tenant administration; private name/color updates require group-management privilege.</remarks>
    public sealed class GroupUpdateRequest
    {
        /// <summary>Gets or sets the new name, or null to preserve it.</summary>
        [JsonProperty("name")] public string Name { get; set; }
        /// <summary>Gets or sets the new color code, or null to preserve it.</summary>
        [JsonProperty("color")] public int? Color { get; set; }
        /// <summary>Gets or sets the selected public or private type, or null to preserve it.</summary>
        /// <remarks>The server determines whether the selected state transition is permitted.</remarks>
        [JsonProperty("type")] public string Type { get; set; }
        /// <summary>Gets or sets the complete replacement membership set, or null to preserve it.</summary>
        [JsonProperty("users")] public List<string> Users { get; set; }
    }

    /// <summary>Specifies a private-group administrator and the selected privilege.</summary>
    public sealed class GroupAdministrator
    {
        /// <summary>Gets or sets the administrator's user identifier.</summary>
        [JsonProperty("user-id")] public string UserId { get; set; }
        /// <summary>Gets or sets group-management or group-membership.</summary>
        [JsonProperty("privilege")] public string Privilege { get; set; }
    }

    /// <summary>Contains the confirmed group creation identity and exact failed member selections.</summary>
    /// <remarks>A partial member failure does not roll back creation or other completed group changes.</remarks>
    public sealed class GroupCommandResult
    {
        internal GroupCommandResult(string createdGroupId, IEnumerable<string> failedUsers)
        { CreatedGroupId = createdGroupId; FailedUsers = Array.AsReadOnly(failedUsers.ToArray()); }
        /// <summary>Gets the confirmed new group identifier for creation, or null for other commands.</summary>
        public string CreatedGroupId { get; }
        /// <summary>Gets the exact requested users that the server could not apply.</summary>
        public IReadOnlyList<string> FailedUsers { get; }
        /// <summary>Gets whether all selected users were applied successfully.</summary>
        public bool AllSucceeded => FailedUsers.Count == 0;
    }
}
