using Newtonsoft.Json.Linq;
using System;
using System.Text.RegularExpressions;

namespace CenterDevice.Rest.Clients.Collections
{
    /// <summary>Specifies an explicit archived-document expiry period.</summary>
    public sealed class CollectionArchiveExpiryPeriod : JObject
    {
        /// <summary>Initializes an empty period without defaults.</summary>
        public CollectionArchiveExpiryPeriod() { }
        /// <summary>Initializes a period by copying complete JSON.</summary>
        /// <param name="other">Original period fields, including future options.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public CollectionArchiveExpiryPeriod(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original period unit, or null to omit it.</summary>
        /// <remarks>The documented units are days, weeks, months and years; the server validates catalogs.</remarks>
        public string Unit { get { return Value<string>("unit"); } set { if (value == null) Remove("unit"); else this["unit"] = value; } }
        /// <summary>Gets or sets the explicit positive Int64 period amount, or null to omit it.</summary>
        /// <remarks>Raw JSON can preserve larger positive integer amounts for server validation; no manufacturer width is inferred.</remarks>
        public long? Amount { get { return Value<long?>("amount"); } set { if (value.HasValue) this["amount"] = value.Value; else Remove("amount"); } }
        /// <summary>Gets or sets the explicit calendar-unit choice, or null to retain the server default of false.</summary>
        public bool? Calendar { get { return Value<bool?>("calendar"); } set { if (value.HasValue) this["calendar"] = value.Value; else Remove("calendar"); } }
    }

    /// <summary>Specifies an explicit date or period and action for an archived collection.</summary>
    /// <remarks>The server computes expiry in Europe/Berlin. Original ISO8601 strings are not converted locally. Expiry can remove references, move documents to trash or permanently delete them; the action is always an explicit caller selection.</remarks>
    public sealed class CollectionArchiveExpiryRequest : JObject
    {
        /// <summary>Initializes an empty request without an expiry or action default.</summary>
        public CollectionArchiveExpiryRequest() { }
        /// <summary>Initializes a request by copying complete JSON.</summary>
        /// <param name="other">Original expiry fields, including future options.</param>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        public CollectionArchiveExpiryRequest(JObject other) : base(other ?? throw new ArgumentNullException(nameof(other))) { }
        /// <summary>Gets or sets the original ISO8601 expiry date, or null to omit it.</summary>
        public string Date { get { return Value<string>("date"); } set { if (value == null) Remove("date"); else this["date"] = value; } }
        /// <summary>Gets or sets a defensive copy of the complete expiry period, or null to omit it.</summary>
        public CollectionArchiveExpiryPeriod Period { get { return this["period"] == null || this["period"].Type == JTokenType.Null ? null : new CollectionArchiveExpiryPeriod((JObject)this["period"]); } set { if (value == null) Remove("period"); else this["period"] = value.DeepClone(); } }
        /// <summary>Gets or sets the original explicit expiry action, or null to omit it.</summary>
        /// <remarks>The documented actions are no-action, remove-from-collection, move-to-trash and delete. Delete permanently deletes expired documents. Future catalogs remain server-authoritative.</remarks>
        public string Action { get { return Value<string>("action"); } set { if (value == null) Remove("action"); else this["action"] = value; } }
        internal JObject SnapshotForSend()
        {
            var snapshot = (JObject)DeepClone();
            if ((snapshot.Property("date") != null) == (snapshot.Property("period") != null)) throw new ArgumentException("Exactly one explicit expiry date or period is required; use RemoveArchiveExpiry to unset the rule.");
            RequireText(snapshot["action"], "action");
            if (snapshot.Property("date") != null) RequireText(snapshot["date"], "date");
            else
            {
                if (!(snapshot["period"] is JObject period)) throw new ArgumentException("A complete expiry period is required.");
                RequireText(period["unit"], "unit"); var amount = period["amount"];
                if (amount == null || amount.Type != JTokenType.Integer || !Regex.IsMatch(amount.ToString(Newtonsoft.Json.Formatting.None), "^[1-9][0-9]*$")) throw new ArgumentException("The period amount must be a positive JSON integer.");
                if (period["calendar"] != null && period["calendar"].Type != JTokenType.Boolean) throw new ArgumentException("A supplied calendar choice must be a boolean.");
            }
            return snapshot;
        }
        private static void RequireText(JToken token, string name)
        { if (token?.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>())) throw new ArgumentException("An explicit nonblank " + name + " string is required."); }
    }
}
