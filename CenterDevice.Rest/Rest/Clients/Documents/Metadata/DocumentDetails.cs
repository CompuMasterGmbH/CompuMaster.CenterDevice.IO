using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace CenterDevice.Rest.Clients.Documents.Metadata
{
    /// <summary>Contains immutable selected document metadata without inferring omitted fields or permissions.</summary>
    /// <remarks>Original dates and hidden-owner/uploader placeholders are retained. Missing values remain unknown; representation catalogs, lock names and future fields remain server-authoritative.</remarks>
    public sealed class DocumentDetails
    {
        private readonly JObject raw;
        internal DocumentDetails(JObject data)
        {
            raw = (JObject)data.DeepClone();
            foreach (var name in TextFields) DocumentDetailsParser.Text(data[name]);
            foreach (var name in IntegerFields) DocumentDetailsParser.Integer(data[name], name == "version");
            foreach (var name in new[] { "tags", "locks", "folders" }) DocumentDetailsParser.Strings(data[name]);
            var representations = DocumentDetailsParser.Object(data["representations"]);
            if (representations != null) foreach (var field in representations.Properties()) DocumentDetailsParser.Text(field.Value);
            var users = DocumentDetailsParser.Object(data["users"]); Users = users == null ? null : new DocumentVisibilityDetails(users);
            var groups = DocumentDetailsParser.Object(data["groups"]); Groups = groups == null ? null : new DocumentVisibilityDetails(groups);
            var collections = DocumentDetailsParser.Object(data["collections"]); Collections = collections == null ? null : new DocumentVisibilityDetails(collections);
            DocumentDetailsParser.Object(data["extended-metadata"]); DocumentDetailsParser.Object(data["indirect-sharings"]);
            VersionDetails = DocumentDetailsParser.Objects(data["version-details"], value => new DocumentDetails(value));
            ArchivedDates = DocumentDetailsParser.Objects(data["archived-dates"], value => new DocumentArchivedDateDetails(value));
        }
        private static readonly string[] TextFields = { "id", "owner", "uploader", "filename", "mimetype", "title", "author", "upload-date", "version-date", "document-date", "archived-date", "deletion-date", "locked-by", "link", "hash", "extension" };
        private static readonly string[] IntegerFields = { "version", "size", "pages", "comments" };
        /// <summary>Gets the original document identifier, or null when explicitly excluded.</summary>
        public string Id => DocumentDetailsParser.Text(raw["id"]);
        /// <summary>Gets the original owner identifier or hidden-owner placeholder, or null when not provided.</summary>
        public string Owner => DocumentDetailsParser.Text(raw["owner"]);
        /// <summary>Gets the original version uploader or visibility placeholder, or null when not provided.</summary>
        public string Uploader => DocumentDetailsParser.Text(raw["uploader"]);
        /// <summary>Gets the original version filename, or null when not provided.</summary>
        public string Filename => DocumentDetailsParser.Text(raw["filename"]);
        /// <summary>Gets the original version MIME type, or null when not provided.</summary>
        public string MimeType => DocumentDetailsParser.Text(raw["mimetype"]);
        /// <summary>Gets the original version title, or null when not provided.</summary>
        public string Title => DocumentDetailsParser.Text(raw["title"]);
        /// <summary>Gets the original version author, or null when not provided.</summary>
        public string Author => DocumentDetailsParser.Text(raw["author"]);
        /// <summary>Gets the original upload-date string without timezone conversion, or null when unknown.</summary>
        public string UploadDate => DocumentDetailsParser.Text(raw["upload-date"]);
        /// <summary>Gets the original version-date string without timezone conversion, or null when unknown.</summary>
        public string VersionDate => DocumentDetailsParser.Text(raw["version-date"]);
        /// <summary>Gets the original document-date string without inventing a local fallback, or null when unknown.</summary>
        public string DocumentDate => DocumentDetailsParser.Text(raw["document-date"]);
        /// <summary>Gets the original oldest archived-date string, or null when not provided.</summary>
        public string ArchivedDate => DocumentDetailsParser.Text(raw["archived-date"]);
        /// <summary>Gets the original deletion-date string, or null when not provided.</summary>
        public string DeletionDate => DocumentDetailsParser.Text(raw["deletion-date"]);
        /// <summary>Gets the original lock-owner identifier, or null when not provided.</summary>
        /// <remarks>Omission does not establish the absence of locks or permission to change a version.</remarks>
        public string LockedBy => DocumentDetailsParser.Text(raw["locked-by"]);
        /// <summary>Gets the original link identifier, or null when not provided.</summary>
        public string Link => DocumentDetailsParser.Text(raw["link"]);
        /// <summary>Gets the original MD5 hash string, or null when not provided.</summary>
        public string Hash => DocumentDetailsParser.Text(raw["hash"]);
        /// <summary>Gets the original extension, or null when not provided.</summary>
        public string Extension => DocumentDetailsParser.Text(raw["extension"]);
        /// <summary>Gets the original positive version integer without narrowing its width, or null when unknown.</summary>
        public string Version => DocumentDetailsParser.Integer(raw["version"], true);
        /// <summary>Gets the original nonnegative byte-size integer without narrowing its width, or null when unknown.</summary>
        public string Size => DocumentDetailsParser.Integer(raw["size"], false);
        /// <summary>Gets the original nonnegative page-count integer without narrowing its width, or null when unknown.</summary>
        public string Pages => DocumentDetailsParser.Integer(raw["pages"], false);
        /// <summary>Gets the original nonnegative comment-count integer without narrowing its width, or null when unknown.</summary>
        public string Comments => DocumentDetailsParser.Integer(raw["comments"], false);
        /// <summary>Gets immutable original tag strings, or null when not provided.</summary>
        public IReadOnlyList<string> Tags => DocumentDetailsParser.Strings(raw["tags"]);
        /// <summary>Gets immutable original lock names, retaining future values and null when unknown.</summary>
        public IReadOnlyList<string> Locks => DocumentDetailsParser.Strings(raw["locks"]);
        /// <summary>Gets immutable original immediate-parent folder identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> Folders => DocumentDetailsParser.Strings(raw["folders"]);
        /// <summary>Gets visible user sharing identities and hidden counts, or null when not provided.</summary>
        public DocumentVisibilityDetails Users { get; }
        /// <summary>Gets visible group sharing identities and hidden counts, or null when not provided.</summary>
        public DocumentVisibilityDetails Groups { get; }
        /// <summary>Gets visible collection identities and hidden counts, or null when not provided.</summary>
        public DocumentVisibilityDetails Collections { get; }
        /// <summary>Gets immutable visible version details, or null when omitted or hidden by the server.</summary>
        /// <remarks>Explicit empty lists remain distinct from unknown lists. Guests do not receive older-version information; no complete history or ordering is inferred.</remarks>
        public IReadOnlyList<DocumentDetails> VersionDetails { get; }
        /// <summary>Gets immutable per-collection archive dates, or null when not provided.</summary>
        public IReadOnlyList<DocumentArchivedDateDetails> ArchivedDates { get; }
        /// <summary>Returns a defensive copy of original representation availability metadata.</summary>
        /// <returns>Original yes/no/supported or future catalog strings, or null when not provided.</returns>
        /// <remarks>Supported means not yet available; no local readiness, conversion request or polling is inferred.</remarks>
        public JObject GetRepresentations() => (JObject)DocumentDetailsParser.Object(raw["representations"])?.DeepClone();
        /// <summary>Returns a defensive copy of complete original extended metadata.</summary>
        /// <returns>Original namespaces, typed section values and future fields, or null when not provided.</returns>
        public JObject GetExtendedMetadata() => (JObject)DocumentDetailsParser.Object(raw["extended-metadata"])?.DeepClone();
        /// <summary>Returns a defensive copy of original indirect sharing metadata.</summary>
        /// <returns>Complete original indirect-users/indirect-groups information, or null when not requested or exposed.</returns>
        public JObject GetIndirectSharings() => (JObject)DocumentDetailsParser.Object(raw["indirect-sharings"])?.DeepClone();
        /// <summary>Returns a defensive copy of every original metadata field.</summary>
        /// <returns>Complete metadata including unknown fields, explicit null and nested original values.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }
    /// <summary>Contains immutable visible identities and the server-reported hidden count.</summary>
    public sealed class DocumentVisibilityDetails
    {
        internal DocumentVisibilityDetails(JObject data) { Visible = DocumentDetailsParser.Strings(data["visible"]); NotVisibleCount = DocumentDetailsParser.Integer(data["not-visible-count"], false); }
        /// <summary>Gets immutable original visible identifiers, or null when unknown.</summary>
        public IReadOnlyList<string> Visible { get; }
        /// <summary>Gets the original nonnegative hidden count without narrowing its width, or null when unknown.</summary>
        public string NotVisibleCount { get; }
    }
    /// <summary>Contains an original archive date associated with a collection.</summary>
    public sealed class DocumentArchivedDateDetails
    {
        internal DocumentArchivedDateDetails(JObject data) { Collection = DocumentDetailsParser.Text(data["collection"]); ArchivedDate = DocumentDetailsParser.Text(data["archived-date"]); }
        /// <summary>Gets the original collection identifier, or null when not provided.</summary>
        public string Collection { get; }
        /// <summary>Gets the original date string without timezone conversion, or null when unknown.</summary>
        public string ArchivedDate { get; }
    }
    internal static class DocumentDetailsParser
    {
        internal static DocumentDetails Parse(string content, string selectedId, long? version, bool idExcluded)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the document metadata.");
                    var result = new DocumentDetails(data);
                    if ((!idExcluded && result.Id == null) || result.Id != null && (string.IsNullOrWhiteSpace(result.Id) || Identity(result.Id) != Identity(selectedId))) throw new InvalidDataException("The document identity does not match the selection.");
                    if (version.HasValue && result.Version != null && result.Version != version.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new InvalidDataException("The document version does not match the selection.");
                    return result;
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The document metadata has invalid identity, version or structure.", error); }
        }
        private static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
        internal static string Text(JToken value) { if (value == null || value.Type == JTokenType.Null) return null; if (value.Type != JTokenType.String) throw new InvalidDataException("Document metadata must be a string or null."); return value.Value<string>(); }
        internal static JObject Object(JToken value) { if (value == null || value.Type == JTokenType.Null) return null; if (!(value is JObject data)) throw new InvalidDataException("Document metadata must be an object or null."); return data; }
        internal static string Integer(JToken value, bool positive)
        { if (value == null || value.Type == JTokenType.Null) return null; if (value.Type != JTokenType.Integer || !System.Text.RegularExpressions.Regex.IsMatch(value.ToString(Formatting.None), positive ? @"^[1-9][0-9]*$" : @"^(0|[1-9][0-9]*)$")) throw new InvalidDataException("Document metadata must be an original integer in its documented range."); return value.ToString(Formatting.None); }
        internal static IReadOnlyList<string> Strings(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (!(value is JArray entries)) throw new InvalidDataException("Document metadata must be an array."); var result = new List<string>(); foreach(var entry in entries) { if (entry.Type != JTokenType.String) throw new InvalidDataException("Document metadata arrays must contain strings."); result.Add(entry.Value<string>()); } return result.AsReadOnly(); }
        internal static IReadOnlyList<T> Objects<T>(JToken value, Func<JObject, T> convert)
        { if (value == null || value.Type == JTokenType.Null) return null; if (!(value is JArray entries)) throw new InvalidDataException("Document metadata must be an object array."); var result = new List<T>(); foreach(var entry in entries) { if (!(entry is JObject data)) throw new InvalidDataException("Document metadata entries must be objects."); result.Add(convert(data)); } return result.AsReadOnly(); }
    }
}
