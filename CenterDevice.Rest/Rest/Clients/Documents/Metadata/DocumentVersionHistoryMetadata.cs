using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CenterDevice.Rest.Clients.Documents.Metadata
{
    /// <summary>Contains the selected document identity and server-visible version history.</summary>
    /// <remarks>The server may omit version details for guests or empty metadata. Null details mean the field was not returned; they do not prove that no versions exist. No ordering or complete visibility is inferred.</remarks>
    public class DocumentVersionHistoryMetadata
    {
        /// <summary>Gets or sets the selected document identifier.</summary>
        public string Id { get; set; }

        /// <summary>Gets or sets the current version number, or null when not returned.</summary>
        public long? Version { get; set; }

        /// <summary>Gets or sets the version details returned by the server, or null when omitted.</summary>
        [JsonPropertyName("version-details")]
        public List<DocumentVersionMetadata> VersionDetails { get; set; }
    }

    /// <summary>Contains version-dependent metadata for a server-visible document version.</summary>
    /// <remarks>The containing history supplies the document identity. Optional fields retain their absence; uploader identity may be the server placeholder not-visible.</remarks>
    public class DocumentVersionMetadata
    {
        /// <summary>Gets or sets the version number, or null when not returned.</summary>
        public long? Version { get; set; }

        /// <summary>Gets or sets the uploader identifier or visibility placeholder.</summary>
        public string Uploader { get; set; }

        /// <summary>Gets or sets the filename of this version.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the content size in bytes, or null when not returned.</summary>
        public long? Size { get; set; }

        /// <summary>Gets or sets the version upload timestamp, or null when not returned.</summary>
        [JsonPropertyName("version-date")]
        public DateTime? VersionDate { get; set; }

        /// <summary>Gets or sets the MIME type of this version.</summary>
        [JsonPropertyName("mimetype")]
        public string MimeType { get; set; }

        /// <summary>Gets or sets the title supplied or extracted for this version.</summary>
        public string Title { get; set; }

        /// <summary>Gets or sets the author supplied or extracted for this version.</summary>
        public string Author { get; set; }

        /// <summary>Gets or sets the page count, or null when not returned.</summary>
        public int? Pages { get; set; }
    }
}
