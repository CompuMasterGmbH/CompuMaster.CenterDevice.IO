using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Specifies copied optional metadata and actions for a configured upload.</summary>
    /// <remarks>Represents the metadata object, not its outer envelope. Filename and measured size are supplied by the upload method. Null setters omit choices, future fields survive, and no sharing or processing defaults are inserted.</remarks>
    public sealed class DocumentUploadOptions : JObject
    {
        /// <summary>Initializes optional upload metadata without defaults.</summary>
        public DocumentUploadOptions() { }
        /// <summary>Copies complete optional metadata including future fields.</summary>
        /// <param name="other">The optional metadata object to copy.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public DocumentUploadOptions(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { if (other is DocumentUploadOptions options) MimeType = options.MimeType; }
        /// <summary>Gets or sets the explicit document-part MIME type, or null for application/octet-stream.</summary>
        /// <remarks>This transport choice is separate from the JSON metadata and is copied before authorization. Original casing and parameters remain intact; no MIME type is detected locally.</remarks>
        public string MimeType { get; set; }
        /// <summary>Gets or sets copied optional document title, author and original date metadata.</summary>
        public DocumentUploadMetadata Document { get => this["document"] == null ? null : new DocumentUploadMetadata((JObject)this["document"]); set => UploadJson.Write(this, "document", value); }
        /// <summary>Gets or sets copied explicit post-upload actions.</summary>
        /// <remarks>Actions are supported only when creating a document, not a new version.</remarks>
        public DocumentUploadActions Actions { get => this["actions"] == null ? null : new DocumentUploadActions((JObject)this["actions"]); set => UploadJson.Write(this, "actions", value); }
        /// <summary>Gets or sets whether invalid extended metadata rejects the upload, or null to omit the choice.</summary>
        public bool? RequireValidExtendedMetadata { get => (bool?)this["require-valid-extended-metadata"]; set => UploadJson.Write(this, "require-valid-extended-metadata", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets copied complete writable extended-metadata namespaces.</summary>
        /// <remarks>Date and decimal values remain original strings. The common namespace is read-only.</remarks>
        public JObject ExtendedMetadata { get => this["extended-metadata"] == null ? null : (JObject)this["extended-metadata"].DeepClone(); set => UploadJson.Write(this, "extended-metadata", value); }
        internal JObject Snapshot(bool newVersion)
        {
            if (MimeType != null && !MediaTypeHeaderValue.TryParse(MimeType, out _)) throw new ArgumentException("The explicit document MIME type is invalid.");
            var snapshot = (JObject)DeepClone();
            if (snapshot["document"] != null)
            {
                if (!(snapshot["document"] is JObject document)) throw new ArgumentException("Document upload metadata must be an object.");
                if (document["filename"] != null || document["size"] != null) throw new ArgumentException("Filename and measured size are supplied by the upload method.");
                foreach (var name in new[] { "title", "author", "document-date" }) UploadJson.Kind(document, name, JTokenType.String);
            }
            UploadJson.Kind(snapshot, "require-valid-extended-metadata", JTokenType.Boolean);
            if (snapshot["extended-metadata"] != null)
            {
                var validation = new DocumentMetadataUpdateRequest { ["metadata"] = snapshot["extended-metadata"].DeepClone() };
                validation.SnapshotForSend();
            }
            if (snapshot["actions"] != null)
            {
                if (newVersion) throw new NotSupportedException("New document versions do not support upload actions.");
                if (!(snapshot["actions"] is JObject actions)) throw new ArgumentException("Upload actions must be an object.");
                DocumentUploadActions.Validate(actions);
            }
            return snapshot;
        }
    }
    /// <summary>Specifies optional title, author and original ISO document date without conversion.</summary>
    public sealed class DocumentUploadMetadata : JObject
    {
        /// <summary>Initializes optional document metadata without defaults.</summary>
        public DocumentUploadMetadata() { }
        /// <summary>Copies original optional document metadata.</summary>
        /// <param name="other">The original metadata to copy.</param>
        public DocumentUploadMetadata(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original document title, or null to omit it.</summary>
        public string Title { get => (string)this["title"]; set => UploadJson.Text(this, "title", value); }
        /// <summary>Gets or sets the original author, or null to omit it.</summary>
        public string Author { get => (string)this["author"]; set => UploadJson.Text(this, "author", value); }
        /// <summary>Gets or sets the original ISO document date, or null to retain the server default.</summary>
        /// <remarks>No time-zone conversion or local date parsing is performed. Existing upload date overloads retain their previous behavior.</remarks>
        public string DocumentDate { get => (string)this["document-date"]; set => UploadJson.Text(this, "document-date", value); }
    }
    /// <summary>Specifies copied explicit actions performed after a new document upload.</summary>
    /// <remarks>Archive extraction can replace the original document and create new collections/folders. Email extraction can create attachments. The caller must account for asynchronous outcomes and cleanup; upload success does not prove completion.</remarks>
    public sealed class DocumentUploadActions : JObject
    {
        /// <summary>Initializes upload actions without defaults.</summary>
        public DocumentUploadActions() { }
        /// <summary>Copies complete actions including future fields.</summary>
        /// <param name="other">The actions to copy.</param>
        public DocumentUploadActions(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets copied collection identifiers, or null to omit the action.</summary>
        public IReadOnlyList<string> AddToCollections { get => UploadJson.Strings(this, "add-to-collection"); set => UploadJson.Write(this, "add-to-collection", value == null ? null : new JArray(value)); }
        /// <summary>Gets or sets a copied folder identifier string or array, or null to omit the action.</summary>
        /// <remarks>The server applies only the first folder per collection and may report failed folder identifiers.</remarks>
        public JToken AddToFolders { get => this["add-to-folder"]?.DeepClone(); set => UploadJson.Write(this, "add-to-folder", value); }
        /// <summary>Gets or sets copied explicit user, email and group sharing selections.</summary>
        public DocumentUploadSharing Share { get => this["share"] == null ? null : new DocumentUploadSharing((JObject)this["share"]); set => UploadJson.Write(this, "share", value); }
        /// <summary>Gets or sets copied original tags, or null to omit the action.</summary>
        public IReadOnlyList<string> AddTags { get => UploadJson.Strings(this, "add-tag"); set => UploadJson.Write(this, "add-tag", value == null ? null : new JArray(value)); }
        /// <summary>Gets or sets copied lock names, or null to omit the action.</summary>
        public IReadOnlyList<string> AddLocks { get => UploadJson.Strings(this, "add-lock"); set => UploadJson.Write(this, "add-lock", value == null ? null : new JArray(value)); }
        /// <summary>Gets or sets copied archive extraction as a boolean or an object containing keep-folder-structure.</summary>
        /// <remarks>Omission retains the server default. Keeping structure allows at most one collection or folder location and can create an additional collection/folder. Extracted files receive other actions; the processed archive is removed by the server.</remarks>
        public JToken ExtractArchive { get => this["extract-archive"]?.DeepClone(); set => UploadJson.Write(this, "extract-archive", value); }
        /// <summary>Gets or sets copied explicit email processing choices.</summary>
        public DocumentUploadEmailProcessing EmailProcessing { get => this["email-processing"] == null ? null : new DocumentUploadEmailProcessing((JObject)this["email-processing"]); set => UploadJson.Write(this, "email-processing", value); }
        /// <summary>Gets or sets copied duplicate-check criteria, or null to omit duplicate detection.</summary>
        /// <remarks>An explicit empty object enables the manufacturer's default duplicate criteria; omission does not enable the check.</remarks>
        public DocumentUploadDuplicateCheck CheckForDuplicate { get => this["check-for-duplicate"] == null ? null : new DocumentUploadDuplicateCheck((JObject)this["check-for-duplicate"]); set => UploadJson.Write(this, "check-for-duplicate", value); }
        internal static void Validate(JObject actions)
        {
            foreach (var name in new[] { "add-to-collection", "add-tag", "add-lock" }) UploadJson.Array(actions, name);
            var folders = actions["add-to-folder"];
            if (folders != null && folders.Type != JTokenType.String) UploadJson.Array(actions, "add-to-folder");
            if (actions["share"] != null)
            { if (!(actions["share"] is JObject share)) throw new ArgumentException("Upload sharing must be an object."); foreach (var name in new[] { "users", "emails", "groups" }) UploadJson.Array(share, name); }
            if (actions["check-for-duplicate"] != null)
            { if (!(actions["check-for-duplicate"] is JObject check)) throw new ArgumentException("Duplicate criteria must be an object."); foreach (var name in new[] { "allow-different-filenames", "allow-different-owners" }) UploadJson.Kind(check, name, JTokenType.Boolean); }
            var archive = actions["extract-archive"];
            if (archive != null && archive.Type != JTokenType.Boolean)
            {
                if (!(archive is JObject settings)) throw new ArgumentException("Archive extraction must be a boolean or object.");
                UploadJson.Kind(settings, "keep-folder-structure", JTokenType.Boolean);
                if ((bool?)settings["keep-folder-structure"] == true)
                {
                    var count = (actions["add-to-collection"] as JArray)?.Count ?? 0;
                    count += folders is JArray folderArray ? folderArray.Count : folders == null ? 0 : 1;
                    if (count > 1) throw new ArgumentException("Structure-preserving extraction supports at most one upload location.");
                }
            }
            if (actions["email-processing"] != null)
            {
                if (!(actions["email-processing"] is JObject email)) throw new ArgumentException("Email processing must be an object.");
                UploadJson.Kind(email, "store-email", JTokenType.String); UploadJson.Kind(email, "extract-attachments", JTokenType.Boolean); UploadJson.Kind(email, "attachment-text-indexing", JTokenType.Boolean);
                var store = (string)email["store-email"]; var index = (bool?)email["attachment-text-indexing"] ?? true; var extract = (bool?)email["extract-attachments"] ?? false;
                if (store == "none" && (index || !extract) || store == "text" && index) throw new ArgumentException("The explicit email processing choices conflict with documented server defaults.");
            }
        }
    }
    /// <summary>Specifies copied explicit recipients for upload sharing.</summary>
    public sealed class DocumentUploadSharing : JObject
    {
        /// <summary>Initializes sharing selections without defaults.</summary>
        public DocumentUploadSharing() { }
        /// <summary>Copies original sharing selections.</summary>
        /// <param name="other">The original selections to copy.</param>
        public DocumentUploadSharing(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets copied explicit user identifiers.</summary>
        public IReadOnlyList<string> Users { get => UploadJson.Strings(this, "users"); set => UploadJson.Write(this, "users", value == null ? null : new JArray(value)); }
        /// <summary>Gets or sets copied original email addresses.</summary>
        public IReadOnlyList<string> Emails { get => UploadJson.Strings(this, "emails"); set => UploadJson.Write(this, "emails", value == null ? null : new JArray(value)); }
        /// <summary>Gets or sets copied explicit group identifiers.</summary>
        public IReadOnlyList<string> Groups { get => UploadJson.Strings(this, "groups"); set => UploadJson.Write(this, "groups", value == null ? null : new JArray(value)); }
    }
    /// <summary>Specifies explicit email storage, extraction and indexing choices.</summary>
    public sealed class DocumentUploadEmailProcessing : JObject
    {
        /// <summary>Initializes email processing without defaults.</summary>
        public DocumentUploadEmailProcessing() { }
        /// <summary>Copies original email processing choices.</summary>
        /// <param name="other">The original choices to copy.</param>
        public DocumentUploadEmailProcessing(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original store-email choice, or null to retain the server default original.</summary>
        /// <remarks>Known values are none, text and original. Text is supported only for eml; future strings remain server-validated.</remarks>
        public string StoreEmail { get => (string)this["store-email"]; set => UploadJson.Text(this, "store-email", value); }
        /// <summary>Gets or sets attachment extraction, or null to retain the server default false.</summary>
        public bool? ExtractAttachments { get => (bool?)this["extract-attachments"]; set => UploadJson.Write(this, "extract-attachments", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets attachment indexing, or null to retain the server default true.</summary>
        public bool? AttachmentTextIndexing { get => (bool?)this["attachment-text-indexing"]; set => UploadJson.Write(this, "attachment-text-indexing", value.HasValue ? new JValue(value.Value) : null); }
    }
    /// <summary>Specifies explicit filename and owner relaxations for duplicate detection.</summary>
    public sealed class DocumentUploadDuplicateCheck : JObject
    {
        /// <summary>Initializes duplicate criteria without defaults.</summary>
        public DocumentUploadDuplicateCheck() { }
        /// <summary>Copies original duplicate criteria.</summary>
        /// <param name="other">The original criteria to copy.</param>
        public DocumentUploadDuplicateCheck(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets whether filenames may differ, or null to retain the server default false.</summary>
        public bool? AllowDifferentFilenames { get => (bool?)this["allow-different-filenames"]; set => UploadJson.Write(this, "allow-different-filenames", value.HasValue ? new JValue(value.Value) : null); }
        /// <summary>Gets or sets whether owners may differ, or null to retain the server default false.</summary>
        public bool? AllowDifferentOwners { get => (bool?)this["allow-different-owners"]; set => UploadJson.Write(this, "allow-different-owners", value.HasValue ? new JValue(value.Value) : null); }
    }
    internal static class UploadJson
    {
        internal static void Write(JObject target, string name, JToken value) { if (value == null) target.Remove(name); else target[name] = value.DeepClone(); }
        internal static void Text(JObject target, string name, string value) => Write(target, name, value == null ? null : new JValue(value));
        internal static IReadOnlyList<string> Strings(JObject target, string name) => target[name] == null ? null : System.Array.AsReadOnly(((JArray)target[name]).Values<string>().ToArray());
        internal static void Kind(JObject target, string name, JTokenType kind) { if (target[name] != null && target[name].Type != kind) throw new ArgumentException("The " + name + " upload field has an invalid JSON kind."); }
        internal static void Array(JObject target, string name) { if (target[name] != null && (!(target[name] is JArray array) || array.Any(item => item.Type != JTokenType.String))) throw new ArgumentException("The " + name + " upload field must be a string array."); }
    }
}
