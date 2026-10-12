using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Trash
{
    /// <summary>Contains a validated page of own-trash search results.</summary>
    public sealed class TrashSearchResults
    {
        private readonly JObject facets;
        internal TrashSearchResults(long hits, List<TrashSearchDocument> documents, JObject facets)
        { Hits = hits; Documents = documents.AsReadOnly(); this.facets = facets; }
        /// <summary>Gets the server's total number of matching documents.</summary>
        public long Hits { get; }
        /// <summary>Gets a read-only page in the server's returned order.</summary>
        /// <remarks>The page can be empty for zero hits, an out-of-bounds offset or rows=0; this does not replace the total hit count.</remarks>
        public IReadOnlyList<TrashSearchDocument> Documents { get; }
        /// <summary>Gets a defensive snapshot of server facet data, or null when omitted.</summary>
        /// <remarks>Preserves term/range structure, qualifiers, counts and numeric or textual boundaries without inferring missing values or equating facet counts to document hits.</remarks>
        public JObject Facets => (JObject)facets?.DeepClone();
    }

    /// <summary>Contains the metadata returned for a trash search hit.</summary>
    /// <remarks>Unrequested or omitted fields remain unknown. Owner and version number are not inferred. Identity may be unknown only when explicitly excluded by the request.</remarks>
    public sealed class TrashSearchDocument
    {
        /// <summary>Gets or sets the document identifier when returned.</summary>
        [JsonProperty("id")]
        public string Id { get; set; }
        /// <summary>Gets or sets the latest filename when returned.</summary>
        [JsonProperty("filename")]
        public string Filename { get; set; }
        /// <summary>Gets or sets the latest content size in bytes when returned.</summary>
        [JsonProperty("size")]
        public long? Size { get; set; }
        /// <summary>Gets or sets the media type when returned.</summary>
        [JsonProperty("mimetype")]
        public string MimeType { get; set; }
        /// <summary>Gets or sets the original upload timestamp and offset when returned.</summary>
        [JsonProperty("upload-date")]
        public DateTimeOffset? UploadDate { get; set; }
        /// <summary>Gets or sets the latest version timestamp and offset when returned.</summary>
        [JsonProperty("version-date")]
        public DateTimeOffset? VersionDate { get; set; }
        /// <summary>Gets or sets the deletion timestamp and offset when returned.</summary>
        [JsonProperty("deletion-date")]
        public DateTimeOffset? DeletionDate { get; set; }
        /// <summary>Gets or sets tags, or null when omitted.</summary>
        [JsonProperty("tags")]
        public List<string> Tags { get; set; }
        /// <summary>Gets or sets the fulltext relevance score when returned.</summary>
        [JsonProperty("score")]
        public double? Score { get; set; }
    }
}
