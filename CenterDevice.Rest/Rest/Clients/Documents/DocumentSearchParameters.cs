using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Specifies complete additive document-search parameters without inserting defaults.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original query selection, or null to omit it.</summary>
        public DocumentTextSearchParameters Query { get => SearchJsonModel.Object(this, "query", data => new DocumentTextSearchParameters(data)); set => SearchJsonModel.SetToken(this, "query", value); }
        /// <summary>Gets or sets the original filter selection, or null to omit it.</summary>
        public DocumentSearchFilterParameters Filter { get => SearchJsonModel.Object(this, "filter", data => new DocumentSearchFilterParameters(data)); set => SearchJsonModel.SetToken(this, "filter", value); }
        /// <summary>Gets or sets the original named selection, or null to omit it.</summary>
        public IReadOnlyList<DocumentNamedSearchParameters> Named { get => SearchJsonModel.Objects(this, "named", data => new DocumentNamedSearchParameters(data)); set => SearchJsonModel.SetObjects(this, "named", value); }
        /// <summary>Gets or sets the original fields selection, or null to omit it.</summary>
        public DocumentSearchFieldSelection Fields { get => SearchJsonModel.Object(this, "fields", data => new DocumentSearchFieldSelection(data)); set => SearchJsonModel.SetToken(this, "fields", value); }
        /// <summary>Gets or sets the original offset selection, or null to omit it.</summary>
        public long? Offset { get => this["offset"] == null || this["offset"].Type == JTokenType.Null ? null : this.Value<long?>("offset"); set => SearchJsonModel.Set(this, "offset", value); }
        /// <summary>Gets or sets the original rows selection, or null to omit it.</summary>
        public long? Rows { get => this["rows"] == null || this["rows"].Type == JTokenType.Null ? null : this.Value<long?>("rows"); set => SearchJsonModel.Set(this, "rows", value); }
        /// <summary>Gets or sets the original sort selection, or null to omit it.</summary>
        public DocumentSearchSortParameters Sort { get => SearchJsonModel.Object(this, "sort", data => new DocumentSearchSortParameters(data)); set => SearchJsonModel.SetToken(this, "sort", value); }
        /// <summary>Gets or sets the original facet selection, or null to omit it.</summary>
        public IReadOnlyList<DocumentSearchFacetParameters> Facets { get => SearchJsonModel.Objects(this, "facet", data => new DocumentSearchFacetParameters(data)); set => SearchJsonModel.SetObjects(this, "facet", value); }
        /// <summary>Gets or sets the original deep-folder-facets selection, or null to omit it.</summary>
        public bool? DeepFolderFacets { get => this["deep-folder-facets"] == null || this["deep-folder-facets"].Type == JTokenType.Null ? null : this.Value<bool?>("deep-folder-facets"); set => SearchJsonModel.Set(this, "deep-folder-facets", value); }
    }
    /// <summary>Specifies original fulltext text and optional scalar or array scope.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentTextSearchParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentTextSearchParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentTextSearchParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original text selection, or null to omit it.</summary>
        public string Text { get => SearchJsonModel.Text(this, "text"); set => SearchJsonModel.Set(this, "text", value); }
        /// <summary>Gets or sets the original scope selection, or null to omit it.</summary>
        public JToken Scope { get => this["scope"]?.DeepClone(); set => SearchJsonModel.SetToken(this, "scope", value); }
    }
    /// <summary>Specifies original exact document filters with manufacturer combination semantics.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchFilterParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchFilterParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchFilterParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original ids selection, or null to omit it.</summary>
        public IReadOnlyList<string> Ids { get => SearchJsonModel.Strings(this, "ids"); set => SearchJsonModel.SetStrings(this, "ids", value); }
        /// <summary>Gets or sets the original filenames selection, or null to omit it.</summary>
        public IReadOnlyList<string> Filenames { get => SearchJsonModel.Strings(this, "filenames"); set => SearchJsonModel.SetStrings(this, "filenames", value); }
        /// <summary>Gets or sets the original extensions selection, or null to omit it.</summary>
        public IReadOnlyList<string> Extensions { get => SearchJsonModel.Strings(this, "extensions"); set => SearchJsonModel.SetStrings(this, "extensions", value); }
        /// <summary>Gets or sets the original mimetypes selection, or null to omit it.</summary>
        public IReadOnlyList<string> MimeTypes { get => SearchJsonModel.Strings(this, "mimetypes"); set => SearchJsonModel.SetStrings(this, "mimetypes", value); }
        /// <summary>Gets or sets the original titles selection, or null to omit it.</summary>
        public IReadOnlyList<string> Titles { get => SearchJsonModel.Strings(this, "titles"); set => SearchJsonModel.SetStrings(this, "titles", value); }
        /// <summary>Gets or sets the original authors selection, or null to omit it.</summary>
        public IReadOnlyList<string> Authors { get => SearchJsonModel.Strings(this, "authors"); set => SearchJsonModel.SetStrings(this, "authors", value); }
        /// <summary>Gets or sets the original owners selection, or null to omit it.</summary>
        public IReadOnlyList<string> Owners { get => SearchJsonModel.Strings(this, "owners"); set => SearchJsonModel.SetStrings(this, "owners", value); }
        /// <summary>Gets or sets the original tags selection, or null to omit it.</summary>
        public IReadOnlyList<string> Tags { get => SearchJsonModel.Strings(this, "tags"); set => SearchJsonModel.SetStrings(this, "tags", value); }
        /// <summary>Gets or sets the original groups selection, or null to omit it.</summary>
        public IReadOnlyList<string> Groups { get => SearchJsonModel.Strings(this, "groups"); set => SearchJsonModel.SetStrings(this, "groups", value); }
        /// <summary>Gets or sets the original collections selection, or null to omit it.</summary>
        public IReadOnlyList<string> Collections { get => SearchJsonModel.Strings(this, "collections"); set => SearchJsonModel.SetStrings(this, "collections", value); }
        /// <summary>Gets or sets the original hashes selection, or null to omit it.</summary>
        public IReadOnlyList<string> Hashes { get => SearchJsonModel.Strings(this, "hashes"); set => SearchJsonModel.SetStrings(this, "hashes", value); }
        /// <summary>Gets or sets the original locked-by selection, or null to omit it.</summary>
        public IReadOnlyList<string> LockedBy { get => SearchJsonModel.Strings(this, "locked-by"); set => SearchJsonModel.SetStrings(this, "locked-by", value); }
        /// <summary>Gets or sets the original locked selection, or null to omit it.</summary>
        public bool? Locked { get => this["locked"] == null || this["locked"].Type == JTokenType.Null ? null : this.Value<bool?>("locked"); set => SearchJsonModel.Set(this, "locked", value); }
        /// <summary>Gets or sets the original folders selection, or null to omit it.</summary>
        public DocumentSearchFolderParameters Folders { get => SearchJsonModel.Object(this, "folders", data => new DocumentSearchFolderParameters(data)); set => SearchJsonModel.SetToken(this, "folders", value); }
        /// <summary>Gets or sets the original size selection, or null to omit it.</summary>
        public DocumentSearchSizeRange Size { get => SearchJsonModel.Object(this, "size", data => new DocumentSearchSizeRange(data)); set => SearchJsonModel.SetToken(this, "size", value); }
        /// <summary>Gets or sets the original upload-date selection, or null to omit it.</summary>
        public DocumentSearchDateRange UploadDate { get => SearchJsonModel.Object(this, "upload-date", data => new DocumentSearchDateRange(data)); set => SearchJsonModel.SetToken(this, "upload-date", value); }
        /// <summary>Gets or sets the original last-change selection, or null to omit it.</summary>
        public DocumentSearchDateRange LastChange { get => SearchJsonModel.Object(this, "last-change", data => new DocumentSearchDateRange(data)); set => SearchJsonModel.SetToken(this, "last-change", value); }
        /// <summary>Gets or sets the original document-date selection, or null to omit it.</summary>
        public DocumentSearchDateRange DocumentDate { get => SearchJsonModel.Object(this, "document-date", data => new DocumentSearchDateRange(data)); set => SearchJsonModel.SetToken(this, "document-date", value); }
        /// <summary>Gets or sets the original archived-date selection, or null to omit it.</summary>
        public DocumentSearchDateRange ArchivedDate { get => SearchJsonModel.Object(this, "archived-date", data => new DocumentSearchDateRange(data)); set => SearchJsonModel.SetToken(this, "archived-date", value); }
        /// <summary>Gets or sets the original extended-metadata selection, or null to omit it.</summary>
        public JObject ExtendedMetadata { get => SearchJsonModel.Object(this, "extended-metadata", data => data); set => SearchJsonModel.SetToken(this, "extended-metadata", value); }
    }
    /// <summary>Specifies original folder identifiers and the explicit recursive-search choice.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchFolderParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchFolderParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchFolderParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original ids selection, or null to omit it.</summary>
        public IReadOnlyList<string> Ids { get => SearchJsonModel.Strings(this, "ids"); set => SearchJsonModel.SetStrings(this, "ids", value); }
        /// <summary>Gets or sets the original deep selection, or null to omit it.</summary>
        public bool? Deep { get => this["deep"] == null || this["deep"].Type == JTokenType.Null ? null : this.Value<bool?>("deep"); set => SearchJsonModel.Set(this, "deep", value); }
    }
    /// <summary>Specifies optional original inclusive Int64 byte-size bounds.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchSizeRange : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchSizeRange() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchSizeRange(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original from selection, or null to omit it.</summary>
        public long? From { get => this["from"] == null || this["from"].Type == JTokenType.Null ? null : this.Value<long?>("from"); set => SearchJsonModel.Set(this, "from", value); }
        /// <summary>Gets or sets the original to selection, or null to omit it.</summary>
        public long? To { get => this["to"] == null || this["to"].Type == JTokenType.Null ? null : this.Value<long?>("to"); set => SearchJsonModel.Set(this, "to", value); }
    }
    /// <summary>Specifies optional original inclusive date-string bounds without timezone conversion.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchDateRange : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchDateRange() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchDateRange(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original from selection, or null to omit it.</summary>
        public string From { get => SearchJsonModel.Text(this, "from"); set => SearchJsonModel.Set(this, "from", value); }
        /// <summary>Gets or sets the original to selection, or null to omit it.</summary>
        public string To { get => SearchJsonModel.Text(this, "to"); set => SearchJsonModel.Set(this, "to", value); }
    }
    /// <summary>Specifies an original named query with copied nested parameter objects.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentNamedSearchParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentNamedSearchParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentNamedSearchParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original name selection, or null to omit it.</summary>
        public string Name { get => SearchJsonModel.Text(this, "name"); set => SearchJsonModel.Set(this, "name", value); }
        /// <summary>Gets or sets the original params selection, or null to omit it.</summary>
        public JObject Parameters { get => SearchJsonModel.Object(this, "params", data => data); set => SearchJsonModel.SetToken(this, "params", value); }
    }
    /// <summary>Specifies mutually exclusive copied included or excluded metadata field names.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchFieldSelection : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchFieldSelection() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchFieldSelection(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original includes selection, or null to omit it.</summary>
        public IReadOnlyList<string> Includes { get => SearchJsonModel.Strings(this, "includes"); set => SearchJsonModel.SetStrings(this, "includes", value); }
        /// <summary>Gets or sets the original excludes selection, or null to omit it.</summary>
        public IReadOnlyList<string> Excludes { get => SearchJsonModel.Strings(this, "excludes"); set => SearchJsonModel.SetStrings(this, "excludes", value); }
    }
    /// <summary>Specifies original sort field, order and copied advanced parameters.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchSortParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchSortParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchSortParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original field selection, or null to omit it.</summary>
        public string Field { get => SearchJsonModel.Text(this, "field"); set => SearchJsonModel.Set(this, "field", value); }
        /// <summary>Gets or sets the original order selection, or null to omit it.</summary>
        public string Order { get => SearchJsonModel.Text(this, "order"); set => SearchJsonModel.Set(this, "order", value); }
        /// <summary>Gets or sets the original params selection, or null to omit it.</summary>
        public JObject Parameters { get => SearchJsonModel.Object(this, "params", data => data); set => SearchJsonModel.SetToken(this, "params", value); }
    }
    /// <summary>Specifies original facet field, size, qualifier and copied advanced parameters.</summary>
    /// <remarks>Typed null setters omit selections. Source, object and array values are defensive copies; raw indexers retain future fields and documented negation wrappers. Known kinds are checked before transport without resolving resources, permissions or defaults.</remarks>
    public sealed class DocumentSearchFacetParameters : JObject
    {
        /// <summary>Initializes empty parameters without server defaults.</summary>
        public DocumentSearchFacetParameters() { }
        /// <summary>Initializes parameters by deeply copying complete JSON.</summary>
        /// <param name="other">The complete original object including future fields.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentSearchFacetParameters(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original field selection, or null to omit it.</summary>
        public string Field { get => SearchJsonModel.Text(this, "field"); set => SearchJsonModel.Set(this, "field", value); }
        /// <summary>Gets or sets the original size selection, or null to omit it.</summary>
        public long? Size { get => this["size"] == null || this["size"].Type == JTokenType.Null ? null : this.Value<long?>("size"); set => SearchJsonModel.Set(this, "size", value); }
        /// <summary>Gets or sets the original qualifier selection, or null to omit it.</summary>
        public string Qualifier { get => SearchJsonModel.Text(this, "qualifier"); set => SearchJsonModel.Set(this, "qualifier", value); }
        /// <summary>Gets or sets the original params selection, or null to omit it.</summary>
        public JObject Parameters { get => SearchJsonModel.Object(this, "params", data => data); set => SearchJsonModel.SetToken(this, "params", value); }
    }
    internal static class SearchJsonModel
    {
        internal static string Text(JObject data, string name) => data[name] == null || data[name].Type == JTokenType.Null ? null : data.Value<string>(name);
        internal static void Set(JObject data, string name, object value) { if (value == null) data.Remove(name); else data[name] = new JValue(value); }
        internal static void SetToken(JObject data, string name, JToken value) { if (value == null) data.Remove(name); else data[name] = value.DeepClone(); }
        internal static T Object<T>(JObject data, string name, Func<JObject, T> convert) where T : class => data[name] == null || data[name].Type == JTokenType.Null ? null : convert((JObject)data[name].DeepClone());
        internal static IReadOnlyList<string> Strings(JObject data, string name) => data[name] == null || data[name].Type == JTokenType.Null ? null : Array.AsReadOnly(((JArray)data[name]).Values<string>().ToArray());
        internal static void SetStrings(JObject data, string name, IReadOnlyList<string> value) { if (value == null) data.Remove(name); else data[name] = new JArray(value); }
        internal static IReadOnlyList<T> Objects<T>(JObject data, string name, Func<JObject, T> convert) => data[name] == null || data[name].Type == JTokenType.Null ? null : Array.AsReadOnly(((JArray)data[name]).Select(item => convert((JObject)item.DeepClone())).ToArray());
        internal static void SetObjects<T>(JObject data, string name, IReadOnlyList<T> value) where T : JObject { if (value == null) data.Remove(name); else data[name] = new JArray(value.Select(item => item?.DeepClone())); }
    }
}
