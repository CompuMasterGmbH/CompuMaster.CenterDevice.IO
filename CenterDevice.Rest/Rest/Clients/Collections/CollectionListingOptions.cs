using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Collections
{
    /// <summary>Specifies explicit optional visible-collection listing filters.</summary>
    /// <remarks>Values are copied before authorization. Omission preserves server defaults; visibility and sharing remain server-authoritative.</remarks>
    public sealed class CollectionListingOptions
    {
        /// <summary>Gets or sets whether visible public collections are included, or null to omit the choice.</summary>
        public bool? IncludePublic { get; set; }
        /// <summary>Gets or sets the deprecated original all query choice, or null to omit it.</summary>
        /// <remarks>The manufacturer replaced this parameter with include-public. If both are supplied, precedence remains server-authoritative.</remarks>
        [Obsolete("Use IncludePublic for new requests. The manufacturer deprecated the all query parameter.")]
        public bool? All { get; set; }
        /// <summary>Gets or sets an original exact-name filter, or null to omit it.</summary>
        public string Name { get; set; }
        /// <summary>Gets or sets an explicit shared-group filter, or null to omit it.</summary>
        public string Group { get; set; }
        /// <summary>Gets or sets individual collection identifiers, or null to omit the filter.</summary>
        /// <remarks>A supplied selection must be nonempty and contain nonblank comma-free identifiers.</remarks>
        public IEnumerable<string> Ids { get; set; }
        /// <summary>Gets or sets individual response field names, or null for server defaults.</summary>
        /// <remarks>A supplied selection must be nonempty and contain nonblank comma-free names. Owner-only auditing fields remain subject to server disclosure.</remarks>
        public IEnumerable<string> Fields { get; set; }
        /// <summary>Gets or sets ordered original extended-metadata key/value filters, or null to omit them.</summary>
        /// <remarks>Keys and values are encoded individually before the complete query is encoded. Duplicate keys remain in order; the server uses their last value. A supplied selection must be nonempty; empty values remain explicit.</remarks>
        public IEnumerable<KeyValuePair<string, string>> ExtendedMetadata { get; set; }
    }
}
