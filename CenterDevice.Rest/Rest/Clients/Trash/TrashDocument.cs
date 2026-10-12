using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CenterDevice.Rest.Clients.Trash
{
    /// <summary>Describes a document in the authenticated user's trash.</summary>
    /// <remarks>Omitted metadata remains unknown. Trash metadata supplies no owner or version number; neither is inferred from authentication.</remarks>
    public sealed class TrashDocument
    {
        /// <summary>Gets or sets the document identifier.</summary>
        public string Id { get; set; }
        /// <summary>Gets or sets the latest filename when supplied.</summary>
        public string Filename { get; set; }
        /// <summary>Gets or sets the latest content size in bytes when supplied.</summary>
        public long? Size { get; set; }
        /// <summary>Gets or sets the media type when supplied.</summary>
        [JsonPropertyName("mimetype")]
        public string MimeType { get; set; }
        /// <summary>Gets or sets the original upload time and server offset when supplied.</summary>
        [JsonPropertyName("upload-date")]
        public DateTimeOffset? UploadDate { get; set; }
        /// <summary>Gets or sets the latest upload time and server offset when supplied.</summary>
        [JsonPropertyName("version-date")]
        public DateTimeOffset? VersionDate { get; set; }
        /// <summary>Gets or sets the deletion time and server offset when supplied.</summary>
        [JsonPropertyName("deletion-date")]
        public DateTimeOffset? DeletionDate { get; set; }
        /// <summary>Gets or sets the tags, or null when omitted.</summary>
        public List<string> Tags { get; set; }
    }
}
