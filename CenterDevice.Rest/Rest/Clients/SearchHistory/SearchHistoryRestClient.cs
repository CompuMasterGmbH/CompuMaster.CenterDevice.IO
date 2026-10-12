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

namespace CenterDevice.Rest.Clients.SearchHistory
{
    /// <summary>Retrieves and explicitly records the authenticated caller's search-term history.</summary>
    /// <remarks>History can be disabled by the user's existing setting. An empty list does not establish the setting's state; writes retain the original disabled-history error. Search itself does not automatically record usage.</remarks>
    public class SearchHistoryRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a search-history client with the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The authorization information provider.</param>
        /// <param name="configuration">The REST origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public SearchHistoryRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Retrieves the caller's bounded search history.</summary>
        /// <inheritdoc cref="GetHistoryAsync"/>
        public IReadOnlyList<SearchHistoryEntry> GetHistory(string userId, SearchHistorySort? sort = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetHistoryAsync(userId, sort, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the caller's search history using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="sort">The optional server sort, or null for the server's unspecified order.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only page retaining server order, original terms, counts and dates.</returns>
        /// <remarks>At most the first 500 entries for the selected sort are returned, without a paging or total contract. It is not a complete history. No sorting or access inference is performed locally; disabled history also returns an empty page.</remarks>
        public async Task<IReadOnlyList<SearchHistoryEntry>> GetHistoryAsync(string userId, SearchHistorySort? sort = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(ApiVersionPrefix + "search-history", Method.Get);
            if (sort.HasValue)
            {
                if (sort != SearchHistorySort.MostRecentFirst && sort != SearchHistorySort.MostUsedFirst) throw new ArgumentOutOfRangeException(nameof(sort));
                request.AddQueryParameter("sort", sort == SearchHistorySort.MostRecentFirst ? "most-recent-first" : "most-used-first");
            }
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return Parse<IReadOnlyList<SearchHistoryEntry>>(response.Content, data =>
            {
                var entries = data["history"] as JArray;
                if (entries == null || entries.Count > 500) throw new InvalidDataException("A bounded search-history array is required.");
                var result = new List<SearchHistoryEntry>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in entries)
                {
                    if (!(item is JObject entry)) throw new InvalidDataException("Search-history entries must be objects.");
                    var parsed = ParseEntry(entry);
                    if (!ids.Add(Identity(parsed.Id))) throw new InvalidDataException("The search-history page repeats an identity.");
                    result.Add(parsed);
                }
                return result.AsReadOnly();
            });
        }

        /// <summary>Records one explicit usage of a search term.</summary>
        /// <inheritdoc cref="AddTermAsync"/>
        public SearchHistoryEntry AddTerm(string userId, string term, CancellationToken cancellationToken = default(CancellationToken))
            => AddTermAsync(userId, term, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Records one explicit usage using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="term">The nonnull original term text; empty text and whitespace are sent unchanged for server validation.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The server's resulting identity, stored term, updated count and timestamp.</returns>
        /// <remarks>The server matches existing terms ignoring case, increments the count and updates the timestamp. This operation is non-idempotent, does not run a document search and is not replayed after an uncertain outcome. A disabled-history error remains an error.</remarks>
        public async Task<SearchHistoryEntry> AddTermAsync(string userId, string term, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (term == null) throw new ArgumentNullException(nameof(term));
            var response = await SendCommandAsync(userId, "add", new JObject { ["term"] = term }, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, data =>
            {
                var entry = ParseEntry(data);
                if (!string.Equals(entry.Term, term, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The recorded term differs from the selection.");
                return entry;
            });
        }

        /// <summary>Removes exactly the selected search-history entries.</summary>
        /// <inheritdoc cref="RemoveEntriesAsync"/>
        public void RemoveEntries(string userId, IEnumerable<string> entryIds, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveEntriesAsync(userId, entryIds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes selected entries using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="entryIds">A nonempty sequence of usable owned entry IDs, snapshotted once before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed selected removal, including entries already absent.</returns>
        /// <remarks>It always sends an ID array, never an inferred all-selection. The server enforces creator visibility. Disabled-history and other errors remain errors; uncertain writes are not replayed.</remarks>
        public Task RemoveEntriesAsync(string userId, IEnumerable<string> entryIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entryIds == null) throw new ArgumentNullException(nameof(entryIds));
            var ids = entryIds.ToArray();
            if (ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("At least one usable selected entry ID is required.", nameof(entryIds));
            return SendCommandAsync(userId, "delete", new JObject { ["ids"] = new JArray(ids) }, HttpStatusCode.NoContent, cancellationToken);
        }

        /// <summary>Explicitly clears the authenticated caller's entire search history.</summary>
        /// <inheritdoc cref="ClearHistoryAsync"/>
        public void ClearHistory(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => ClearHistoryAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Explicitly clears the entire own history using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed whole-history clearing.</returns>
        /// <remarks>This distinct operation sends ids as the string all. It is not used by selected deletion or normal integration-test cleanup. The server enforces own-history access; disabled-history errors remain errors and uncertain writes are not replayed.</remarks>
        public Task ClearHistoryAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SendCommandAsync(userId, "delete", new JObject { ["ids"] = "all" }, HttpStatusCode.NoContent, cancellationToken);
        }

        private async Task<RestResponse> SendCommandAsync(string userId, string action, JObject options, HttpStatusCode expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = CreateRestRequest(ApiVersionPrefix + "search-history", Method.Post);
            request.AddStringBody(new JObject { ["action"] = action, ["params"] = options }.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(expected));
            return response;
        }
        private static T Parse<T>(string content, Func<JObject, T> convert)
        {
            try
            {
                using (var text = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the search history."); return convert(root); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is OverflowException || error is InvalidCastException || error is FormatException)
            { throw new InvalidResponseDataException("The search-history response has invalid identity, metadata or page structure.", error); }
        }
        private static SearchHistoryEntry ParseEntry(JObject data)
        {
            var id = data["id"]; var term = data["term"]; var count = data["count"]; var timestamp = data["timestamp"];
            if (id?.Type != JTokenType.String || string.IsNullOrWhiteSpace(id.Value<string>()) || term?.Type != JTokenType.String ||
                count?.Type != JTokenType.Integer || timestamp?.Type != JTokenType.String)
                throw new InvalidDataException("A search-history entry requires identity, text, integer count and ISO8601 timestamp.");
            var uses = count.ToObject<long>();
            if (uses < 1) throw new InvalidDataException("Recorded usage count must be positive.");
            var timeText = timestamp.Value<string>();
            if (timeText == null || !Regex.IsMatch(timeText, @"^\d{4}-\d{2}-\d{2}(?:T|$)") ||
                !DateTimeOffset.TryParse(timeText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                throw new InvalidDataException("The usage timestamp must be usable ISO8601 data.");
            DateTimeOffset? time = Regex.IsMatch(timeText, @"(?:[Zz]|[+-]\d{2}:?\d{2})$") ? (DateTimeOffset?)parsed : null;
            return new SearchHistoryEntry(id.Value<string>(), term.Value<string>(), uses, timeText, time);
        }
        private static string Identity(string id) => Guid.TryParse(id, out var value) ? value.ToString("D") : id;
    }
}
