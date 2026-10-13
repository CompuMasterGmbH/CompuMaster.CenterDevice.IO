using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Specifies explicit optional filters and paging for current document metadata.</summary>
    public sealed class DocumentMetadataListingOptions
    {
        /// <summary>Gets or sets individual document identifiers, or null to omit them.</summary>
        public IEnumerable<string> Ids { get; set; }
        /// <summary>Gets or sets the original collection filter, or null to omit it.</summary>
        public string Collection { get; set; }
        /// <summary>Gets or sets the original immediate-parent folder filter, including none for a selected collection's top level.</summary>
        public string Folder { get; set; }
        /// <summary>Gets or sets the original inclusive lower last-change date bound, or null to omit it.</summary>
        public string LastChangeFrom { get; set; }
        /// <summary>Gets or sets the original exclusive upper last-change date bound, or null to omit it.</summary>
        public string LastChangeTo { get; set; }
        /// <summary>Gets or sets the explicit public-collection inclusion choice, or null to retain server defaults.</summary>
        /// <remarks>The server ignores this choice for roles other than internal/admin.</remarks>
        public bool? IncludePublic { get; set; }
        /// <summary>Gets or sets the nonnegative page offset, or null to omit it.</summary>
        public long? Offset { get; set; }
        /// <summary>Gets or sets the positive requested page size, or null to omit it.</summary>
        /// <remarks>The server may return fewer records due to its current maximum; no client cap is inserted.</remarks>
        public long? Rows { get; set; }
        /// <summary>Gets or sets individual included metadata fields, or null to omit them.</summary>
        public IEnumerable<string> Includes { get; set; }
        /// <summary>Gets or sets individual excluded metadata fields, or null to omit them.</summary>
        /// <remarks>Includes/excludes are exclusive; base cannot be excluded.</remarks>
        public IEnumerable<string> Excludes { get; set; }
    }
}
