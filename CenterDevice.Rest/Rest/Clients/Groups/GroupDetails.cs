using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CenterDevice.Rest.Clients.Groups
{
    /// <summary>Contains immutable original group administrator metadata.</summary>
    public sealed class GroupAdministratorDetails
    {
        internal GroupAdministratorDetails(string userId, string privilege) { UserId = userId; Privilege = privilege; }
        /// <summary>Gets the original administrator user identifier.</summary>
        public string UserId { get; }
        /// <summary>Gets the original privilege, retaining future values.</summary>
        public string Privilege { get; }
    }

    /// <summary>Contains immutable group detail without inferring hidden administrators or membership rights.</summary>
    /// <remarks>Public groups are visible to internal users, tenant administrators and members; private groups are visible to their administrators and members. Group administration does not itself grant shared-document visibility.</remarks>
    public sealed class GroupDetails
    {
        internal GroupDetails(string id, string name, int? color, string type, IReadOnlyList<string> users, IReadOnlyList<GroupAdministratorDetails> admins)
        { Id = id; Name = name; Color = color; Type = type; Users = users; Admins = admins; }
        /// <summary>Gets the original group identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original display name.</summary>
        public string Name { get; }
        /// <summary>Gets the original display color code.</summary>
        /// <value>The code, or null when absent or JSON null; zero is not inferred.</value>
        public int? Color { get; }
        /// <summary>Gets the original public/private or future group type.</summary>
        /// <value>The server string, or null when absent or JSON null; public is not inferred.</value>
        public string Type { get; }
        /// <summary>Gets immutable original member user identifiers.</summary>
        /// <value>The ordered list, an explicit empty list, or null when absent or JSON null.</value>
        public IReadOnlyList<string> Users { get; }
        /// <summary>Gets immutable original group administrators when exposed.</summary>
        /// <value>The ordered list, an explicit empty list, or null when hidden/absent/JSON null.</value>
        /// <remarks>For private groups, these details are exposed to group administrators and internal/tenant-administrator members. Hidden details do not mean no administrators and do not establish caller privileges.</remarks>
        public IReadOnlyList<GroupAdministratorDetails> Admins { get; }
    }

    internal static class GroupDetailsParser
    {
        internal static void RequireId(string id)
        { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A usable group identifier is required."); }
        internal static string Identity(string id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : id;
        internal static T Parse<T>(string text, Func<JObject, T> convert)
        {
            try
            {
                using (var source = new StringReader(text ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                { var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the group response."); return convert(root); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException || error is OverflowException)
            { throw new InvalidResponseDataException("The group response has invalid identity, detail or list structure.", error); }
        }
        internal static GroupDetails Record(JObject data)
        {
            var id = Text(data["id"], true); if (id == "." || id == "..") throw new InvalidDataException("A usable group identity is required.");
            int? color = null; var colorData = data["color"];
            if (colorData != null && colorData.Type != JTokenType.Null)
            {
                if (colorData.Type != JTokenType.Integer || !int.TryParse(colorData.ToString(), out var value)) throw new InvalidDataException("A group color must be an Int32 code."); color = value;
            }
            var typeData = data["type"]; var type = typeData == null || typeData.Type == JTokenType.Null ? null : Text(typeData, true);
            List<string> users = null; var userData = data["users"];
            if (userData != null && userData.Type != JTokenType.Null)
            {
                if (!(userData is JArray entries)) throw new InvalidDataException("Group members must be an array."); users = new List<string>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var user in entries) { var userId = Text(user, true); if (!ids.Add(Identity(userId))) throw new InvalidDataException("Repeated group member identity."); users.Add(userId); }
            }
            List<GroupAdministratorDetails> admins = null; var adminData = data["admins"];
            if (adminData != null && adminData.Type != JTokenType.Null)
            {
                if (!(adminData is JArray entries)) throw new InvalidDataException("Exposed group administrators must be an array."); admins = new List<GroupAdministratorDetails>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    if (!(entry is JObject admin)) throw new InvalidDataException("A group administrator must be an object.");
                    var adminId = Text(admin["user-id"], true); if (!ids.Add(Identity(adminId))) throw new InvalidDataException("Repeated group administrator identity."); admins.Add(new GroupAdministratorDetails(adminId, Text(admin["privilege"], true)));
                }
            }
            return new GroupDetails(id, Text(data["name"], false), color, type, users?.AsReadOnly(), admins?.AsReadOnly());
        }
        private static string Text(JToken value, bool nonblank)
        { if (value?.Type != JTokenType.String || nonblank && string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Group detail must be usable text."); return value.Value<string>(); }
    }
}
