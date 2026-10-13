using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Link
{
    internal static class LinkContractJson
    {
        internal static void Write(JObject data, string key, JToken value) { if (value == null) data.Remove(key); else data[key] = value.DeepClone(); }
        internal static string Selection(string value) { if (string.IsNullOrWhiteSpace(value) || value == "." || value == "..") throw new ArgumentException("An original explicit link/resource identifier is required."); return Uri.EscapeDataString(value); }
        internal static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
        internal static string Integer(JToken token) => token == null || token.Type == JTokenType.Null ? null : token.ToString(Formatting.None);
        internal static JObject AccessSnapshot(JObject options)
        { var copy = options == null ? null : (JObject)options.DeepClone(); if (copy != null) ValidateAccess(copy, false); return copy; }
        internal static JObject UploadSnapshot(JObject options)
        {
            var copy = options == null ? new JObject() : (JObject)options.DeepClone();
            foreach (var key in new[] { "name", "collection", "folder", "expiry-date" }) Kind(copy, key, JTokenType.String, false);
            Kind(copy, "password", JTokenType.String, true); Kind(copy, "email-creator-on-upload", JTokenType.Boolean, false);
            if (copy["collection"] != null && copy["folder"] != null) throw new ArgumentException("Upload-link collection and folder targets are exclusive.");
            foreach (var key in new[] { "collection", "folder" }) if (copy[key] != null && string.IsNullOrWhiteSpace(copy.Value<string>(key))) throw new ArgumentException("A supplied upload target must identify a resource.");
            Strings(copy, "tags", false);
            if (copy["max-documents"] != null)
            { Kind(copy, "max-documents", JTokenType.Integer, false); var text = Integer(copy["max-documents"]); if (text.StartsWith("-", StringComparison.Ordinal) || text.All(c => c == '0')) throw new ArgumentException("The explicit upload count must be positive."); }
            if (copy["max-bytes"] != null)
            { Kind(copy, "max-bytes", JTokenType.Integer, false); if (!long.TryParse(Integer(copy["max-bytes"]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes) || bytes <= 0 || bytes % 1073741824L != 0) throw new ArgumentException("The explicit byte limit must be a positive Int64 multiple of 1 GiB."); }
            return copy;
        }
        private static void ValidateAccess(JObject data, bool response)
        {
            Kind(data, "expiry-date", JTokenType.String, response); Kind(data, "password", JTokenType.String, true); Kind(data, "max-downloads", JTokenType.Integer, response);
            Kind(data, "view-only", JTokenType.Boolean, response); Kind(data, "hide-date", JTokenType.Boolean, response);
        }
        internal static void ValidateMetadata(JObject data, bool upload, bool creation)
        {
            Kind(data, "id", JTokenType.String, false); if (string.IsNullOrWhiteSpace(data.Value<string>("id"))) throw new InvalidDataException("Confirmed link metadata requires an identity.");
            var texts = upload ? new[] { "web", "creator", "name", "creation-date", "expiry-date", "collection", "folder", "password" } : new[] { "web", "download", "rest", "document-id", "collection", "folder" };
            foreach (var key in texts) Kind(data, key, JTokenType.String, true);
            if (creation && (string.IsNullOrWhiteSpace(data.Value<string>("web")) || !upload && string.IsNullOrWhiteSpace(data.Value<string>("download")))) throw new InvalidDataException("Created links require original server URLs.");
            foreach (var key in upload ? new[] { "max-documents", "max-bytes", "uploads", "uploaded-bytes" } : new[] { "downloads", "views" }) Kind(data, key, JTokenType.Integer, true);
            if (upload) { Strings(data, "tags", true); Kind(data, "email-creator-on-upload", JTokenType.Boolean, true); }
            else if (data["access-control"] != null && data["access-control"].Type != JTokenType.Null) { if (!(data["access-control"] is JObject controls)) throw new InvalidDataException("Access controls must be an object."); ValidateAccess(controls, true); }
        }
        internal static void Correlate(string selected, string actual) { if (Identity(selected) != Identity(actual)) throw new InvalidDataException("The response identifies another selected link."); }
        private static void Kind(JObject data, string key, JTokenType type, bool nullable)
        { var value = data[key]; if (value != null && !(nullable && value.Type == JTokenType.Null) && value.Type != type) throw new ArgumentException("The " + key + " link field has an invalid JSON kind."); }
        private static void Strings(JObject data, string key, bool nullable)
        { var value = data[key]; if (value != null && !(nullable && value.Type == JTokenType.Null) && (!(value is JArray array) || array.Any(item => item.Type != JTokenType.String))) throw new ArgumentException("Link tags must remain original string arrays."); }
        internal static T Parse<T>(string json, Func<JObject, T> parse)
        {
            try
            {
                using (var source = new StringReader(json ?? "")) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                { var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new InvalidDataException("Unexpected trailing link JSON."); return parse(data); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException || error is FormatException || error is OverflowException)
            { throw new InvalidResponseDataException("The link response has invalid metadata.", error); }
        }
    }
}
