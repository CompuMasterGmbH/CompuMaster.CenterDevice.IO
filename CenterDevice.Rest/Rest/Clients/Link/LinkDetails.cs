using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Link
{
    /// <summary>Contains immutable complete link metadata and access controls.</summary>
    /// <remarks>Absent values remain unknown. Original date strings, arbitrary-width counters and future fields survive; no creator/permission/default or URL fallback is inferred. Returned password metadata must not be logged.</remarks>
    public sealed class LinkDetails
    {
        private readonly JObject data;
        internal LinkDetails(JObject data, string rawJson, bool creation = false)
        {
            LinkContractJson.ValidateMetadata(data, false, creation); this.data = (JObject)data.DeepClone(); RawJson = rawJson;
        }
        /// <summary>Gets the confirmed original link identity.</summary>
        public string Id => (string)data["id"];
        /// <summary>Gets the original web, or null when absent or JSON null.</summary>
        public string Web => (string)data["web"];
        /// <summary>Gets the original download, or null when absent or JSON null.</summary>
        public string Download => (string)data["download"];
        /// <summary>Gets the original rest, or null when absent or JSON null.</summary>
        public string Rest => (string)data["rest"];
        /// <summary>Gets the original document-id, or null when absent or JSON null.</summary>
        public string DocumentId => (string)data["document-id"];
        /// <summary>Gets the original collection, or null when absent or JSON null.</summary>
        public string Collection => (string)data["collection"];
        /// <summary>Gets the original folder, or null when absent or JSON null.</summary>
        public string Folder => (string)data["folder"];
        /// <summary>Gets the original downloads integer text without width narrowing, or null when absent or JSON null.</summary>
        public string Downloads => LinkContractJson.Integer(data["downloads"]);
        /// <summary>Gets the original views integer text without width narrowing, or null when absent or JSON null.</summary>
        public string Views => LinkContractJson.Integer(data["views"]);
        /// <summary>Gets the original expiry-date access-control choice, or null when unknown.</summary>
        public string ExpiryDate => (string)data["access-control"]?["expiry-date"];
        /// <summary>Gets the original max-downloads access-control choice, or null when unknown.</summary>
        public string MaxDownloads => LinkContractJson.Integer(data["access-control"]?["max-downloads"]);
        /// <summary>Gets the original password access-control choice, or null when unknown.</summary>
        public string Password => (string)data["access-control"]?["password"];
        /// <summary>Gets the original view-only access-control choice, or null when unknown.</summary>
        public bool? ViewOnly => (bool?)data["access-control"]?["view-only"];
        /// <summary>Gets the original hide-date access-control choice, or null when unknown.</summary>
        public bool? HideDate => (bool?)data["access-control"]?["hide-date"];
        /// <summary>Copies complete access controls, or returns null when absent or JSON null.</summary>
        /// <returns>A defensive copy retaining future access-control fields.</returns>
        public JObject GetAccessControl() => data["access-control"] is JObject controls ? (JObject)controls.DeepClone() : null;
        /// <summary>Gets exact selected/create response JSON, or null for embedded list metadata.</summary>
        public string RawJson { get; }
        /// <summary>Copies complete original parsed metadata including future fields.</summary>
        /// <returns>A defensive copy of the complete metadata.</returns>
        public JObject GetRawDetails() => (JObject)data.DeepClone();
    }
    /// <summary>Contains immutable complete upload-link metadata without URL reconstruction.</summary>
    /// <remarks>Absent values remain unknown. Original date strings, arbitrary-width counters and future fields survive; no creator/permission/default or URL fallback is inferred. Returned password metadata must not be logged.</remarks>
    public sealed class UploadLinkDetails
    {
        private readonly JObject data;
        internal UploadLinkDetails(JObject data, string rawJson, bool creation = false)
        {
            LinkContractJson.ValidateMetadata(data, true, creation); this.data = (JObject)data.DeepClone(); RawJson = rawJson;
        }
        /// <summary>Gets the confirmed original link identity.</summary>
        public string Id => (string)data["id"];
        /// <summary>Gets the original web, or null when absent or JSON null.</summary>
        public string Web => (string)data["web"];
        /// <summary>Gets the original creator, or null when absent or JSON null.</summary>
        public string Creator => (string)data["creator"];
        /// <summary>Gets the original name, or null when absent or JSON null.</summary>
        public string Name => (string)data["name"];
        /// <summary>Gets the original creation-date, or null when absent or JSON null.</summary>
        public string CreationDate => (string)data["creation-date"];
        /// <summary>Gets the original expiry-date, or null when absent or JSON null.</summary>
        public string ExpiryDate => (string)data["expiry-date"];
        /// <summary>Gets the original collection, or null when absent or JSON null.</summary>
        public string Collection => (string)data["collection"];
        /// <summary>Gets the original folder, or null when absent or JSON null.</summary>
        public string Folder => (string)data["folder"];
        /// <summary>Gets the original max-documents integer text without width narrowing, or null when absent or JSON null.</summary>
        public string MaxDocuments => LinkContractJson.Integer(data["max-documents"]);
        /// <summary>Gets the original max-bytes integer text without width narrowing, or null when absent or JSON null.</summary>
        public string MaxBytes => LinkContractJson.Integer(data["max-bytes"]);
        /// <summary>Gets the original uploads integer text without width narrowing, or null when absent or JSON null.</summary>
        public string Uploads => LinkContractJson.Integer(data["uploads"]);
        /// <summary>Gets the original uploaded-bytes integer text without width narrowing, or null when absent or JSON null.</summary>
        public string UploadedBytes => LinkContractJson.Integer(data["uploaded-bytes"]);
        /// <summary>Gets the original password, or null when absent or JSON null.</summary>
        public string Password => (string)data["password"];
        /// <summary>Gets the original email-creator-on-upload, or null when absent or JSON null.</summary>
        public bool? EmailCreatorOnUpload => (bool?)data["email-creator-on-upload"];
        /// <summary>Gets original immutable tags, or null when absent or JSON null.</summary>
        public IReadOnlyList<string> Tags => data["tags"] is JArray tags ? Array.AsReadOnly(tags.Values<string>().ToArray()) : null;
        /// <summary>Gets exact selected/create response JSON, or null for embedded list metadata.</summary>
        public string RawJson { get; }
        /// <summary>Copies complete original parsed metadata including future fields.</summary>
        /// <returns>A defensive copy of the complete metadata.</returns>
        public JObject GetRawDetails() => (JObject)data.DeepClone();
    }
    /// <summary>Contains immutable complete upload-link metadata in server order.</summary>
    public sealed class UploadLinkList
    {
        private readonly JObject data;
        internal UploadLinkList(JObject data, string rawJson)
        {
            this.data = (JObject)data.DeepClone(); RawJson = rawJson;
            if (!(data["upload-links"] is JArray links)) throw new InvalidDataException("An upload-links array is required.");
            var results = new List<UploadLinkDetails>(); var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in links)
            {
                if (!(item is JObject link)) throw new InvalidDataException("Upload-link entries must be objects.");
                var result = new UploadLinkDetails(link, null);
                if (!identities.Add(LinkContractJson.Identity(result.Id))) throw new InvalidDataException("An upload-link identity occurs twice.");
                results.Add(result);
            }
            Links = results.AsReadOnly();
        }
        /// <summary>Gets complete immutable upload-link metadata in server order.</summary>
        public IReadOnlyList<UploadLinkDetails> Links { get; }
        /// <summary>Gets exact original list response JSON.</summary>
        public string RawJson { get; }
        /// <summary>Copies the complete original list envelope including future fields.</summary>
        /// <returns>A defensive copy of the list response.</returns>
        public JObject GetRawDetails() => (JObject)data.DeepClone();
    }
}
