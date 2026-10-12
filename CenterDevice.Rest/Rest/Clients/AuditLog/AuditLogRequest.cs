using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.AuditLog
{
    /// <summary>Specifies collection audit-log search filters.</summary>
    /// <remarks>Omitted options retain server defaults; explicit empty lists remain explicit.</remarks>
    public sealed class AuditLogSearchRequest
    {
        /// <summary>Gets or sets up to 20 distinct event type names.</summary>
        [JsonProperty("types")]
        public List<string> Types { get; set; }
        /// <summary>Gets or sets up to 10 distinct event-creator user identifiers.</summary>
        [JsonProperty("users")]
        public List<string> Users { get; set; }
        /// <summary>Gets or sets up to 10 distinct document identifiers matched against event details.</summary>
        [JsonProperty("document-ids")]
        public List<string> DocumentIds { get; set; }
        /// <summary>Gets or sets inclusive lower and exclusive upper timestamp bounds.</summary>
        [JsonProperty("timestamp")]
        public AuditLogTimestampRange Timestamp { get; set; }
        /// <summary>Gets or sets the nonnegative offset, up to the current server maximum of 5000.</summary>
        [JsonProperty("offset")]
        public int? Offset { get; set; }
        /// <summary>Gets or sets a positive requested page size, or null for the server default. The server caps returned pages at 1000.</summary>
        [JsonProperty("rows")]
        public int? Rows { get; set; }
    }

    /// <summary>Specifies cursor-based collection audit-log iteration.</summary>
    /// <remarks>Scrolling requires an audited collection. The server uses logical event order rather than timestamp order.</remarks>
    public sealed class AuditLogScrollRequest
    {
        /// <summary>Gets or sets up to 20 distinct event type names.</summary>
        [JsonProperty("types")]
        public List<string> Types { get; set; }
        /// <summary>Gets or sets up to 10 distinct event-creator user identifiers.</summary>
        [JsonProperty("users")]
        public List<string> Users { get; set; }
        /// <summary>Gets or sets up to 10 distinct document identifiers matched against event details.</summary>
        [JsonProperty("document-ids")]
        public List<string> DocumentIds { get; set; }
        /// <summary>Gets or sets inclusive lower and exclusive upper timestamp bounds.</summary>
        [JsonProperty("timestamp")]
        public AuditLogTimestampRange Timestamp { get; set; }
        /// <summary>Gets or sets the previous event identifier, excluded from the next page, or null to start a new scroll.</summary>
        [JsonProperty("previous-id")]
        public string PreviousId { get; set; }
        /// <summary>Gets or sets a positive requested page size, or null for the server default. The server caps returned pages at 1000.</summary>
        [JsonProperty("rows")]
        public int? Rows { get; set; }
        /// <summary>Gets or sets asc or desc, or null for the server's ascending default.</summary>
        [JsonProperty("sort")]
        public string Sort { get; set; }
    }

    /// <summary>Specifies a timestamp interval without inferring omitted bounds.</summary>
    public sealed class AuditLogTimestampRange
    {
        /// <summary>Gets or sets the inclusive lower timestamp with its supplied offset.</summary>
        [JsonProperty("from")]
        public DateTimeOffset? From { get; set; }
        /// <summary>Gets or sets the exclusive upper timestamp with its supplied offset.</summary>
        [JsonProperty("to")]
        public DateTimeOffset? To { get; set; }
    }
}
