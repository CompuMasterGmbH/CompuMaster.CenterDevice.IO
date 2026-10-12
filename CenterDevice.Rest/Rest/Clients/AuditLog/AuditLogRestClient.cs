using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.AuditLog
{
    /// <summary>Retrieves collection audit logs without changing collection configuration.</summary>
    /// <remarks>External/internal users and admins may search visible collections. Scrolling requires an audited collection; additional view/download events are visible only to its owner. Empty results do not establish availability or privilege.</remarks>
    public class AuditLogRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes an audit-log client using the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The authorization information provider.</param>
        /// <param name="configuration">The REST origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public AuditLogRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Searches a visible collection's audit log.</summary>
        /// <inheritdoc cref="SearchAsync"/>
        public IReadOnlyList<AuditLogEntry> Search(string userId, string collectionId, AuditLogSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => SearchAsync(userId, collectionId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Searches a visible collection's audit log using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The nonblank selected collection identifier.</param>
        /// <param name="request">The nonnull filters and page options, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only page in decreasing server timestamp order, without an inferred total or next-page flag.</returns>
        /// <remarks>Auditing is not required for ordinary collection events. View/download events require auditing and owner visibility. Existing token refresh is preserved; uncertain POST outcomes are not replayed.</remarks>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentException">The selected collection or an option is invalid.</exception>
        /// <exception cref="InvalidResponseDataException">The successful response has invalid entries, identities, metadata or page structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<IReadOnlyList<AuditLogEntry>> SearchAsync(string userId, string collectionId, AuditLogSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => ExecutePageAsync(userId, collectionId, "search", request, cancellationToken);

        /// <summary>Retrieves the next logical page of an audited collection's audit log.</summary>
        /// <inheritdoc cref="ScrollAsync"/>
        public IReadOnlyList<AuditLogEntry> Scroll(string userId, string collectionId, AuditLogScrollRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => ScrollAsync(userId, collectionId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the next logical page using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="SearchAsync"/>
        /// <returns>A read-only page in logical event order, excluding the specified previous event.</returns>
        /// <remarks>Requires an audited visible collection. Missing PreviousId starts a new scroll; pass the last returned ID unchanged for the next page. With explicit Rows, compare page length to the effective size Math.Min(Rows, 1000); a smaller page ends the scroll. Omitted Rows uses an unknown server default, so no termination threshold is inferred. No timestamp sorting, cursor fallback, automatic iteration or inferred auditing is performed. Unknown cursor errors remain server errors; existing token refresh is preserved.</remarks>
        public Task<IReadOnlyList<AuditLogEntry>> ScrollAsync(string userId, string collectionId, AuditLogScrollRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => ExecutePageAsync(userId, collectionId, "scroll", request, cancellationToken);

        private async Task<IReadOnlyList<AuditLogEntry>> ExecutePageAsync(string userId, string collectionId, string action, object options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (options == null) throw new ArgumentNullException("request");
            if (string.IsNullOrWhiteSpace(collectionId)) throw new ArgumentException("A selected collection is required.", nameof(collectionId));
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var snapshot = JObject.FromObject(options, serializer);
            ValidateOptions(snapshot, action == "scroll");
            var rows = Math.Min(snapshot.Value<int?>("rows") ?? 1000, 1000);
            var previous = snapshot.Value<string>("previous-id");
            var request = CreateRestRequest(ApiVersionPrefix + "collection/" + Uri.EscapeDataString(collectionId) + "/auditlog", Method.Post);
            request.AddStringBody(new JObject { ["action"] = action, ["params"] = snapshot }.ToString(Formatting.None), RestSharp.ContentType.Json);
            token.ThrowIfCancellationRequested();
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                JObject data;
                using (var text = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { data = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the audit page."); }
                var entries = data["entries"] as JArray;
                if (entries == null || entries.Count > rows) throw new InvalidDataException("A complete bounded audit entry array is required.");
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var result = new List<AuditLogEntry>();
                foreach (var entry in entries)
                {
                    if (entry.Type != JTokenType.Object) throw new InvalidDataException("An audit entry must be an object.");
                    var id = OptionalText(entry["id"]);
                    var type = OptionalText(entry["type"]);
                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) || !seen.Add(Identity(id)) ||
                        (previous != null && Identity(id) == Identity(previous))) throw new InvalidDataException("An audit entry has no usable distinct exclusive identity/type.");
                    var timestampText = OptionalText(entry["timestamp"]);
                    DateTimeOffset? timestamp = null;
                    if (timestampText != null)
                    {
                        if (!Regex.IsMatch(timestampText, @"^\d{4}-\d{2}-\d{2}(?:T|$)") ||
                            !DateTimeOffset.TryParse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                            throw new InvalidDataException("An audit timestamp is not usable ISO8601 data.");
                        if (Regex.IsMatch(timestampText, @"(?:[Zz]|[+-]\d{2}:?\d{2})$")) timestamp = parsed;
                    }
                    var detailToken = entry["details"];
                    if (detailToken != null && detailToken.Type != JTokenType.Null && detailToken.Type != JTokenType.Object)
                        throw new InvalidDataException("Audit details must be an object.");
                    var details = detailToken as JObject;
                    AuditDocumentAccessDetails access = null;
                    if ((type == AuditLogEntry.DocumentDownloaded || type == AuditLogEntry.DocumentViewed) && details != null)
                    {
                        var documentId = OptionalText(details["document-id"]);
                        var filename = OptionalText(details["filename"]);
                        var versionToken = details["version"];
                        long? version = null;
                        if (versionToken != null && versionToken.Type != JTokenType.Null)
                        {
                            if (versionToken.Type != JTokenType.Integer) throw new InvalidDataException("Accessed versions must be integers.");
                            version = versionToken.ToObject<long>();
                            if (version <= 0) throw new InvalidDataException("Accessed versions must be positive.");
                        }
                        access = new AuditDocumentAccessDetails(documentId, version, filename);
                    }
                    result.Add(new AuditLogEntry(id, type, timestampText, timestamp, OptionalText(entry["creator"]), details, access));
                }
                return result.AsReadOnly();
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is OverflowException || error is InvalidCastException || error is FormatException)
            { throw new InvalidResponseDataException("The collection audit log returned an invalid page.", error); }
        }

        private static string Identity(string value) => Guid.TryParse(value, out var id) ? id.ToString("D") : value;
        private static string OptionalText(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.String) throw new InvalidDataException("Textual audit metadata must be a string.");
            return value.Value<string>();
        }

        private static void ValidateOptions(JObject options, bool scroll)
        {
            if (options.Value<int?>("rows") <= 0 ||
                (!scroll && (options.Value<int?>("offset") < 0 || options.Value<int?>("offset") > 5000)))
                throw new ArgumentException("Audit page size must be positive and search offset 0–5000.", "request");
            foreach (var filter in new[] { "types", "users", "document-ids" })
            {
                var values = options[filter] as JArray;
                if (values == null) continue;
                if (values.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>())))
                    throw new ArgumentException("Audit filter values must be usable strings.", "request");
                var distinct = values.Values<string>().Select(value => filter == "types" ? value : Identity(value)).Distinct(StringComparer.Ordinal).Count();
                if (distinct > (filter == "types" ? 20 : 10)) throw new ArgumentException("The audit filter exceeds its distinct-value limit.", "request");
            }
            var range = options["timestamp"];
            if (range != null && range.Value<DateTimeOffset?>("from") > range.Value<DateTimeOffset?>("to"))
                throw new ArgumentException("Audit timestamp bounds must be ordered.", "request");
            if (scroll && ((options["previous-id"] != null && string.IsNullOrWhiteSpace(options.Value<string>("previous-id"))) ||
                (options["sort"] != null && options.Value<string>("sort") != "asc" && options.Value<string>("sort") != "desc")))
                throw new ArgumentException("A supplied cursor must be usable and sort must be asc or desc.", "request");
        }
    }
}
