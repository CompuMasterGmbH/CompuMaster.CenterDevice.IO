using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Collections
{
    /// <summary>Specifies explicit collection creation options without inserting defaults.</summary>
    /// <remarks>Raw indexers preserve future fields. Typed null setters omit selections; complete inputs are copied before authorization. The server controls uniqueness, visibility, auditing, archiving and partial document/sharing failures.</remarks>
    public sealed class CollectionCreationRequest : JObject
    {
        /// <summary>Initializes an empty collection creation request.</summary>
        public CollectionCreationRequest() { }
        /// <summary>Initializes a collection creation request by copying complete JSON.</summary>
        /// <param name="other">The original request including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public CollectionCreationRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original required collection name.</summary>
        public string Name { get => CollectionConfigurationParser.Text(this, "name"); set => CollectionConfigurationParser.Set(this, "name", value); }
        /// <summary>Gets or sets the explicit unique-name choice, or null to omit it.</summary>
        public bool? Unique { get => CollectionConfigurationParser.Flag(this, "unique"); set => CollectionConfigurationParser.Set(this, "unique", value); }
        /// <summary>Gets or sets the explicit public choice, or null to omit it.</summary>
        public bool? Public { get => CollectionConfigurationParser.Flag(this, "public"); set => CollectionConfigurationParser.Set(this, "public", value); }
        /// <summary>Gets or sets the explicit inherited-sharing disclosure choice, or null to omit it.</summary>
        public bool? InheritedSharings { get => CollectionConfigurationParser.Flag(this, "inherited-sharings"); set => CollectionConfigurationParser.Set(this, "inherited-sharings", value); }
        /// <summary>Gets or sets the explicit full-path disclosure choice, or null to omit it.</summary>
        public bool? FullPath { get => CollectionConfigurationParser.Flag(this, "full-path"); set => CollectionConfigurationParser.Set(this, "full-path", value); }
        /// <summary>Gets or sets the explicit auditing choice, or null to omit it.</summary>
        /// <remarks>Auditing is irreversible; permissions and public/intelligent restrictions remain server-authoritative.</remarks>
        public bool? Auditing { get => CollectionConfigurationParser.Flag(this, "auditing"); set => CollectionConfigurationParser.Set(this, "auditing", value); }
        /// <summary>Gets or sets the explicit archived choice, or null to omit it.</summary>
        /// <remarks>Archiving changes document lifecycle; establish unarchiving and cleanup capability before creating test archives.</remarks>
        public bool? Archived { get => CollectionConfigurationParser.Flag(this, "archived"); set => CollectionConfigurationParser.Set(this, "archived", value); }
        /// <summary>Gets or sets the explicit duplicate-name check choice, or null to omit it.</summary>
        public bool? CheckForDuplicate { get => CollectionConfigurationParser.Flag(this, "check-for-duplicate"); set => CollectionConfigurationParser.Set(this, "check-for-duplicate", value); }
        /// <summary>Gets or sets copied initial document identifiers, or null to omit them.</summary>
        public IReadOnlyList<string> Documents { get => CollectionConfigurationParser.List(this, "documents"); set => CollectionConfigurationParser.SetList(this, "documents", value); }
        /// <summary>Gets or sets copied user sharing identifiers, or null to omit them.</summary>
        public IReadOnlyList<string> Users { get => CollectionConfigurationParser.List(this, "users"); set => CollectionConfigurationParser.SetList(this, "users", value); }
        /// <summary>Gets or sets copied group sharing identifiers, or null to omit them.</summary>
        public IReadOnlyList<string> Groups { get => CollectionConfigurationParser.List(this, "groups"); set => CollectionConfigurationParser.SetList(this, "groups", value); }
        /// <summary>Gets or sets a copied intelligent-collection filter object, or null to omit it.</summary>
        /// <remarks>Complete original nested named queries, folder filters, dates and future fields are retained. When supplied, documents and public must be omitted. The server stores these filters without checking referenced resources.</remarks>
        public JObject FilterParameters { get => CollectionConfigurationParser.Object(this, "filter-params"); set => CollectionConfigurationParser.SetObject(this, "filter-params", value); }
        /// <summary>Gets or sets copied custom metadata whose values are strings or arrays of strings.</summary>
        public JObject ExtendedMetadata { get => CollectionConfigurationParser.Object(this, "extended-metadata"); set => CollectionConfigurationParser.SetObject(this, "extended-metadata", value); }
        internal JObject SnapshotForSend() => CollectionConfigurationParser.Snapshot(this, true);
    }

    /// <summary>Specifies explicit collection property changes while preserving omitted settings.</summary>
    public sealed class CollectionUpdateRequest : JObject
    {
        /// <summary>Initializes an empty collection update without defaults.</summary>
        public CollectionUpdateRequest() { }
        /// <summary>Initializes a collection update by copying complete JSON.</summary>
        /// <param name="other">The original request including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public CollectionUpdateRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original new name, or null to omit it.</summary>
        public string Name { get => CollectionConfigurationParser.Text(this, "name"); set => CollectionConfigurationParser.Set(this, "name", value); }
        /// <summary>Gets or sets the explicit unique-name choice, or null to omit it.</summary>
        public bool? Unique { get => CollectionConfigurationParser.Flag(this, "unique"); set => CollectionConfigurationParser.Set(this, "unique", value); }
        /// <summary>Gets or sets the explicit public choice, or null to omit it.</summary>
        public bool? Public { get => CollectionConfigurationParser.Flag(this, "public"); set => CollectionConfigurationParser.Set(this, "public", value); }
        /// <summary>Gets or sets the explicit inherited-sharing disclosure choice, or null to omit it.</summary>
        public bool? InheritedSharings { get => CollectionConfigurationParser.Flag(this, "inherited-sharings"); set => CollectionConfigurationParser.Set(this, "inherited-sharings", value); }
        /// <summary>Gets or sets the explicit full-path disclosure choice, or null to omit it.</summary>
        public bool? FullPath { get => CollectionConfigurationParser.Flag(this, "full-path"); set => CollectionConfigurationParser.Set(this, "full-path", value); }
        /// <summary>Gets or sets a copied complete intelligent-collection filter object, or null to omit it.</summary>
        /// <remarks>Original nested values remain intact without computing dates or verifying referenced resources. The server enforces normal/intelligent collection restrictions.</remarks>
        public JObject FilterParameters { get => CollectionConfigurationParser.Object(this, "filter-params"); set => CollectionConfigurationParser.SetObject(this, "filter-params", value); }
        internal JObject SnapshotForSend() => CollectionConfigurationParser.Snapshot(this, false);
    }

    internal static class CollectionConfigurationParser
    {
        internal static string Text(JObject data, string name) => data[name]?.Type == JTokenType.Null ? null : data.Value<string>(name);
        internal static bool? Flag(JObject data, string name) => data[name]?.Type == JTokenType.Null ? null : data.Value<bool?>(name);
        internal static void Set(JObject data, string name, object value) { if (value == null) data.Remove(name); else data[name] = new JValue(value); }
        internal static JObject Object(JObject data, string name) => data[name] == null || data[name].Type == JTokenType.Null ? null : (JObject)data[name].DeepClone();
        internal static void SetObject(JObject data, string name, JObject value) { if (value == null) data.Remove(name); else data[name] = value.DeepClone(); }
        internal static IReadOnlyList<string> List(JObject data, string name) => data[name] == null || data[name].Type == JTokenType.Null ? null : Array.AsReadOnly(((JArray)data[name]).Values<string>().ToArray());
        internal static void SetList(JObject data, string name, IReadOnlyList<string> value) { if (value == null) data.Remove(name); else data[name] = new JArray(value); }
        internal static JObject Snapshot(JObject source, bool create)
        {
            var data = (JObject)source.DeepClone();
            if (create && data["name"] == null) throw new ArgumentException("The collection creation name is required.");
            if (data["name"] != null && data["name"].Type != JTokenType.String) throw new ArgumentException("The collection name must be a string.");
            foreach (var field in new[] { "unique", "public", "inherited-sharings", "full-path", "auditing", "archived", "check-for-duplicate" })
                if (data[field] != null && data[field].Type != JTokenType.Boolean) throw new ArgumentException("The " + field + " choice must be a boolean.");
            foreach (var field in new[] { "documents", "users", "groups" })
                if (data[field] != null && (!(data[field] is JArray values) || values.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())))) throw new ArgumentException("The " + field + " selection must be an array of nonblank identifiers.");
            if (data["filter-params"] != null)
            {
                if (!(data["filter-params"] is JObject)) throw new ArgumentException("The intelligent filter parameters must be an object.");
                if (create && (data["documents"] != null || data["public"] != null)) throw new ArgumentException("Intelligent collection creation must omit documents and public.");
            }
            if (data["extended-metadata"] != null)
            {
                if (!(data["extended-metadata"] is JObject metadata)) throw new ArgumentException("Extended metadata must be an object.");
                foreach (var field in metadata.Properties()) if (field.Value.Type != JTokenType.String && (!(field.Value is JArray values) || values.Any(value => value.Type != JTokenType.String))) throw new ArgumentException("Extended metadata values must be strings or arrays of strings.");
            }
            return data;
        }
    }
}
