using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Comments
{
    /// <summary>Contains immutable server-confirmed document comment metadata.</summary>
    /// <remarks>Creation returns an identifier only; absent author/dates/text remain unknown. Visibility and creator rights remain server-enforced. No document version, permission or authentication-context author is inferred.</remarks>
    public sealed class DocumentComment
    {
        private readonly JObject data;
        internal DocumentComment(JObject source, bool detailed)
        {
            data = (JObject)source.DeepClone();
            Id = CommentJson.Text(data, "id", true);
            if (string.IsNullOrWhiteSpace(Id)) throw new InvalidDataException("A comment identifier is required.");
            Author = CommentJson.Text(data, "author", detailed); Text = CommentJson.Text(data, "text", detailed);
            CreationDate = CommentJson.Text(data, "creation-date", false); EditDate = CommentJson.Text(data, "edit-date", false);
        }
        /// <summary>Gets the original comment identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original creator identifier, or null when creation omitted it.</summary>
        public string Author { get; }
        /// <summary>Gets original text including permitted formatting, or null when creation omitted it.</summary>
        public string Text { get; }
        /// <summary>Gets the original ISO creation-date string, or null when omitted or JSON null.</summary>
        public string CreationDate { get; }
        /// <summary>Gets the original ISO edit-date string, or null when omitted or JSON null.</summary>
        public string EditDate { get; }
        /// <summary>Copies complete comment metadata including future fields.</summary>
        /// <returns>A defensive copy of the original metadata.</returns>
        public JObject GetRawDetails() => (JObject)data.DeepClone();
    }
    /// <summary>Contains an immutable original list of visible comments for a selected document.</summary>
    /// <remarks>No paging, sorting, version associations or empty-list fallback is invented.</remarks>
    public sealed class DocumentCommentList
    {
        private readonly JObject data;
        internal DocumentCommentList(JObject data, string rawJson)
        {
            this.data = (JObject)data.DeepClone(); RawJson = rawJson;
            if (!(data["comments"] is JArray comments)) throw new InvalidDataException("A comments array is required.");
            var entries = new List<DocumentComment>(); var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in comments)
            {
                if (!(item is JObject comment)) throw new InvalidDataException("Comment entries must be objects.");
                var entry = new DocumentComment(comment, true);
                if (!identities.Add(CommentJson.Identity(entry.Id))) throw new InvalidDataException("A comment identity occurs more than once.");
                entries.Add(entry);
            }
            Comments = entries.AsReadOnly();
        }
        /// <summary>Gets the original immutable comments in server order.</summary>
        public IReadOnlyList<DocumentComment> Comments { get; }
        /// <summary>Gets the exact original list response JSON.</summary>
        public string RawJson { get; }
        /// <summary>Copies the complete original list response including future fields.</summary>
        /// <returns>A defensive copy of the original response.</returns>
        public JObject GetRawDetails() => (JObject)data.DeepClone();
    }
    internal static class CommentJson
    {
        internal static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
        internal static string Text(JObject data, string name, bool required)
        {
            var value = data[name];
            if (value == null || value.Type == JTokenType.Null) { if (required) throw new InvalidDataException("The " + name + " comment field is required."); return null; }
            if (value.Type != JTokenType.String) throw new InvalidDataException("The " + name + " field must remain an original string.");
            return value.Value<string>();
        }
        internal static T Parse<T>(string json, Func<JObject, T> parse)
        {
            try
            {
                using (var source = new StringReader(json ?? "")) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                { var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); if (reader.Read()) throw new InvalidDataException("Unexpected trailing response JSON."); return parse(data); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException || error is FormatException || error is OverflowException)
            { throw new InvalidResponseDataException("The comment or tag response has invalid metadata.", error); }
        }
    }
}
