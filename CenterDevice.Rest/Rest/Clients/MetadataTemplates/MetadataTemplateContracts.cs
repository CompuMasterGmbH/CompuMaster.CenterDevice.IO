using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.MetadataTemplates
{
    /// <summary>Contains an immutable extended metadata field and optional default text.</summary>
    public sealed class MetadataTemplateField
    {
        /// <summary>Initializes a field for a current well-known user namespace.</summary>
        /// <param name="key">The exact full user.strings, user.numbers or user.dates field key.</param>
        /// <param name="defaultValue">The exact string/decimal/ISO8601 default text, including empty text, or null to omit it.</param>
        /// <remarks>Default semantics are server-validated; decimal/date text is not coerced or normalized.</remarks>
        /// <exception cref="ArgumentException">The field key does not select a supported namespace and usable field tail.</exception>
        public MetadataTemplateField(string key, string defaultValue = null) : this(key, defaultValue, true) { }
        internal MetadataTemplateField(string key, string defaultValue, bool validate)
        { if (validate) ValidateKey(key); Key = key; DefaultValue = defaultValue; }
        /// <summary>Gets the original full field key, retaining future response namespaces.</summary>
        public string Key { get; }
        /// <summary>Gets the original default text without numeric/date conversion.</summary>
        /// <value>The exact text, or null when omitted or JSON null.</value>
        public string DefaultValue { get; }
        internal static void ValidateKey(string key)
        {
            var prefixes = new[] { "user.strings.", "user.numbers.", "user.dates." };
            foreach (var prefix in prefixes) if (key != null && key.StartsWith(prefix, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(key.Substring(prefix.Length))) return;
            throw new ArgumentException("A full current user metadata field key is required.", nameof(key));
        }
    }

    /// <summary>Contains an immutable template sharing selection.</summary>
    public sealed class MetadataTemplateSharing
    {
        /// <summary>Initializes a sharing using current manufacturer right and entity names.</summary>
        /// <param name="id">The nonblank user, group or role identifier.</param>
        /// <param name="right">The viewer, editor or coowner right.</param>
        /// <param name="type">The user, group or role entity type.</param>
        /// <exception cref="ArgumentException">A selection is blank or unsupported.</exception>
        public MetadataTemplateSharing(string id, string right, string type) : this(id, right, type, true) { }
        internal MetadataTemplateSharing(string id, string right, string type, bool validate)
        {
            if (validate && (string.IsNullOrWhiteSpace(id) || right != "viewer" && right != "editor" && right != "coowner" || type != "user" && type != "group" && type != "role"))
                throw new ArgumentException("A usable sharee and current sharing right/type are required.");
            Id = id; Right = right; Type = type;
        }
        /// <summary>Gets the original sharee identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original sharing right, retaining future response values.</summary>
        public string Right { get; }
        /// <summary>Gets the original entity type, retaining future response values.</summary>
        public string Type { get; }
    }

    /// <summary>Selects the complete creation of a user metadata template.</summary>
    public sealed class MetadataTemplateCreateRequest
    {
        /// <summary>Gets or sets the required nonnull exact name.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets required complete field definitions, including an explicit empty sequence.</summary>
        public IEnumerable<MetadataTemplateField> Fields { get; set; }
        /// <summary>Gets or sets required complete sharings, including an explicit empty sequence.</summary>
        public IEnumerable<MetadataTemplateSharing> Sharings { get; set; }
    }

    /// <summary>Selects metadata-template update fields without guessing hidden sharing state.</summary>
    /// <remarks>Omitted fields are not sent and remain subject to server update validation. Supplied arrays replace complete state; single-entry patch semantics are not offered.</remarks>
    public sealed class MetadataTemplateUpdateRequest
    {
        /// <summary>Gets or sets the selected exact name, or null to omit it.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets the selected current template type.</summary>
        /// <value>The user type, or null to omit it.</value>
        public string Type { get; set; }
        /// <summary>Gets or sets the complete replacement fields, an explicit empty sequence, or null to omit them.</summary>
        public IEnumerable<MetadataTemplateField> Fields { get; set; }
        /// <summary>Gets or sets the complete replacement sharings, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>Only owner/coowner may update sharings. Never replace hidden sharings by an inferred empty array.</remarks>
        public IEnumerable<MetadataTemplateSharing> Sharings { get; set; }
    }

    /// <summary>Contains immutable original metadata-template detail.</summary>
    public sealed class MetadataTemplate
    {
        internal MetadataTemplate(string id, string name, string type, string owner, string right, string creationText, DateTimeOffset? creation,
            string modifiedText, DateTimeOffset? modified, IReadOnlyList<MetadataTemplateField> fields, IReadOnlyList<MetadataTemplateSharing> sharings)
        { Id = id; Name = name; Type = type; OwnerId = owner; SharingRight = right; CreationDateText = creationText; CreationDate = creation; LastModifiedDateText = modifiedText; LastModifiedDate = modified; Fields = fields; Sharings = sharings; }
        /// <summary>Gets the server-confirmed original template identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original template name.</summary>
        public string Name { get; }
        /// <summary>Gets the original template type, retaining future values.</summary>
        public string Type { get; }
        /// <summary>Gets the original server-returned owner identifier without substituting the authenticated user.</summary>
        public string OwnerId { get; }
        /// <summary>Gets the original current-user right, retaining future values.</summary>
        public string SharingRight { get; }
        /// <summary>Gets the original ISO8601 creation text.</summary>
        public string CreationDateText { get; }
        /// <summary>Gets the creation date only when the text has an explicit timezone offset.</summary>
        /// <value>The offset-aware date, or null for offset-free text; no workstation timezone is inferred.</value>
        public DateTimeOffset? CreationDate { get; }
        /// <summary>Gets the original ISO8601 modification text.</summary>
        public string LastModifiedDateText { get; }
        /// <summary>Gets the modification date only when supplied with an explicit timezone offset.</summary>
        /// <value>The offset-aware date, or null for offset-free text.</value>
        public DateTimeOffset? LastModifiedDate { get; }
        /// <summary>Gets immutable original field definitions.</summary>
        public IReadOnlyList<MetadataTemplateField> Fields { get; }
        /// <summary>Gets immutable original sharing details when the server exposes them.</summary>
        /// <value>The list, an explicit empty list, or null when absent/JSON null; absence does not mean unshared.</value>
        public IReadOnlyList<MetadataTemplateSharing> Sharings { get; }
    }
}
