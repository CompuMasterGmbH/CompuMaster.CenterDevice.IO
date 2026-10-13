using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Timeline
{
    public partial class TimelineRestClient
    {
        /// <summary>Searches visible timeline events using complete explicit filters.</summary>
        /// <inheritdoc cref="SearchTimelineAsync"/>
        public TimelineEventPage SearchTimeline(string userId, TimelineSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => SearchTimelineAsync(userId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Searches visible timeline events using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="request">The explicit filters and paging, copied before authorization; null options retain server defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>The complete original bounded page, ordered by decreasing timestamp.</returns>
        /// <remarks>Visibility is determined at event creation time. Document filters exclude other event types. Bounds are inclusive lower and exclusive upper; original ISO8601 strings remain unchanged. No paging or current-resource reconstruction is performed.</remarks>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentException">An explicit filter or paging choice is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<TimelineEventPage> SearchTimelineAsync(string userId, TimelineSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteTimelinePageAsync(userId, "search", request, cancellationToken);

        /// <summary>Retrieves one logical timeline page using complete explicit iteration choices.</summary>
        /// <inheritdoc cref="ScrollTimelineAsync"/>
        public TimelineEventPage ScrollTimeline(string userId, TimelineScrollRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => ScrollTimelineAsync(userId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves one logical timeline page using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="request">The explicit filters, previous cursor, direction and paging, copied before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>The complete original bounded page in logical event order.</returns>
        /// <remarks>The previous cursor is excluded. Null starts at the server's default oldest event; null sort retains ascending order. An unknown or expired cursor remains a server error; consumers decide explicit resource reconciliation. No automatic paging, cursor advancement, reversal, state inference or new retry is performed. Historical methods/defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentException">An explicit filter, cursor or paging choice is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<TimelineEventPage> ScrollTimelineAsync(string userId, TimelineScrollRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteTimelinePageAsync(userId, "scroll", request, cancellationToken);

        private async Task<TimelineEventPage> ExecuteTimelinePageAsync(string userId, string action, object options, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); if (options == null) throw new ArgumentNullException("request");
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var snapshot = JObject.FromObject(options, serializer); ValidateTimelineChoices(snapshot);
            var maximumRows = Math.Min(snapshot.Value<int?>("rows") ?? 1000, 1000); var previousId = snapshot.Value<string>("previous-id");
            var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json");
            request.AddStringBody(new JObject { ["action"] = action, ["params"] = snapshot }.ToString(Formatting.None), RestSharp.ContentType.Json);
            var auth = await GetOAuthInfoAsync(userId, token).ConfigureAwait(false);
            var response = await ExecuteAsync(auth, request, token).ConfigureAwait(false); token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return TimelineEventPage.Parse(response.Content, maximumRows, previousId);
        }
        private static void ValidateTimelineChoices(JObject options)
        {
            foreach (var filter in new[] { new KeyValuePair<string, int>("types", 50), new KeyValuePair<string, int>("users", 10), new KeyValuePair<string, int>("document-ids", 100) })
            {
                if (options[filter.Key] == null) continue;
                var values = (JArray)options[filter.Key];
                if (values.Any(value => value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>()))) throw new ArgumentException("Timeline filters require nonblank original strings.", "request");
                var identities = values.Values<string>().Select(value => filter.Key == "types" ? value : Guid.TryParse(value, out var id) ? id.ToString("D") : value);
                if (identities.Distinct(StringComparer.Ordinal).Count() > filter.Value) throw new ArgumentException("The timeline filter exceeds its distinct-value limit.", "request");
            }
            if (options.Value<int?>("rows") <= 0 || options.Value<int?>("offset") < 0 || options.Value<int?>("offset") > 5000) throw new ArgumentException("Timeline paging is outside the declared limits.", "request");
            var sort = options.Value<string>("sort"); if (sort != null && sort != "asc" && sort != "desc") throw new ArgumentException("Timeline sorting must be asc or desc.", "request");
            var previous = options.Value<string>("previous-id"); if (previous != null && string.IsNullOrWhiteSpace(previous)) throw new ArgumentException("An explicit previous cursor must not be blank.", "request");
            if (options["timestamp"] is JObject timestamp) foreach (var bound in new[] { "from", "to" })
            {
                var original = timestamp.Value<string>(bound);
                if (original != null && (!Regex.IsMatch(original, @"^\d{4}-\d{2}-\d{2}(?:T|$)") || !DateTimeOffset.TryParse(original, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) throw new ArgumentException("A timeline bound must be original ISO8601 date text.", "request");
            }
        }
    }
}
