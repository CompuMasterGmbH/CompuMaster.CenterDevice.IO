using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Contains an immutable original document query page without inferring a complete result set.</summary>
    public sealed class DocumentQueryPage
    {
        private readonly JObject raw;
        internal DocumentQueryPage(JObject data, string json, bool search, bool idExcluded)
        {
            raw = (JObject)data.DeepClone(); RawJson = json;
            Hits = DocumentDetailsParser.Integer(data["hits"], false); if (Hits == null) throw new InvalidDataException("The total hit count is required.");
            var documents = new List<DocumentDetails>(); var entries = data["documents"];
            if (!(entries is JArray) && (!search || entries != null)) throw new InvalidDataException("A document page must provide an array; search may omit an empty page.");
            if (entries is JArray list) foreach (var entry in list)
            {
                if (!(entry is JObject metadata)) throw new InvalidDataException("Document hits must be objects.");
                if (search && metadata["score"] != null && metadata["score"].Type != JTokenType.Null)
                {
                    var score = metadata["score"];
                    if ((score.Type != JTokenType.Integer && score.Type != JTokenType.Float) || !System.Text.RegularExpressions.Regex.IsMatch(score.ToString(Formatting.None), @"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$")) throw new InvalidDataException("Search scores must be finite JSON numbers.");
                }
                if (search) DocumentDetailsParser.Strings(metadata["workflows"]);
                var details = new DocumentDetails(metadata);
                if ((!idExcluded && details.Id == null) || details.Id != null && string.IsNullOrWhiteSpace(details.Id)) throw new InvalidDataException("Document identity must be present unless explicitly excluded.");
                documents.Add(details);
            }
            Documents = documents.AsReadOnly();
            var facets = data["facets"]; if (search && facets != null && facets.Type != JTokenType.Null && !(facets is JArray) && !(facets is JObject)) throw new InvalidDataException("Facets must retain their original object or array structure.");
        }
        /// <summary>Gets the original nonnegative total-hit integer without narrowing its width.</summary>
        /// <remarks>This is the total found by the query, not the number of documents on this page.</remarks>
        public string Hits { get; }
        /// <summary>Gets immutable metadata in original server order for this page.</summary>
        /// <remarks>Search may omit documents for no hits or an out-of-range page. Score/workflows and future search fields remain accessible in each document's raw details. No full history, exhaustive visibility or stable snapshot is inferred.</remarks>
        public IReadOnlyList<DocumentDetails> Documents { get; }
        /// <summary>Gets the exact original response JSON for lossless access to numeric and future values.</summary>
        /// <remarks>Parsed JSON numbers have Json.NET's default bounded floating-point precision; this original text preserves exact numeric spelling.</remarks>
        public string RawJson { get; }
        /// <summary>Returns a defensive copy of the original facet metadata.</summary>
        /// <returns>The original object or array, or null when absent or JSON null.</returns>
        /// <remarks>The reference describes an array and provides keyed-object examples; neither shape is normalized or interpreted as permission.</remarks>
        public JToken GetFacets() => raw["facets"] == null || raw["facets"].Type == JTokenType.Null ? null : raw["facets"].DeepClone();
        /// <summary>Returns a defensive copy of all parsed page fields.</summary>
        /// <returns>Original structure and future fields with bounded parsed-number precision.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
        internal static DocumentQueryPage Parse(string content, bool search, bool idExcluded)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                { var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the query page."); return new DocumentQueryPage(data, content, search, idExcluded); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The document query page has invalid metadata or structure.", error); }
        }
    }
}
