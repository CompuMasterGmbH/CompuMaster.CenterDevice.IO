using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Folders
{
    /// <summary>Specifies explicit optional visible-folder listing filters.</summary>
    /// <remarks>Inputs are copied before authorization. Paths retain their original slash-based server meaning; no default parent, public inclusion or sharing scope is inserted.</remarks>
    public sealed class FolderListingOptions
    {
        /// <summary>Gets or sets the original collection filter, or null to omit it.</summary>
        public string Collection { get; set; }
        /// <summary>Gets or sets the original parent filter, including none for top-level folders, or null to omit it.</summary>
        public string Parent { get; set; }
        /// <summary>Gets or sets the original document filter, or null to omit it.</summary>
        public string Document { get; set; }
        /// <summary>Gets or sets the original slash-separated path filter, or null to omit it.</summary>
        public string Path { get; set; }
        /// <summary>Gets or sets the original explicitly shared group filter, or null to omit it.</summary>
        public string Group { get; set; }
        /// <summary>Gets or sets the original partial-name query, or null to omit it.</summary>
        /// <remarks>When supplied, only Fields may also be selected.</remarks>
        public string Text { get; set; }
        /// <summary>Gets or sets copied individual folder identifiers, or null to omit them.</summary>
        public IEnumerable<string> Ids { get; set; }
        /// <summary>Gets or sets copied individual field names, or null for server defaults.</summary>
        /// <remarks>Inherited-sharings is unsupported for listings. Full-path requires both OnlySharedFolders and OnlyTopMost to be explicitly true.</remarks>
        public IEnumerable<string> Fields { get; set; }
        /// <summary>Gets or sets the explicit immediate-sharing-only choice, or null to omit it.</summary>
        public bool? OnlySharedFolders { get; set; }
        /// <summary>Gets or sets the explicit top-most-sharing choice, or null to omit it.</summary>
        public bool? OnlyTopMost { get; set; }
        /// <summary>Gets or sets the explicit public-collection inclusion choice, or null to omit it.</summary>
        public bool? IncludePublicCollections { get; set; }
    }
}
