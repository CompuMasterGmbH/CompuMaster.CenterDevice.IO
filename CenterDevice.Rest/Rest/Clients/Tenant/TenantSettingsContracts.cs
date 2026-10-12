using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CenterDevice.Rest.Clients.Tenant
{
    /// <summary>Specifies complete original user and role selections for a tenant setting.</summary>
    public sealed class TenantPermissionSelection : JObject
    {
        /// <summary>Initializes an empty selection without inferred permissions.</summary>
        public TenantPermissionSelection() { }
        /// <summary>Initializes a selection by copying complete JSON.</summary>
        /// <param name="other">Original selection fields, including future metadata.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public TenantPermissionSelection(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets original user identifiers, with defensive copies.</summary>
        /// <value>A list including explicit empty selections, or null to omit it.</value>
        public IReadOnlyList<string> Users { get { return TenantSettingsJson.Strings(this["users"]); } set { TenantSettingsJson.Set(this, "users", value == null ? null : new JArray(value)); } }
        /// <summary>Gets or sets original roles, retaining future values with defensive copies.</summary>
        /// <value>A list including explicit empty selections, or null to omit it.</value>
        public IReadOnlyList<string> Roles { get { return TenantSettingsJson.Strings(this["roles"]); } set { TenantSettingsJson.Set(this, "roles", value == null ? null : new JArray(value)); } }
    }

    /// <summary>Specifies explicit complete tenant-setting changes without modifying unselected settings.</summary>
    /// <remarks>The server enforces administrator access, setting editability, role constraints and minimum owner rights. Empty user/role selections do not universally remove owner rights.</remarks>
    public sealed class TenantSettingsUpdateRequest : JObject
    {
        /// <summary>Initializes an empty update without defaults.</summary>
        public TenantSettingsUpdateRequest() { }
        /// <summary>Initializes an update by copying complete JSON.</summary>
        /// <param name="other">Original update fields, including future settings.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public TenantSettingsUpdateRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the complete auditing selection as a defensive copy.</summary>
        public TenantPermissionSelection Auditing { get { return Get("auditing"); } set { TenantSettingsJson.Set(this, "auditing", value); } }
        /// <summary>Gets or sets the complete archiving selection as a defensive copy.</summary>
        public TenantPermissionSelection Archiving { get { return Get("archiving"); } set { TenantSettingsJson.Set(this, "archiving", value); } }
        /// <summary>Gets or sets the complete unarchiving selection as a defensive copy.</summary>
        public TenantPermissionSelection Unarchiving { get { return Get("unarchiving"); } set { TenantSettingsJson.Set(this, "unarchiving", value); } }
        /// <summary>Gets or sets the complete non-owner document-deletion selection as a defensive copy.</summary>
        public TenantPermissionSelection DocumentDeletion { get { return Get("document-deletion"); } set { TenantSettingsJson.Set(this, "document-deletion", value); } }
        /// <summary>Gets or sets the complete non-owner ownership-transfer selection as a defensive copy.</summary>
        public TenantPermissionSelection TransferOwnership { get { return Get("transfer-ownership"); } set { TenantSettingsJson.Set(this, "transfer-ownership", value); } }
        /// <summary>Gets or sets the complete non-owner collection-sharing selection as a defensive copy.</summary>
        public TenantPermissionSelection CollectionSharing { get { return Get("collection-sharing"); } set { TenantSettingsJson.Set(this, "collection-sharing", value); } }
        /// <summary>Gets or sets the complete multi-factor authentication requirement as a defensive copy.</summary>
        /// <remarks>Selected users without multi-factor authentication lose tenant access until they enable it.</remarks>
        public TenantPermissionSelection EnforceMfa { get { return Get("enforce-mfa"); } set { TenantSettingsJson.Set(this, "enforce-mfa", value); } }
        private TenantPermissionSelection Get(string name) => this[name] == null || this[name].Type == JTokenType.Null ? null : new TenantPermissionSelection((JObject)this[name]);
        internal string SnapshotForSend()
        {
            var snapshot = (JObject)DeepClone();
            foreach (var name in new[] { "auditing", "archiving", "unarchiving", "document-deletion", "transfer-ownership", "collection-sharing", "enforce-mfa" })
            {
                if (snapshot.Property(name) == null) continue;
                if (!(snapshot[name] is JObject selection)) throw new ArgumentException("A supplied tenant setting must contain its complete selection.");
                foreach (var field in new[] { "users", "roles" })
                { if (!(selection[field] is JArray)) throw new ArgumentException("Complete users and roles arrays are required for " + name + "."); TenantSettingsJson.Strings(selection[field]); }
                if (selection.Property("enabled") != null || selection.Property("value") != null) throw new ArgumentException("Read-only tenant-setting metadata cannot be sent as a selection.");
            }
            return snapshot.ToString(Newtonsoft.Json.Formatting.None);
        }
    }

    /// <summary>Contains immutable original tenant-setting permission selections.</summary>
    public sealed class TenantPermissionDetails
    {
        internal TenantPermissionDetails(JObject data)
        { Users = TenantSettingsJson.Strings(data["users"]); Roles = TenantSettingsJson.Strings(data["roles"]); }
        /// <summary>Gets original selected user identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> Users { get; }
        /// <summary>Gets original selected roles, retaining future values and null when not provided.</summary>
        public IReadOnlyList<string> Roles { get; }
    }

    /// <summary>Contains immutable original value and editability metadata for a tenant setting.</summary>
    public sealed class TenantSettingDetails
    {
        private readonly JObject raw;
        internal TenantSettingDetails(string name, JObject data)
        {
            raw = (JObject)data.DeepClone(); Name = name;
            var enabled = data["enabled"];
            if (enabled != null && enabled.Type != JTokenType.Null)
            { if (enabled.Type != JTokenType.Boolean) throw new ArgumentException("Setting editability must be a boolean or null."); Enabled = enabled.Value<bool>(); }
            var value = data["value"];
            if (value != null && value.Type != JTokenType.Null)
            { if (!(value is JObject selection)) throw new ArgumentException("A tenant-setting value must be an object or null."); Value = new TenantPermissionDetails(selection); }
        }
        /// <summary>Gets the original setting name from the response map.</summary>
        public string Name { get; }
        /// <summary>Gets the indication whether an administrator may change this setting, or null when not provided.</summary>
        /// <remarks>This is editability metadata, not whether the feature is active or whether the caller may perform its action.</remarks>
        public bool? Enabled { get; }
        /// <summary>Gets original permission selections, or null when not provided.</summary>
        public TenantPermissionDetails Value { get; }
        /// <summary>Returns a defensive copy of complete setting metadata.</summary>
        /// <returns>Original value/editability fields, including future metadata.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    /// <summary>Contains immutable original tenant settings without inferring caller or owner permissions.</summary>
    public sealed class TenantSettingsDetails
    {
        private readonly JObject raw;
        internal TenantSettingsDetails(JObject data)
        {
            raw = (JObject)data.DeepClone(); var entries = new Dictionary<string, TenantSettingDetails>(StringComparer.Ordinal);
            foreach (var field in data.Properties())
            { if (!(field.Value is JObject entry)) throw new ArgumentException("A tenant setting must be an object."); entries.Add(field.Name, new TenantSettingDetails(field.Name, entry)); }
            Settings = new ReadOnlyDictionary<string, TenantSettingDetails>(entries);
        }
        /// <summary>Gets original setting entries, retaining future names and nullable missing metadata.</summary>
        /// <remarks>Tenant settings extend or configure default availability; empty selections do not remove the minimum owner rights described by the server contract.</remarks>
        public IReadOnlyDictionary<string, TenantSettingDetails> Settings { get; }
        /// <summary>Returns a defensive copy of complete tenant settings.</summary>
        /// <returns>The original map, including future setting names and metadata.</returns>
        public JObject GetRawSettings() => (JObject)raw.DeepClone();
    }

    internal static class TenantSettingsJson
    {
        internal static void Set(JObject root, string name, JToken value)
        { if (value == null) root.Remove(name); else root[name] = value.DeepClone(); }
        internal static IReadOnlyList<string> Strings(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (!(token is JArray values) || values.Any(value => value.Type != JTokenType.String)) throw new ArgumentException("Tenant permission selections must be arrays of strings.");
            return Array.AsReadOnly(values.Values<string>().ToArray());
        }
    }
}
