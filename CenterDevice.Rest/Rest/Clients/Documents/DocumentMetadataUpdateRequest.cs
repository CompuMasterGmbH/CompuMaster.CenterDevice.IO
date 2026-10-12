using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Specifies explicit document extended-metadata additions and removals.</summary>
    /// <remarks>Source and nested objects are copied. Typed null setters omit selections; raw future fields remain intact. The common namespace is read-only, and original date/decimal strings remain server-validated without conversion.</remarks>
    public sealed class DocumentMetadataUpdateRequest : JObject
    {
        /// <summary>Initializes an empty metadata update without defaults.</summary>
        public DocumentMetadataUpdateRequest() { }
        /// <summary>Initializes a metadata update by copying complete JSON.</summary>
        /// <param name="other">The complete original parameters including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentMetadataUpdateRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets copied dotted field paths and their explicit original values.</summary>
        /// <value>Whole namespaces/sections or individual field values, or null to omit them.</value>
        public JObject Set { get => ReadObject("set"); set => WriteObject("set", value); }
        /// <summary>Gets or sets copied dotted field paths to remove, or null to omit them.</summary>
        public IReadOnlyList<string> Unset { get => this["unset"] == null || this["unset"].Type == JTokenType.Null ? null : Array.AsReadOnly(((JArray)this["unset"]).Values<string>().ToArray()); set { if (value == null) Remove("unset"); else this["unset"] = new JArray(value); } }
        /// <summary>Gets or sets copied complete namespace data through the deprecated manufacturer metadata parameter.</summary>
        /// <remarks>New updates should use Set/Unset for explicit granular changes. This original parameter is retained without an inferred merge or precedence when combined with other selections.</remarks>
        [Obsolete("Use Set and Unset for new requests. The manufacturer deprecated the metadata parameter.")]
        public JObject Metadata { get => ReadObject("metadata"); set => WriteObject("metadata", value); }
        private JObject ReadObject(string name) => this[name] == null || this[name].Type == JTokenType.Null ? null : (JObject)this[name].DeepClone();
        private void WriteObject(string name, JObject value) { if (value == null) Remove(name); else this[name] = value.DeepClone(); }
        internal JObject SnapshotForSend()
        {
            var snapshot = (JObject)DeepClone();
            if (snapshot["set"] != null)
            {
                if (!(snapshot["set"] is JObject fields)) throw new ArgumentException("Metadata set must be an object.");
                foreach (var field in fields.Properties())
                {
                    var path = ValidatePath(field.Name);
                    if (path.Length == 1) ValidateNamespace(field.Value);
                    else if (path.Length == 2 && path[1] != "value") ValidateSection(path[1], field.Value);
                    else if (path.Length == 2 || path[1] != "dates" && path[1] != "numbers") ValidateStrings(field.Value);
                    else if (field.Value.Type != JTokenType.String) throw new ArgumentException("Date and number metadata values must be original strings.");
                }
            }
            if (snapshot["unset"] != null)
            {
                if (!(snapshot["unset"] is JArray paths)) throw new ArgumentException("Metadata unset must be a field-path array.");
                foreach (var path in paths) { if (path.Type != JTokenType.String) throw new ArgumentException("Metadata removal paths must be strings."); ValidatePath(path.Value<string>()); }
            }
            if (snapshot["metadata"] != null)
            {
                if (!(snapshot["metadata"] is JObject namespaces)) throw new ArgumentException("Complete namespace metadata must be an object.");
                foreach (var field in namespaces.Properties()) { ValidatePath(field.Name); if (field.Name.Contains(".")) throw new ArgumentException("Complete metadata keys must be namespace names."); ValidateNamespace(field.Value); }
            }
            return snapshot;
        }
        private static string[] ValidatePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("$") || value.Contains("=")) throw new ArgumentException("A dotted metadata path cannot contain dollar or equals signs or be blank.");
            var parts = value.Split('.'); if (parts.Length > 3 || parts.Any(string.IsNullOrEmpty) || parts.Length == 3 && parts[1] == "value") throw new ArgumentException("A metadata path must select a namespace, section or field.");
            if (parts[0] == "common") throw new NotSupportedException("The common document metadata namespace is read-only."); return parts;
        }
        private static void ValidateName(string name)
        { if (name.Contains(".") || name.Contains("$") || name.Contains("=")) throw new ArgumentException("Metadata field names cannot contain dot, dollar or equals signs."); }
        private static void ValidateNamespace(JToken value)
        {
            if (!(value is JObject fields)) throw new ArgumentException("A metadata namespace must be an object.");
            foreach (var field in fields.Properties()) { ValidateName(field.Name); if (field.Name == "value") ValidateStrings(field.Value); else ValidateSection(field.Name, field.Value); }
        }
        private static void ValidateSection(string name, JToken value)
        {
            if (!(value is JObject fields)) throw new ArgumentException("A metadata section must be an object.");
            foreach (var field in fields.Properties()) { ValidateName(field.Name); if (name == "dates" || name == "numbers") { if (field.Value.Type != JTokenType.String) throw new ArgumentException("Date and decimal metadata must remain strings."); } else ValidateStrings(field.Value); }
        }
        private static void ValidateStrings(JToken value)
        { if (value.Type != JTokenType.String && (!(value is JArray values) || values.Any(item => item.Type != JTokenType.String))) throw new ArgumentException("String metadata must be a string or string array."); }
    }
}
