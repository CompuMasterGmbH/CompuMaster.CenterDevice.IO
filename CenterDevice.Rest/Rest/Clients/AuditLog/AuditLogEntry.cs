using Newtonsoft.Json.Linq;
using System;

namespace CenterDevice.Rest.Clients.AuditLog
{
    /// <summary>Contains a collection audit-log event without inferring hidden metadata.</summary>
    public sealed class AuditLogEntry
    {
        /// <summary>Identifies an audited document download event.</summary>
        public const string DocumentDownloaded = "document-downloaded";
        /// <summary>Identifies an audited document view event.</summary>
        public const string DocumentViewed = "document-viewed";
        private readonly JObject details;
        internal AuditLogEntry(string id, string type, string timestampText, DateTimeOffset? timestamp, string creator, JObject details, AuditDocumentAccessDetails access)
        { Id = id; Type = type; TimestampText = timestampText; Timestamp = timestamp; Creator = creator; this.details = details; DocumentAccess = access; }
        /// <summary>Gets the server-confirmed event identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the event type name, retaining unknown future names.</summary>
        public string Type { get; }
        /// <summary>Gets the creation timestamp when supplied with an explicit offset.</summary>
        /// <remarks>Offset-free timestamps retain their text without an inferred local timezone. Scroll order is logical event order; consumers must not reorder cursor pages by timestamp.</remarks>
        public DateTimeOffset? Timestamp { get; }
        /// <summary>Gets the exact server timestamp text when supplied.</summary>
        public string TimestampText { get; }
        /// <summary>Gets the event creator's user identifier when supplied.</summary>
        /// <remarks>The authentication context is not used as a substitute for an omitted creator.</remarks>
        public string Creator { get; }
        /// <summary>Gets a defensive snapshot of all event details, or null when omitted.</summary>
        public JObject Details => (JObject)details?.DeepClone();
        /// <summary>Gets typed document-access details for view/download events, or null for other event types.</summary>
        public AuditDocumentAccessDetails DocumentAccess { get; }
    }

    /// <summary>Contains the details of an audited document view or download.</summary>
    /// <remarks>These event types require auditing and are visible only to the collection owner. Omitted values stay unknown.</remarks>
    public sealed class AuditDocumentAccessDetails
    {
        internal AuditDocumentAccessDetails(string id, long? version, string filename)
        { DocumentId = id; Version = version; Filename = filename; }
        /// <summary>Gets the document identifier when supplied.</summary>
        public string DocumentId { get; }
        /// <summary>Gets the accessed version when supplied, without substituting a current version.</summary>
        public long? Version { get; }
        /// <summary>Gets the filename at the time of the event when supplied.</summary>
        public string Filename { get; }
    }
}
