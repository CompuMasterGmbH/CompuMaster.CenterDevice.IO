using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CenterDevice.Rest.Exceptions;

namespace CenterDevice.Rest.Clients.User
{
    /// <summary>Contains immutable user-list metadata without inferred visibility or privileges.</summary>
    public sealed class UserListingDetails
    {
        internal UserListingDetails(string id, string firstName, string lastName, string email, string status, string role, bool? technicalUser)
        { Id = id; FirstName = firstName; LastName = lastName; Email = email; Status = status; Role = role; TechnicalUser = technicalUser; }
        /// <summary>Gets the original user identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original first name, or null when absent or JSON null.</summary>
        public string FirstName { get; }
        /// <summary>Gets the original last name, or null when absent or JSON null.</summary>
        public string LastName { get; }
        /// <summary>Gets the original email address, or null when absent or JSON null.</summary>
        public string Email { get; }
        /// <summary>Gets the original status, retaining future values and null when absent.</summary>
        public string Status { get; }
        /// <summary>Gets the original role, retaining future values and null when absent.</summary>
        public string Role { get; }
        /// <summary>Gets the technical-user indication without replacing omitted information with false.</summary>
        /// <value>The explicit server value, or null when absent or JSON null.</value>
        /// <remarks>Technical users cannot receive document or collection shares. A missing indication does not establish sharing permission.</remarks>
        public bool? TechnicalUser { get; }
    }

    internal static class UserListingDetailsParser
    {
        internal static IReadOnlyList<UserListingDetails> Parse(string content)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the user list.");
                    if (!(root["users"] is JArray users)) throw new InvalidDataException("A user array is required.");
                    var result = new List<UserListingDetails>();
                    foreach (var token in users)
                    {
                        if (!(token is JObject data)) throw new InvalidDataException("A user must be an object.");
                        var id = Text(data["id"]);
                        if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("A user identifier is required.");
                        var technical = data["technical-user"];
                        if (technical != null && technical.Type != JTokenType.Null && technical.Type != JTokenType.Boolean)
                            throw new InvalidDataException("The technical-user indication must be a boolean or null.");
                        result.Add(new UserListingDetails(id, Text(data["first-name"]), Text(data["last-name"]), Text(data["email"]),
                            Text(data["status"]), Text(data["role"]), technical == null || technical.Type == JTokenType.Null ? (bool?)null : technical.Value<bool>()));
                    }
                    return result.AsReadOnly();
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The user list has invalid metadata or structure.", error); }
        }

        private static string Text(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.String) throw new InvalidDataException("User metadata must be a string or null.");
            return token.Value<string>();
        }
    }
}
