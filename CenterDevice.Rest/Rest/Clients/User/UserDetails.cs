using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CenterDevice.Rest.Clients.User
{
    /// <summary>Contains immutable selected user details without inferring missing fields or privileges.</summary>
    public sealed class UserDetails
    {
        private readonly JObject raw;
        internal UserDetails(JObject raw)
        {
            this.raw = (JObject)raw.DeepClone();
            Id = UserDetailsParser.Text(raw["id"]); FirstName = UserDetailsParser.Text(raw["first-name"]);
            LastName = UserDetailsParser.Text(raw["last-name"]); Email = UserDetailsParser.Text(raw["email"]);
            TenantName = UserDetailsParser.Text(raw["tenant-name"]); TenantId = UserDetailsParser.Text(raw["tenant-id"]);
            Role = UserDetailsParser.Text(raw["role"]); Status = UserDetailsParser.Text(raw["status"]);
            TechnicalUser = UserDetailsParser.Boolean(raw["technical-user"]);
            var picture = UserDetailsParser.Object(raw["picture"]); Picture = picture == null ? null : new UserPictureDetails(picture);
            Groups = UserDetailsParser.List(raw["groups"], value => new UserGroupDetails(UserDetailsParser.RequiredObject(value)));
            Collections = UserDetailsParser.List(raw["collections"], value => new UserCollectionDetails(UserDetailsParser.RequiredObject(value)));
        }
        /// <summary>Gets the original user identifier, or null when not provided.</summary>
        public string Id { get; }
        /// <summary>Gets the original first name, or null when not provided.</summary>
        public string FirstName { get; }
        /// <summary>Gets the original last name, or null when not provided.</summary>
        public string LastName { get; }
        /// <summary>Gets the original email address, or null when not provided.</summary>
        public string Email { get; }
        /// <summary>Gets the original tenant name returned as tenant-name, or null when not provided.</summary>
        public string TenantName { get; }
        /// <summary>Gets the original tenant identifier, or null when not provided.</summary>
        public string TenantId { get; }
        /// <summary>Gets the original role, retaining future values and null when not provided.</summary>
        public string Role { get; }
        /// <summary>Gets the original status, retaining future values and null when not provided.</summary>
        public string Status { get; }
        /// <summary>Gets the explicit technical-user indication, or null when not provided.</summary>
        /// <remarks>Technical users cannot receive document or collection shares. Omission does not establish sharing permission.</remarks>
        public bool? TechnicalUser { get; }
        /// <summary>Gets the original encoded picture metadata, or null when not provided.</summary>
        public UserPictureDetails Picture { get; }
        /// <summary>Gets immutable group metadata, or null when absent, hidden or JSON null.</summary>
        /// <remarks>The server exposes this field only for the current user. An explicit empty list is distinct from unknown information.</remarks>
        public IReadOnlyList<UserGroupDetails> Groups { get; }
        /// <summary>Gets immutable visible collection metadata, or null when absent, hidden or JSON null.</summary>
        /// <remarks>The server exposes this field only for the current user. Visibility does not imply ownership or write permission.</remarks>
        public IReadOnlyList<UserCollectionDetails> Collections { get; }
        /// <summary>Returns a defensive copy of the complete selected user metadata.</summary>
        /// <returns>Original fields, including future fields and nested metadata without conversion.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    /// <summary>Contains the original encoded profile-picture metadata.</summary>
    public sealed class UserPictureDetails
    {
        internal UserPictureDetails(JObject raw)
        { MimeType = UserDetailsParser.Text(raw["mimetype"]); Encoding = UserDetailsParser.Text(raw["encoding"]); Data = UserDetailsParser.Text(raw["data"]); }
        /// <summary>Gets the original MIME type, or null when not provided.</summary>
        public string MimeType { get; }
        /// <summary>Gets the original encoding, retaining future values and null when not provided.</summary>
        public string Encoding { get; }
        /// <summary>Gets the original encoded image without decoding or conversion, or null when not provided.</summary>
        public string Data { get; }
    }

    /// <summary>Contains immutable group membership metadata exposed in user details.</summary>
    public sealed class UserGroupDetails
    {
        internal UserGroupDetails(JObject raw)
        {
            Id = UserDetailsParser.RequiredId(raw["id"]); Name = UserDetailsParser.Text(raw["name"]);
            var color = raw["color"];
            if (color != null && color.Type != JTokenType.Null)
            {
                if (color.Type != JTokenType.Integer || !int.TryParse(color.ToString(Formatting.None), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    throw new InvalidDataException("A group color must be an Int32 code.");
                Color = value;
            }
        }
        /// <summary>Gets the original group identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original display name, or null when not provided.</summary>
        /// <remarks>The all-users group supplies only its identifier.</remarks>
        public string Name { get; }
        /// <summary>Gets the explicit display color code, or null when not provided.</summary>
        public int? Color { get; }
    }

    /// <summary>Contains immutable collection identity and sharing metadata exposed in user details.</summary>
    public sealed class UserCollectionDetails
    {
        private readonly JObject raw;
        internal UserCollectionDetails(JObject data)
        {
            raw = (JObject)data.DeepClone(); Id = UserDetailsParser.RequiredId(data["id"]); Name = UserDetailsParser.Text(data["name"]);
            Owner = UserDetailsParser.Text(data["owner"]); Public = UserDetailsParser.Boolean(data["public"]);
            Users = UserDetailsParser.List(data["users"], UserDetailsParser.RequiredId);
            Groups = UserDetailsParser.List(data["groups"], UserDetailsParser.RequiredId);
            UserDetailsParser.Object(data["filter-params"]);
        }
        /// <summary>Gets the original collection identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original collection name, or null when not provided.</summary>
        public string Name { get; }
        /// <summary>Gets the original owner identifier, or null when not provided.</summary>
        public string Owner { get; }
        /// <summary>Gets the explicit public flag, or null when not provided.</summary>
        public bool? Public { get; }
        /// <summary>Gets the original shared-user identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> Users { get; }
        /// <summary>Gets the original shared-group identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> Groups { get; }
        /// <summary>Returns a defensive copy of the complete collection metadata.</summary>
        /// <returns>Original collection fields, including intelligent filter parameters, extended metadata and future fields.</returns>
        /// <remarks>Provider-specific extended metadata and intelligent filters remain intact without guessing template fields or inferring permissions. Mutating this copy cannot change this result.</remarks>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    internal static class UserDetailsParser
    {
        internal static UserDetails Parse(string content, string selectedId)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the user details.");
                    var result = new UserDetails(root);
                    if (result.Id != null && (string.IsNullOrWhiteSpace(result.Id) || selectedId != "current" && Identity(result.Id) != Identity(selectedId)))
                        throw new InvalidDataException("The returned user identity differs from the selection.");
                    return result;
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The user details have invalid identity, metadata or structure.", error); }
        }
        private static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
        internal static string Text(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.String) throw new InvalidDataException("User metadata must be a string or null.");
            return token.Value<string>();
        }
        internal static string RequiredId(JToken token)
        { var result = Text(token); if (string.IsNullOrWhiteSpace(result)) throw new InvalidDataException("A resource identifier is required."); return result; }
        internal static bool? Boolean(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Boolean) throw new InvalidDataException("User metadata must be a boolean or null.");
            return token.Value<bool>();
        }
        internal static JObject Object(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return RequiredObject(token);
        }
        internal static JObject RequiredObject(JToken token)
        { if (!(token is JObject result)) throw new InvalidDataException("User metadata must be an object."); return result; }
        internal static IReadOnlyList<T> List<T>(JToken token, Func<JToken, T> convert)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (!(token is JArray entries)) throw new InvalidDataException("User metadata must be an array.");
            var result = new List<T>(); foreach (var entry in entries) result.Add(convert(entry)); return result.AsReadOnly();
        }
    }
}
