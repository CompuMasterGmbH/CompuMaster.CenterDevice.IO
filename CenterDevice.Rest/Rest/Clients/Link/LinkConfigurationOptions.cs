using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Link
{
    /// <summary>Specifies copied complete access-control replacement choices without defaults.</summary>
    /// <remarks>Original dates and future fields remain intact. Updating a link replaces the whole access-control state; omission does not merge with current controls.</remarks>
    public sealed class LinkAccessControlOptions : JObject
    {
        /// <summary>Initializes choices without defaults.</summary>
        public LinkAccessControlOptions() { }
        /// <summary>Copies complete choices including future fields.</summary>
        /// <param name="other">The original choices to copy.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public LinkAccessControlOptions(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original ISO expiry date, or null to omit it.</summary>
        public string ExpiryDate { get => (string)this["expiry-date"]; set => LinkContractJson.Write(this, "expiry-date", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the explicit allowed download count, or null to omit it.</summary>
        public long? MaxDownloads { get => (long?)this["max-downloads"]; set => LinkContractJson.Write(this, "max-downloads", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets the original password, or null to omit it.</summary>
        public string Password { get => (string)this["password"]; set => LinkContractJson.Write(this, "password", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the explicit viewer-only choice, or null to omit it.</summary>
        public bool? ViewOnly { get => (bool?)this["view-only"]; set => LinkContractJson.Write(this, "view-only", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets the explicit upload-date visibility choice, or null to omit it.</summary>
        public bool? HideDate { get => (bool?)this["hide-date"]; set => LinkContractJson.Write(this, "hide-date", value.HasValue ? new JValue(value.Value) : null); }
    }
    /// <summary>Specifies copied upload-link creation or partial-update choices.</summary>
    /// <remarks>Collection and folder targets are exclusive. Typed null setters omit fields; raw explicit password null remains available for clearing protection. No expiry, upload/byte limit, location or notification defaults are inserted.</remarks>
    public sealed class UploadLinkOptions : JObject
    {
        /// <summary>Initializes choices without defaults.</summary>
        public UploadLinkOptions() { }
        /// <summary>Copies complete choices including future fields.</summary>
        /// <param name="other">The original choices to copy.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public UploadLinkOptions(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original display name, or null to omit it.</summary>
        public string Name { get => (string)this["name"]; set => LinkContractJson.Write(this, "name", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the explicit collection target, or null to omit it.</summary>
        public string Collection { get => (string)this["collection"]; set => LinkContractJson.Write(this, "collection", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the explicit folder target, or null to omit it.</summary>
        public string Folder { get => (string)this["folder"]; set => LinkContractJson.Write(this, "folder", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the original ordered tags, or null to omit it.</summary>
        public IReadOnlyList<string> Tags { get => this["tags"] == null ? null : Array.AsReadOnly(((JArray)this["tags"]).Values<string>().ToArray()); set => LinkContractJson.Write(this, "tags", value == null ? null : new JArray(value)); }
        /// <summary>Gets or sets the original ISO expiry date, or null to omit it.</summary>
        public string ExpiryDate { get => (string)this["expiry-date"]; set => LinkContractJson.Write(this, "expiry-date", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the positive allowed upload count, or null to omit it.</summary>
        public long? MaxDocuments { get => (long?)this["max-documents"]; set => LinkContractJson.Write(this, "max-documents", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets the positive GiB-aligned allowed byte count, or null to omit it.</summary>
        public long? MaxBytes { get => (long?)this["max-bytes"]; set => LinkContractJson.Write(this, "max-bytes", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets the original password, or null to omit it.</summary>
        public string Password { get => (string)this["password"]; set => LinkContractJson.Write(this, "password", value == null ? null : new JValue(value)); }
        /// <summary>Gets or sets the explicit creator notification choice, or null to omit it.</summary>
        public bool? EmailCreatorOnUpload { get => (bool?)this["email-creator-on-upload"]; set => LinkContractJson.Write(this, "email-creator-on-upload", value.HasValue ? new JValue(value.Value) : null); }
    }
}
