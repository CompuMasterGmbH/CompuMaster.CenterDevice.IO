using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Represents an immutable confirmed upload or explicit duplicate-detection outcome.</summary>
    /// <remarks>A created document is downloadable but indexing, previews and processing can remain pending. Failed folder actions do not undo a stored document. Duplicate detection does not create a document and does not select a preferred duplicate.</remarks>
    public sealed class DocumentUploadResult
    {
        private readonly JObject details;
        private DocumentUploadResult(JObject details, string rawJson, bool duplicate, bool newVersion)
        {
            this.details = (JObject)details.DeepClone(); RawJson = rawJson; DuplicateDetected = duplicate;
            if (duplicate) { Duplicates = ReadIds(details["duplicates"], true); return; }
            if (newVersion)
            {
                var version = details["version"]; var text = version?.Type == JTokenType.String ? version.Value<string>() : version?.Type == JTokenType.Integer ? version.ToString(Formatting.None) : null;
                if (string.IsNullOrEmpty(text) || text.Any(c => c < '0' || c > '9') || text.All(c => c == '0')) throw new InvalidDataException("A confirmed version must be a positive integer string or number.");
                Version = text;
            }
            else
            {
                if (details["id"]?.Type != JTokenType.String || string.IsNullOrWhiteSpace(details.Value<string>("id"))) throw new InvalidDataException("A created upload requires a document identifier.");
                Id = details.Value<string>("id");
            }
            if (details["upload-actions"] != null)
            {
                if (!(details["upload-actions"] is JObject actions)) throw new InvalidDataException("Upload action outcomes must be an object.");
                if (actions["add-to-folder"] != null)
                { if (!(actions["add-to-folder"] is JObject folders)) throw new InvalidDataException("Folder outcomes must be an object."); if (folders["failed"] != null) FailedFolders = ReadIds(folders["failed"], false); }
            }
        }
        /// <summary>Gets the created document identifier, or null for a duplicate or new-version result.</summary>
        public string Id { get; }
        /// <summary>Gets the original positive new-version integer text, or null for a new-document result.</summary>
        /// <remarks>No integer-width narrowing or initial-version inference is performed.</remarks>
        public string Version { get; }
        /// <summary>Gets whether explicitly enabled duplicate detection returned HTTP 409 with duplicate identifiers.</summary>
        public bool DuplicateDetected { get; }
        /// <summary>Gets original duplicate identifiers, or null when the response is not a duplicate result.</summary>
        public IReadOnlyList<string> Duplicates { get; }
        /// <summary>Gets original failed folder identifiers, or null when the response omitted this outcome.</summary>
        public IReadOnlyList<string> FailedFolders { get; }
        /// <summary>Gets the exact original response JSON.</summary>
        public string RawJson { get; }
        /// <summary>Copies the complete original response including future action outcomes.</summary>
        /// <returns>A defensive copy of the response object.</returns>
        public JObject GetRawDetails() => (JObject)details.DeepClone();
        internal static DocumentUploadResult Parse(string json, bool duplicate, bool newVersion)
        {
            try
            {
                using (var source = new StringReader(json ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var details = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("An upload response has trailing JSON.");
                    return new DocumentUploadResult(details, json, duplicate, newVersion);
                }
            }
            catch (Exception failure) when (failure is JsonException || failure is InvalidDataException || failure is ArgumentException || failure is FormatException || failure is OverflowException)
            { throw new InvalidResponseDataException("The upload response has invalid metadata.", failure); }
        }
        private static IReadOnlyList<string> ReadIds(JToken token, bool requireNonempty)
        {
            if (!(token is JArray ids) || requireNonempty && ids.Count == 0 || ids.Any(item => item.Type != JTokenType.String || string.IsNullOrWhiteSpace(item.Value<string>()))) throw new InvalidDataException("Upload outcomes require original nonblank identifier arrays.");
            return Array.AsReadOnly(ids.Values<string>().ToArray());
        }
    }
}
