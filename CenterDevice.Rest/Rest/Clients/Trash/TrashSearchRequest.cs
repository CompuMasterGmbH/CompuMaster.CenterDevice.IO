using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Trash
{
    /// <summary>Specifies a search of the authenticated user's own trash.</summary>
    /// <remarks>Omitted options retain server defaults. Named queries, negation and general-document-only filters are not supported by this resource. The client snapshots all options before authorization.</remarks>
    public sealed class TrashSearchRequest
    {
        /// <summary>Gets or sets the filename and extracted-content query, or null when omitted.</summary>
        [JsonProperty("query")]
        public TrashSearchQuery Query { get; set; }
        /// <summary>Gets or sets the supported metadata filters, or null when omitted.</summary>
        [JsonProperty("filter")]
        public TrashSearchFilter Filter { get; set; }
        /// <summary>Gets or sets the returned metadata selection, or null for server defaults.</summary>
        [JsonProperty("fields")]
        public TrashSearchFields Fields { get; set; }
        /// <summary>Gets or sets the nonnegative zero-based result offset, or null for the server default.</summary>
        [JsonProperty("offset")]
        public int? Offset { get; set; }
        /// <summary>Gets or sets the nonnegative page size, or null for the server default.</summary>
        /// <remarks>Zero requests no document entries while retaining hits and requested facets. The server can cap larger pages.</remarks>
        [JsonProperty("rows")]
        public int? Rows { get; set; }
        /// <summary>Gets or sets the result ordering, or null for query-dependent server defaults.</summary>
        [JsonProperty("sort")]
        public TrashSearchSort Sort { get; set; }
        /// <summary>Gets or sets requested facets, or null when omitted.</summary>
        [JsonProperty("facet")]
        public List<TrashSearchFacet> Facets { get; set; }
    }

    /// <summary>Specifies a filename and extracted-content query without negation.</summary>
    public sealed class TrashSearchQuery
    {
        /// <summary>Gets or sets the query text, preserving explicit empty text.</summary>
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    /// <summary>Specifies the metadata filters supported by trash search.</summary>
    /// <remarks>Null options are omitted; explicit empty lists remain distinct. No ownership, collection, folder or named-query filters are inferred.</remarks>
    public sealed class TrashSearchFilter
    {
        /// <summary>Gets or sets document identifiers to match.</summary>
        [JsonProperty("ids")]
        public List<string> Ids { get; set; }
        /// <summary>Gets or sets filenames to match.</summary>
        [JsonProperty("filenames")]
        public List<string> Filenames { get; set; }
        /// <summary>Gets or sets file extensions to match.</summary>
        [JsonProperty("extensions")]
        public List<string> Extensions { get; set; }
        /// <summary>Gets or sets MIME types to match.</summary>
        [JsonProperty("mimetypes")]
        public List<string> MimeTypes { get; set; }
        /// <summary>Gets or sets tags to match.</summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; }
        /// <summary>Gets or sets inclusive content-size bounds in bytes.</summary>
        [JsonProperty("size")]
        public TrashSearchSizeRange Size { get; set; }
        /// <summary>Gets or sets inclusive original upload-time bounds.</summary>
        [JsonProperty("upload-date")]
        public TrashSearchDateRange UploadDate { get; set; }
        /// <summary>Gets or sets inclusive last-change-time bounds.</summary>
        [JsonProperty("last-change")]
        public TrashSearchDateRange LastChange { get; set; }
        /// <summary>Gets or sets inclusive deletion-time bounds.</summary>
        [JsonProperty("deletion-date")]
        public TrashSearchDateRange DeletionDate { get; set; }
    }

    /// <summary>Specifies inclusive nonnegative content-size bounds.</summary>
    public sealed class TrashSearchSizeRange
    {
        /// <summary>Gets or sets the inclusive lower byte count, or null for no lower bound.</summary>
        [JsonProperty("from")]
        public long? From { get; set; }
        /// <summary>Gets or sets the inclusive upper byte count, or null for no upper bound.</summary>
        [JsonProperty("to")]
        public long? To { get; set; }
    }

    /// <summary>Specifies inclusive timestamps with their supplied offsets.</summary>
    public sealed class TrashSearchDateRange
    {
        /// <summary>Gets or sets the inclusive lower timestamp, or null for no lower bound.</summary>
        [JsonProperty("from")]
        public DateTimeOffset? From { get; set; }
        /// <summary>Gets or sets the inclusive upper timestamp, or null for no upper bound.</summary>
        [JsonProperty("to")]
        public DateTimeOffset? To { get; set; }
    }

    /// <summary>Selects returned trash metadata fields.</summary>
    /// <remarks>Only one list may be supplied. Supported names are id, upload-date, filename, size, mimetype, version-date, tags, score and deletion-date.</remarks>
    public sealed class TrashSearchFields
    {
        /// <summary>Gets or sets included fields, or null when not specified.</summary>
        [JsonProperty("includes")]
        public List<string> Includes { get; set; }
        /// <summary>Gets or sets excluded fields, or null when not specified.</summary>
        [JsonProperty("excludes")]
        public List<string> Excludes { get; set; }
    }

    /// <summary>Specifies a supported trash result ordering.</summary>
    public sealed class TrashSearchSort
    {
        /// <summary>Gets or sets score, filename, upload-date, version-date, size or deletion-date.</summary>
        [JsonProperty("field")]
        public string Field { get; set; }
        /// <summary>Gets or sets asc or desc, or null for the server default.</summary>
        [JsonProperty("order")]
        public string Order { get; set; }
    }

    /// <summary>Requests a supported trash-search facet.</summary>
    public sealed class TrashSearchFacet
    {
        /// <summary>Gets or sets extension, filename, mimetype, size, tags, upload-date, version-date or deletion-date.</summary>
        [JsonProperty("field")]
        public string Field { get; set; }
        /// <summary>Gets or sets a positive limit for returned terms, or null for the server default.</summary>
        /// <remarks>The server ignores this limit for range facets.</remarks>
        [JsonProperty("size")]
        public int? Size { get; set; }
        /// <summary>Gets or sets the response-name qualifier, required when requesting the same field more than once.</summary>
        [JsonProperty("qualifier")]
        public string Qualifier { get; set; }
    }
}
