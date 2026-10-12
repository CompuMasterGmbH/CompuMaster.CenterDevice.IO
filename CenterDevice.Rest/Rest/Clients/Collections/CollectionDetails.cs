using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CenterDevice.Rest.Clients.Collections
{
    /// <summary>Contains immutable collection details without inferring missing metadata or permissions.</summary>
    public sealed class CollectionDetails
    {
        private readonly JObject raw;
        internal CollectionDetails(JObject data)
        {
            raw = (JObject)data.DeepClone();
            Id = CollectionDetailsParser.Text(data["id"]); Name = CollectionDetailsParser.Text(data["name"]);
            Owner = CollectionDetailsParser.Text(data["owner"]); Link = CollectionDetailsParser.Text(data["link"]);
            Unique = CollectionDetailsParser.Boolean(data["unique"]); Public = CollectionDetailsParser.Boolean(data["public"]);
            InheritedSharings = CollectionDetailsParser.Boolean(data["inherited-sharings"]); FullPath = CollectionDetailsParser.Boolean(data["full-path"]);
            Auditing = CollectionDetailsParser.Boolean(data["auditing"]); NeedToOptIn = CollectionDetailsParser.Boolean(data["need-to-opt-in"]);
            HasFolders = CollectionDetailsParser.Boolean(data["has-folders"]);
            ArchivedDate = CollectionDetailsParser.Text(data["archived-date"]); ExpiryDate = CollectionDetailsParser.Text(data["expiry-date"]);
            var users = CollectionDetailsParser.Object(data["users"]); Users = users == null ? null : new CollectionSharingDetails(users);
            var groups = CollectionDetailsParser.Object(data["groups"]); Groups = groups == null ? null : new CollectionSharingDetails(groups);
            var auditing = CollectionDetailsParser.Object(data["auditing-users"]); AuditingUsers = auditing == null ? null : new CollectionAuditingUsersDetails(auditing);
            var email = CollectionDetailsParser.Object(data["auditing-email-settings"]); AuditingEmailSettings = email == null ? null : new CollectionAuditingEmailDetails(email);
            var period = CollectionDetailsParser.Object(data["expiry-period"]); ExpiryPeriod = period == null ? null : new CollectionExpiryPeriodDetails(period);
            CollectionDetailsParser.Object(data["filter-params"]);
            var metadata = CollectionDetailsParser.Object(data["extended-metadata"]);
            if (metadata != null) foreach (var property in metadata.Properties())
            {
                if (property.Value.Type == JTokenType.String) continue;
                if (!(property.Value is JArray values)) throw new InvalidDataException("Extended metadata values must be strings or string arrays.");
                foreach (var value in values) if (value.Type != JTokenType.String) throw new InvalidDataException("Extended metadata arrays must contain strings.");
            }
        }
        /// <summary>Gets the original identifier, or null when excluded by the field selection.</summary>
        public string Id { get; }
        /// <summary>Gets the original name, or null when not provided.</summary>
        public string Name { get; }
        /// <summary>Gets the original owner identifier or hidden-owner placeholder, or null when not provided.</summary>
        /// <remarks>A placeholder does not identify a visible user or grant ownership.</remarks>
        public string Owner { get; }
        /// <summary>Gets the original link identifier, or null when not provided.</summary>
        public string Link { get; }
        /// <summary>Gets the explicit unique-name flag, or null when unknown.</summary>
        public bool? Unique { get; }
        /// <summary>Gets the explicit public flag, or null when unknown.</summary>
        public bool? Public { get; }
        /// <summary>Gets the explicit inherited-sharing disclosure flag, or null when unknown.</summary>
        public bool? InheritedSharings { get; }
        /// <summary>Gets the explicit full-path disclosure flag, or null when unknown.</summary>
        public bool? FullPath { get; }
        /// <summary>Gets the explicit auditing flag, or null when unknown.</summary>
        public bool? Auditing { get; }
        /// <summary>Gets whether the caller explicitly needs to opt in, or null when not provided.</summary>
        /// <remarks>Omission does not establish access, opt-in status or absence of auditing.</remarks>
        public bool? NeedToOptIn { get; }
        /// <summary>Gets the explicit child-folder indication, preserving unknown and known false.</summary>
        public bool? HasFolders { get; }
        /// <summary>Gets the original archived-date string without timezone conversion, or null when not provided.</summary>
        public string ArchivedDate { get; }
        /// <summary>Gets the original expiry-date string without timezone conversion, or null when not provided.</summary>
        public string ExpiryDate { get; }
        /// <summary>Gets the visible user sharing metadata, or null when not provided.</summary>
        public CollectionSharingDetails Users { get; }
        /// <summary>Gets the visible group sharing metadata, or null when not provided.</summary>
        public CollectionSharingDetails Groups { get; }
        /// <summary>Gets owner-only auditing state metadata, or null when not requested or exposed.</summary>
        public CollectionAuditingUsersDetails AuditingUsers { get; }
        /// <summary>Gets owner-only auditing email metadata, or null when not requested or exposed.</summary>
        public CollectionAuditingEmailDetails AuditingEmailSettings { get; }
        /// <summary>Gets original expiry-period metadata, or null when not provided.</summary>
        public CollectionExpiryPeriodDetails ExpiryPeriod { get; }
        /// <summary>Returns a defensive copy of the original intelligent-collection filter parameters.</summary>
        /// <returns>Complete original parameters including future fields, or null when not provided.</returns>
        public JObject GetFilterParameters() => (JObject)CollectionDetailsParser.Object(raw["filter-params"])?.DeepClone();
        /// <summary>Returns a defensive copy of the original collection extended metadata.</summary>
        /// <returns>String and string-array values, including explicit empty arrays, or null when not provided.</returns>
        public JObject GetExtendedMetadata() => (JObject)CollectionDetailsParser.Object(raw["extended-metadata"])?.DeepClone();
        /// <summary>Returns a defensive copy of all original collection details.</summary>
        /// <returns>All supplied fields, including unknown fields and explicit JSON null.</returns>
        public JObject GetRawDetails() => (JObject)raw.DeepClone();
    }

    /// <summary>Contains immutable visible sharing identities and the server's hidden-identity count.</summary>
    public sealed class CollectionSharingDetails
    {
        internal CollectionSharingDetails(JObject data)
        { Visible = CollectionDetailsParser.Identifiers(data["visible"]); NotVisibleCount = CollectionDetailsParser.NonnegativeInteger(data["not-visible-count"]); }
        /// <summary>Gets original visible identifiers in server order, or null when not provided.</summary>
        public IReadOnlyList<string> Visible { get; }
        /// <summary>Gets the original nonnegative hidden-identity count without inventing an integer width, or null when unknown.</summary>
        public string NotVisibleCount { get; }
    }

    /// <summary>Contains immutable owner-visible auditing response categories.</summary>
    public sealed class CollectionAuditingUsersDetails
    {
        internal CollectionAuditingUsersDetails(JObject data)
        { OptedIn = CollectionDetailsParser.Identifiers(data["opted-in"]); OptedOut = CollectionDetailsParser.Identifiers(data["opted-out"]); Pending = CollectionDetailsParser.Identifiers(data["pending"]); }
        /// <summary>Gets original opted-in user identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> OptedIn { get; }
        /// <summary>Gets original opted-out user identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> OptedOut { get; }
        /// <summary>Gets original pending user identifiers, or null when not provided.</summary>
        public IReadOnlyList<string> Pending { get; }
    }

    /// <summary>Contains immutable owner-visible auditing email settings.</summary>
    public sealed class CollectionAuditingEmailDetails
    {
        internal CollectionAuditingEmailDetails(JObject data)
        { UserOptedIn = CollectionDetailsParser.Boolean(data["user-opted-in"]); UserOptedOut = CollectionDetailsParser.Boolean(data["user-opted-out"]); PendingResponses = CollectionDetailsParser.Text(data["pending-responses"]); }
        /// <summary>Gets the explicit opted-in notification flag, or null when unknown.</summary>
        public bool? UserOptedIn { get; }
        /// <summary>Gets the explicit opted-out notification flag, or null when unknown.</summary>
        public bool? UserOptedOut { get; }
        /// <summary>Gets the original pending-response period, retaining future values and null when unknown.</summary>
        public string PendingResponses { get; }
    }

    /// <summary>Contains immutable archive expiry-period metadata without date arithmetic.</summary>
    public sealed class CollectionExpiryPeriodDetails
    {
        internal CollectionExpiryPeriodDetails(JObject data)
        { Unit = CollectionDetailsParser.Text(data["unit"]); Amount = CollectionDetailsParser.NonnegativeInteger(data["amount"]); Calendar = CollectionDetailsParser.Boolean(data["calendar"]); }
        /// <summary>Gets the original period unit, retaining future values and null when unknown.</summary>
        public string Unit { get; }
        /// <summary>Gets the original integer amount without narrowing its width, or null when unknown.</summary>
        public string Amount { get; }
        /// <summary>Gets the explicit calendar flag, or null when not provided.</summary>
        public bool? Calendar { get; }
    }

    internal static class CollectionDetailsParser
    {
        internal static CollectionDetails Parse(string content, string selectedId)
        {
            try
            {
                using (var source = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the collection details.");
                    var result = new CollectionDetails(data);
                    if (result.Id != null && (string.IsNullOrWhiteSpace(result.Id) || Identity(result.Id) != Identity(selectedId)))
                        throw new InvalidDataException("The returned collection identity differs from the selection.");
                    return result;
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The collection details have invalid identity, metadata or structure.", error); }
        }
        private static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
        internal static string Text(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (value.Type != JTokenType.String) throw new InvalidDataException("Collection metadata must be a string or null."); return value.Value<string>(); }
        internal static bool? Boolean(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (value.Type != JTokenType.Boolean) throw new InvalidDataException("Collection metadata must be a boolean or null."); return value.Value<bool>(); }
        internal static JObject Object(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (!(value is JObject result)) throw new InvalidDataException("Collection metadata must be an object or null."); return result; }
        internal static string NonnegativeInteger(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (value.Type != JTokenType.Integer || !System.Text.RegularExpressions.Regex.IsMatch(value.ToString(Formatting.None), @"^(0|[1-9][0-9]*)$")) throw new InvalidDataException("Collection metadata must be a nonnegative integer."); return value.ToString(Formatting.None); }
        internal static IReadOnlyList<string> Identifiers(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return null;
            if (!(value is JArray entries)) throw new InvalidDataException("Collection identities must be an array.");
            var result = new List<string>(); foreach (var entry in entries) { var id = Text(entry); if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("A visible identifier must be nonblank."); result.Add(id); }
            return result.AsReadOnly();
        }
    }
}
