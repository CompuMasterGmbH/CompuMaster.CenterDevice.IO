using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Collections
{
    /// <summary>Specifies explicit collection custom-metadata additions and removals.</summary>
    public sealed class CollectionMetadataUpdateRequest : JObject
    {
        /// <summary>Initializes an empty metadata update without defaults.</summary>
        public CollectionMetadataUpdateRequest() { }
        /// <summary>Initializes an update by copying complete JSON.</summary>
        /// <param name="other">Original update parameters, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public CollectionMetadataUpdateRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets a defensive copy of fields whose values are strings or arrays of strings.</summary>
        /// <value>The explicit set object, including empty values, or null to omit it.</value>
        public JObject Set { get { return this["set"] == null || this["set"].Type == JTokenType.Null ? null : new JObject((JObject)this["set"]); } set { if (value == null) Remove("set"); else this["set"] = value.DeepClone(); } }
        /// <summary>Gets or sets original field names to remove with defensive copies.</summary>
        /// <value>The explicit ordered list, or null to omit it; missing server keys are silently ignored.</value>
        public IReadOnlyList<string> Unset { get { return this["unset"] == null || this["unset"].Type == JTokenType.Null ? null : Array.AsReadOnly(((JArray)this["unset"]).Values<string>().ToArray()); } set { if (value == null) Remove("unset"); else this["unset"] = new JArray(value); } }
        internal JObject SnapshotForSend()
        {
            var snapshot = (JObject)DeepClone(); var set = snapshot["set"];
            if (set != null)
            {
                if (!(set is JObject fields)) throw new ArgumentException("Collection metadata set must be an object.");
                foreach (var field in fields.Properties())
                    if (field.Value.Type != JTokenType.String && (!(field.Value is JArray values) || values.Any(value => value.Type != JTokenType.String)))
                        throw new ArgumentException("Collection custom metadata values must be strings or arrays of strings.");
            }
            var unset = snapshot["unset"];
            if (unset != null && (!(unset is JArray names) || names.Any(value => value.Type != JTokenType.String))) throw new ArgumentException("Collection metadata unset must be an array of field names.");
            return snapshot;
        }
    }

    /// <summary>Specifies explicit auditing notification choices without filling omitted defaults.</summary>
    public sealed class CollectionAuditingEmailSettings : JObject
    {
        /// <summary>Initializes empty auditing notification settings.</summary>
        public CollectionAuditingEmailSettings() { }
        /// <summary>Initializes settings by copying complete JSON.</summary>
        /// <param name="other">Original settings, including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public CollectionAuditingEmailSettings(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets explicit owner notification for opted-in users, or null to omit it.</summary>
        public bool? UserOptedIn { get { return Value<bool?>("user-opted-in"); } set { if (value.HasValue) this["user-opted-in"] = value.Value; else Remove("user-opted-in"); } }
        /// <summary>Gets or sets explicit owner notification for opted-out users, or null to omit it.</summary>
        public bool? UserOptedOut { get { return Value<bool?>("user-opted-out"); } set { if (value.HasValue) this["user-opted-out"] = value.Value; else Remove("user-opted-out"); } }
        /// <summary>Gets or sets the original pending-response period, or null to omit it.</summary>
        /// <remarks>The documented periods are monthly, weekly and none; future values remain server-authoritative.</remarks>
        public string PendingResponses { get { return Value<string>("pending-responses"); } set { if (value == null) Remove("pending-responses"); else this["pending-responses"] = value; } }
        internal JObject SnapshotForSend()
        {
            var snapshot = (JObject)DeepClone();
            foreach (var name in new[] { "user-opted-in", "user-opted-out" }) if (snapshot[name] != null && snapshot[name].Type != JTokenType.Boolean) throw new ArgumentException("Supplied auditing notification choices must be booleans.");
            if (snapshot["pending-responses"] != null && snapshot["pending-responses"].Type != JTokenType.String) throw new ArgumentException("A supplied pending-response period must be a string.");
            return snapshot;
        }
    }
}
