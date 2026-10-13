using Newtonsoft.Json;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Timeline
{
    /// <summary>Specifies complete explicit timeline search choices.</summary>
    /// <remarks>Null options are omitted; explicit empty lists retain their original meaning. Search results are ordered by decreasing timestamp.</remarks>
    public sealed class TimelineSearchRequest
    {
        /// <summary>Gets or sets up to 50 distinct original event type names.</summary>
        [JsonProperty("types")] public List<string> Types { get; set; }
        /// <summary>Gets or sets up to 10 distinct event-creator user identifiers.</summary>
        [JsonProperty("users")] public List<string> Users { get; set; }
        /// <summary>Gets or sets up to 100 distinct document identifiers matched against event details.</summary>
        [JsonProperty("document-ids")] public List<string> DocumentIds { get; set; }
        /// <summary>Gets or sets original inclusive lower and exclusive upper timestamp bounds.</summary>
        [JsonProperty("timestamp")] public TimelineTimestampRange Timestamp { get; set; }
        /// <summary>Gets or sets the nonnegative offset, up to the current server limit of 5000.</summary>
        [JsonProperty("offset")] public int? Offset { get; set; }
        /// <summary>Gets or sets a positive requested page size, or null for the server default.</summary>
        /// <remarks>The server currently returns at most 1000 events. The requested value is not clamped.</remarks>
        [JsonProperty("rows")] public int? Rows { get; set; }
    }

    /// <summary>Specifies complete explicit cursor-based timeline iteration choices.</summary>
    /// <remarks>Logical event order can differ from timestamp order. No timestamp filter or offset is added to scroll requests.</remarks>
    public sealed class TimelineScrollRequest
    {
        /// <summary>Gets or sets up to 50 distinct original event type names.</summary>
        [JsonProperty("types")] public List<string> Types { get; set; }
        /// <summary>Gets or sets up to 10 distinct event-creator user identifiers.</summary>
        [JsonProperty("users")] public List<string> Users { get; set; }
        /// <summary>Gets or sets up to 100 distinct document identifiers matched against event details.</summary>
        [JsonProperty("document-ids")] public List<string> DocumentIds { get; set; }
        /// <summary>Gets or sets the exclusive previous event identifier, or null to begin a new scroll.</summary>
        [JsonProperty("previous-id")] public string PreviousId { get; set; }
        /// <summary>Gets or sets a positive requested page size, or null for the server default.</summary>
        /// <remarks>The server currently returns at most 1000 events. The requested value is not clamped.</remarks>
        [JsonProperty("rows")] public int? Rows { get; set; }
        /// <summary>Gets or sets asc or desc, or null for the server's ascending default.</summary>
        [JsonProperty("sort")] public string Sort { get; set; }
    }

    /// <summary>Specifies original ISO8601 timeline bounds without conversion or inferred limits.</summary>
    public sealed class TimelineTimestampRange
    {
        /// <summary>Gets or sets the original inclusive lower bound, or null to omit it.</summary>
        [JsonProperty("from")] public string From { get; set; }
        /// <summary>Gets or sets the original exclusive upper bound, or null to omit it.</summary>
        [JsonProperty("to")] public string To { get; set; }
    }
}
