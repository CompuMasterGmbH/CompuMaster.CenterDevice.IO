using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace CenterDevice.Rest.Clients.Tenant
{
    /// <summary>Contains immutable selected tenant metadata without inferred subscription state.</summary>
    public sealed class TenantDetails
    {
        private readonly JObject raw;
        internal TenantDetails(JObject data)
        {
            raw = (JObject)data.DeepClone(); Id = TenantDetailsParser.Text(data["id"]); Name = TenantDetailsParser.Text(data["name"]);
            Status = TenantDetailsParser.Text(data["status"]); Cancelled = TenantDetailsParser.Boolean(data["cancelled"]);
            var logo = TenantDetailsParser.Object(data["logo"]); Logo = logo == null ? null : new TenantLogoDetails(logo);
        }
        /// <summary>Gets the original tenant identifier, or null when not provided.</summary>
        public string Id { get; }
        /// <summary>Gets the original display name, or null when not provided.</summary>
        public string Name { get; }
        /// <summary>Gets the original subscription status, retaining future values and null when not provided.</summary>
        public string Status { get; }
        /// <summary>Gets the explicit cancellation indication, or null when not provided.</summary>
        public bool? Cancelled { get; }
        /// <summary>Gets original encoded logo metadata, or null when not provided.</summary>
        public TenantLogoDetails Logo { get; }
        /// <summary>Returns a defensive copy of complete selected tenant metadata.</summary>
        /// <returns>The original fields, including future metadata.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    /// <summary>Contains immutable original encoded tenant logo metadata.</summary>
    public sealed class TenantLogoDetails
    {
        internal TenantLogoDetails(JObject data)
        { MimeType = TenantDetailsParser.Text(data["mimetype"]); Encoding = TenantDetailsParser.Text(data["encoding"]); Data = TenantDetailsParser.Text(data["data"]); }
        /// <summary>Gets the original MIME type, or null when not provided.</summary>
        public string MimeType { get; }
        /// <summary>Gets the original encoding, retaining future values and null when not provided.</summary>
        public string Encoding { get; }
        /// <summary>Gets the original encoded image without decoding or conversion, or null when not provided.</summary>
        public string Data { get; }
    }

    /// <summary>Contains immutable tenant selections associated with the authenticated login.</summary>
    public sealed class TenantLoginDetails
    {
        private readonly JObject raw;
        internal TenantLoginDetails(JObject data)
        {
            raw = (JObject)data.DeepClone(); DefaultTenantId = TenantDetailsParser.Text(data["default-tenant-id"]);
            var entries = data["tenants"];
            if (entries != null && entries.Type != JTokenType.Null)
            {
                if (!(entries is JArray list)) throw new InvalidDataException("Login tenants must be an array.");
                var result = new List<TenantLoginEntryDetails>();
                foreach (var value in list) result.Add(new TenantLoginEntryDetails(TenantDetailsParser.RequiredObject(value)));
                Tenants = result.AsReadOnly();
            }
        }
        /// <summary>Gets the original login default tenant identifier, or null when not provided.</summary>
        /// <remarks>This preference is not a client instance selection and does not switch the current OAuth token.</remarks>
        public string DefaultTenantId { get; }
        /// <summary>Gets tenant entries in server order, or null when not provided.</summary>
        /// <value>An immutable list, including explicit empty lists; missing information remains unknown.</value>
        public IReadOnlyList<TenantLoginEntryDetails> Tenants { get; }
        /// <summary>Returns a defensive copy of complete selected login tenant metadata.</summary>
        /// <returns>Original entries, default preference and future fields.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    /// <summary>Contains immutable original tenant and associated user metadata for a login.</summary>
    public sealed class TenantLoginEntryDetails
    {
        private readonly JObject raw;
        internal TenantLoginEntryDetails(JObject data)
        {
            raw = (JObject)data.DeepClone(); Id = TenantDetailsParser.RequiredId(data["id"]); Name = TenantDetailsParser.Text(data["name"]);
            Status = TenantDetailsParser.Text(data["status"]); Expired = TenantDetailsParser.Boolean(data["expired"]); Cancelled = TenantDetailsParser.Boolean(data["cancelled"]);
            var user = TenantDetailsParser.Object(data["user"]); User = user == null ? null : new TenantLoginUserDetails(user);
        }
        /// <summary>Gets the original tenant identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original tenant name, or null when not provided.</summary>
        public string Name { get; }
        /// <summary>Gets the original subscription status, retaining future values and null when not provided.</summary>
        public string Status { get; }
        /// <summary>Gets the explicit subscription-expiry indication, or null when not provided.</summary>
        public bool? Expired { get; }
        /// <summary>Gets the explicit cancellation indication, or null when not provided.</summary>
        public bool? Cancelled { get; }
        /// <summary>Gets original associated-user metadata, or null when not provided.</summary>
        public TenantLoginUserDetails User { get; }
        /// <summary>Returns a defensive copy of complete tenant-entry metadata.</summary>
        /// <returns>Original tenant/user fields, including future metadata.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    /// <summary>Contains immutable original user metadata for an associated tenant.</summary>
    public sealed class TenantLoginUserDetails
    {
        internal TenantLoginUserDetails(JObject data)
        {
            Id = TenantDetailsParser.RequiredId(data["id"]); Email = TenantDetailsParser.Text(data["email"]);
            FirstName = TenantDetailsParser.Text(data["first-name"]); LastName = TenantDetailsParser.Text(data["last-name"]);
            Role = TenantDetailsParser.Text(data["role"]); Status = TenantDetailsParser.Text(data["status"]);
        }
        /// <summary>Gets the original associated user identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original email address, or null when not provided.</summary>
        public string Email { get; }
        /// <summary>Gets the original first name, or null when not provided.</summary>
        public string FirstName { get; }
        /// <summary>Gets the original last name, or null when not provided.</summary>
        public string LastName { get; }
        /// <summary>Gets the original role, retaining future values and null when not provided.</summary>
        public string Role { get; }
        /// <summary>Gets the original status, retaining future values and null when not provided.</summary>
        public string Status { get; }
    }

    /// <summary>Contains immutable caller-visible tenant feature statuses.</summary>
    public sealed class TenantFeaturesDetails
    {
        internal TenantFeaturesDetails(JObject data)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in data.Properties()) result.Add(field.Name, TenantDetailsParser.Text(field.Value));
            Statuses = new ReadOnlyDictionary<string, string>(result);
        }
        /// <summary>Gets original feature status strings, retaining future names/values and explicit null.</summary>
        /// <remarks>Administrators can see available features; other users see not-available for an available but inactive feature. Caller visibility does not establish global tenant availability or activation rights.</remarks>
        public IReadOnlyDictionary<string, string> Statuses { get; }
        /// <summary>Gets the original archiving status, or null when absent or JSON null.</summary>
        public string ArchivingStatus => Statuses.TryGetValue("archiving", out var value) ? value : null;
        /// <summary>Gets the original auditing status, or null when absent or JSON null.</summary>
        public string AuditingStatus => Statuses.TryGetValue("auditing", out var value) ? value : null;
    }

    internal static class TenantDetailsParser
    {
        internal static T Parse<T>(string content, Func<JObject, T> convert)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the tenant metadata.");
                    return convert(root);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The tenant metadata has invalid identity or structure.", error); }
        }
        internal static void RequireId(string id)
        { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A selected tenant identifier is required.", nameof(id)); }
        internal static string Identity(string id) => Guid.TryParse(id, out var value) ? value.ToString("D") : id;
        internal static string Text(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.String) throw new InvalidDataException("Tenant metadata must be a string or null.");
            return token.Value<string>();
        }
        internal static string RequiredId(JToken token)
        { var value = Text(token); if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("A resource identifier is required."); return value; }
        internal static bool? Boolean(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.Boolean) throw new InvalidDataException("Tenant metadata must be a boolean or null.");
            return token.Value<bool>();
        }
        internal static JObject Object(JToken token) => token == null || token.Type == JTokenType.Null ? null : RequiredObject(token);
        internal static JObject RequiredObject(JToken token)
        { if (!(token is JObject result)) throw new InvalidDataException("Tenant metadata must be an object."); return result; }
    }
}
