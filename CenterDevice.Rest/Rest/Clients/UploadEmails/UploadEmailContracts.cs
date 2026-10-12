using System;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.UploadEmails
{
    /// <summary>Selects how an upload endpoint stores the incoming e-mail.</summary>
    public enum UploadEmailStorage
    {
        /// <summary>Omits the incoming e-mail file.</summary>
        None,
        /// <summary>Stores the e-mail without attachments.</summary>
        Text,
        /// <summary>Stores the original e-mail including attachments.</summary>
        Original
    }

    /// <summary>Selects optional upload-email behavior without imposing server defaults.</summary>
    /// <remarks>Null properties omit fields; explicit empty sequences remain empty. Partial updates leave omitted fields unchanged. The server validates visibility and combinations involving existing endpoint state.</remarks>
    public class UploadEmailOptions
    {
        /// <summary>Gets or sets the optional display name.</summary>
        /// <value>The exact name, including an empty string, or null to omit it.</value>
        public string Name { get; set; }
        /// <summary>Gets or sets incoming e-mail storage.</summary>
        /// <value>The explicit selection, or null to omit it; creation defaults to none.</value>
        public UploadEmailStorage? StoreEmail { get; set; }
        /// <summary>Gets or sets separate attachment extraction.</summary>
        /// <value>The explicit choice, or null to omit it; creation defaults to true.</value>
        public bool? ExtractAttachments { get; set; }
        /// <summary>Gets or sets whether attachment text joins the stored e-mail's fulltext.</summary>
        /// <value>The explicit choice, or null to omit it; creation defaults to true for original storage and false otherwise.</value>
        public bool? AttachmentTextIndexing { get; set; }
        /// <summary>Gets or sets the optional visible destination collection identifier.</summary>
        /// <value>The exact nonblank identifier, or null to omit it; a folder cannot also be selected.</value>
        public string CollectionId { get; set; }
        /// <summary>Gets or sets the optional visible destination folder identifier.</summary>
        /// <value>The exact nonblank identifier, or null to omit it; a collection cannot also be selected.</value>
        public string FolderId { get; set; }
        /// <summary>Gets or sets tags applied to imported documents.</summary>
        /// <value>The exact strings to snapshot once, an explicit empty sequence, or null to omit the field.</value>
        public IEnumerable<string> Tags { get; set; }
        /// <summary>Gets or sets allowed attachment extensions.</summary>
        /// <value>The exact extensions, an explicit empty sequence to remove the whitelist, or null to omit it.</value>
        public IEnumerable<string> WhitelistedFileExtensions { get; set; }
        /// <summary>Gets or sets excluded attachment extensions.</summary>
        /// <value>The exact extensions, an explicit empty sequence to remove the blacklist, or null to omit it.</value>
        public IEnumerable<string> BlacklistedFileExtensions { get; set; }
    }

    /// <summary>Contains immutable upload-email metadata returned by the server.</summary>
    /// <remarks>The address enables imports and should be handled as confidential data. Ownership and optional server defaults are not inferred. Future storage names remain original strings.</remarks>
    public sealed class UploadEmail
    {
        internal UploadEmail(string id, string name, string email, string storeEmail, bool? extract, bool? indexing, string collection, string folder,
            IReadOnlyList<string> tags, IReadOnlyList<string> whitelist, IReadOnlyList<string> blacklist)
        { Id = id; Name = name; Email = email; StoreEmail = storeEmail; ExtractAttachments = extract; AttachmentTextIndexing = indexing; CollectionId = collection; FolderId = folder; Tags = tags; WhitelistedFileExtensions = whitelist; BlacklistedFileExtensions = blacklist; }
        /// <summary>Gets the original endpoint identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original display name.</summary>
        public string Name { get; }
        /// <summary>Gets the complete confidential import address.</summary>
        public string Email { get; }
        /// <summary>Gets the original storage selection without guessing an omitted default.</summary>
        /// <value>The server string, or null if absent or JSON null.</value>
        public string StoreEmail { get; }
        /// <summary>Gets the server's attachment extraction choice.</summary>
        /// <value>The choice, or null if absent or JSON null.</value>
        public bool? ExtractAttachments { get; }
        /// <summary>Gets the server's attachment fulltext choice.</summary>
        /// <value>The choice, or null if absent or JSON null.</value>
        public bool? AttachmentTextIndexing { get; }
        /// <summary>Gets the selected destination collection.</summary>
        /// <value>The server identifier, or null if absent or JSON null.</value>
        public string CollectionId { get; }
        /// <summary>Gets the selected destination folder.</summary>
        /// <value>The server identifier, or null if absent or JSON null.</value>
        public string FolderId { get; }
        /// <summary>Gets immutable original tag strings.</summary>
        /// <value>The read-only list, or null if absent or JSON null.</value>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>Gets immutable original allowed extensions.</summary>
        /// <value>The read-only list, or null if absent or JSON null.</value>
        public IReadOnlyList<string> WhitelistedFileExtensions { get; }
        /// <summary>Gets immutable original excluded extensions.</summary>
        /// <value>The read-only list, or null if absent or JSON null.</value>
        public IReadOnlyList<string> BlacklistedFileExtensions { get; }
    }
}
