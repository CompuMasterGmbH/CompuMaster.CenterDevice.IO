using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Trash
{
    public partial class TrashRestClient
    {
        private static readonly HashSet<string> SearchFields = new HashSet<string>(new[] { "id", "upload-date", "filename", "size", "mimetype", "version-date", "tags", "score", "deletion-date" }, StringComparer.Ordinal);
        private static readonly HashSet<string> SortFields = new HashSet<string>(new[] { "score", "filename", "upload-date", "version-date", "size", "deletion-date" }, StringComparer.Ordinal);
        private static readonly HashSet<string> FacetFields = new HashSet<string>(new[] { "extension", "filename", "mimetype", "size", "tags", "upload-date", "version-date", "deletion-date" }, StringComparer.Ordinal);

        /// <summary>Searches the authenticated user's own trash.</summary>
        /// <inheritdoc cref="SearchAsync"/>
        public TrashSearchResults Search(string userId, TrashSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => SearchAsync(userId, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Searches the authenticated user's own trash using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="request">The nonnull restricted query, snapshotted before authorization. Omitted options retain server defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The total hit count, ordered page and unmodified facet data, preserving omitted metadata.</returns>
        /// <remarks>Requires an admin, internal or external user and searches only that user's trash. Text covers filenames and extracted document content. Zero rows requests only hits/facets; page sizes may be capped by the server. Named queries and negation are unavailable. Uncertain HTTP outcomes are not replayed; existing expired-token refresh behavior is preserved.</remarks>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentException">A bound, field selection, sort or facet is invalid.</exception>
        /// <exception cref="InvalidResponseDataException">The successful response has invalid counts, identities, metadata or page/facet structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<TrashSearchResults> SearchAsync(string userId, TrashSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            var serializer = JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, DateParseHandling = DateParseHandling.None });
            var snapshot = JObject.FromObject(request, serializer);
            ValidateSearchSnapshot(snapshot);
            var offset = snapshot.Value<int?>("offset") ?? 0;
            var rows = snapshot.Value<int?>("rows");
            var excludedIdentity = (snapshot["fields"]?["excludes"] as JArray)?.Values<string>().Contains("id") == true;
            var rest = CreateRestRequest(Resource, Method.Post);
            rest.AddStringBody(new JObject { ["action"] = "search", ["params"] = snapshot }.ToString(Formatting.None), RestSharp.ContentType.Json);
            cancellationToken.ThrowIfCancellationRequested();
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), rest, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                JObject data;
                using (var text = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { data = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the search result."); }
                if (data["hits"]?.Type != JTokenType.Integer) throw new InvalidDataException("The search result requires a total hit count.");
                var hits = data.Value<long>("hits");
                if (hits < 0) throw new InvalidDataException("The total hit count cannot be negative.");
                var token = data["documents"];
                var emptyPage = hits == 0 || offset >= hits || rows == 0;
                List<TrashSearchDocument> documents;
                if (token == null || token.Type == JTokenType.Null)
                {
                    if (!emptyPage) throw new InvalidDataException("The nonempty page is missing.");
                    documents = new List<TrashSearchDocument>();
                }
                else
                {
                    if (token.Type != JTokenType.Array) throw new InvalidDataException("Documents must be an array.");
                    foreach (var entry in (JArray)token) ValidateSearchDocumentShape(entry);
                    documents = token.ToObject<List<TrashSearchDocument>>(serializer);
                    if ((!emptyPage && documents.Count == 0) || (rows.HasValue && documents.Count > rows.Value) ||
                        documents.Count > Math.Max(0, hits - offset)) throw new InvalidDataException("The page contradicts its request or total hit count.");
                }
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var document in documents)
                {
                    if (document == null || (!excludedIdentity && string.IsNullOrWhiteSpace(document.Id)) ||
                        (document.Id != null && (string.IsNullOrWhiteSpace(document.Id) || !seen.Add(Guid.TryParse(document.Id, out var guid) ? guid.ToString("D") : document.Id))) ||
                        document.Size < 0 || (document.Score.HasValue && (document.Score < 0 || double.IsNaN(document.Score.Value) || double.IsInfinity(document.Score.Value))))
                        throw new InvalidDataException("A search document has invalid metadata or repeated identity.");
                }
                var facets = data["facets"];
                if (facets != null && facets.Type != JTokenType.Null && facets.Type != JTokenType.Object)
                    throw new InvalidDataException("Facet data must be an object.");
                return new TrashSearchResults(hits, documents, facets as JObject);
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is OverflowException || error is FormatException || error is InvalidCastException)
            { throw new InvalidResponseDataException("The trash search returned an invalid result.", error); }
        }

        private static void ValidateSearchDocumentShape(JToken entry)
        {
            if (entry.Type != JTokenType.Object) throw new InvalidDataException("A search document must be an object.");
            foreach (var name in new[] { "id", "filename", "mimetype", "upload-date", "version-date", "deletion-date" })
                if (entry[name] != null && entry[name].Type != JTokenType.Null && entry[name].Type != JTokenType.String)
                    throw new InvalidDataException("Textual metadata must retain its JSON string type.");
            if (entry["size"] != null && entry["size"].Type != JTokenType.Null && entry["size"].Type != JTokenType.Integer)
                throw new InvalidDataException("Content size must be an integer.");
            if (entry["score"] != null && entry["score"].Type != JTokenType.Null && entry["score"].Type != JTokenType.Integer && entry["score"].Type != JTokenType.Float)
                throw new InvalidDataException("Relevance score must be numeric.");
            var tags = entry["tags"];
            if (tags != null && tags.Type != JTokenType.Null && (tags.Type != JTokenType.Array || tags.Any(tag => tag.Type != JTokenType.String)))
                throw new InvalidDataException("Tags must be an array of strings.");
        }

        private static void ValidateSearchSnapshot(JObject request)
        {
            if (request.Value<int?>("offset") < 0 || request.Value<int?>("rows") < 0)
                throw new ArgumentException("Search offsets and row counts must be nonnegative.", "request");
            var fields = request["fields"];
            if (fields?["includes"] != null && fields["excludes"] != null)
                throw new ArgumentException("Includes and excludes are mutually exclusive.", "request");
            foreach (var field in new[] { fields?["includes"], fields?["excludes"] }.OfType<JArray>().SelectMany(values => values.Values<string>()))
                if (field == null || !SearchFields.Contains(field)) throw new ArgumentException("Unsupported trash metadata field.", "request");
            var filter = request["filter"];
            foreach (var name in new[] { "ids", "filenames", "extensions", "mimetypes", "tags" })
                if ((filter?[name] as JArray)?.Any(value => value.Type != JTokenType.String || (name == "ids" && string.IsNullOrWhiteSpace(value.Value<string>()))) == true)
                    throw new ArgumentException("Filter values must be strings with usable document identifiers.", "request");
            var size = filter?["size"];
            if (size != null && (size.Value<long?>("from") < 0 || size.Value<long?>("to") < 0 || size.Value<long?>("from") > size.Value<long?>("to")))
                throw new ArgumentException("Size bounds must be nonnegative and ordered.", "request");
            foreach (var name in new[] { "upload-date", "last-change", "deletion-date" })
            {
                var range = filter?[name];
                if (range != null && range.Value<DateTimeOffset?>("from") > range.Value<DateTimeOffset?>("to"))
                    throw new ArgumentException("Date bounds must be ordered.", "request");
            }
            var sort = request["sort"];
            if (sort != null && (!SortFields.Contains(sort.Value<string>("field") ?? "") ||
                (sort["order"] != null && sort.Value<string>("order") != "asc" && sort.Value<string>("order") != "desc")))
                throw new ArgumentException("Unsupported trash search ordering.", "request");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var facets = request["facet"] as JArray ?? new JArray();
            foreach (var facet in facets)
            {
                if (facet.Type != JTokenType.Object || !FacetFields.Contains(facet.Value<string>("field") ?? "") || facet.Value<int?>("size") <= 0 ||
                    (facet["qualifier"] != null && string.IsNullOrWhiteSpace(facet.Value<string>("qualifier"))) ||
                    !names.Add(facet.Value<string>("field") + (facet["qualifier"] == null ? "" : "." + facet.Value<string>("qualifier"))))
                    throw new ArgumentException("Facets require supported fields, positive sizes and distinct response names.", "request");
            }
            if (facets.GroupBy(facet => facet.Value<string>("field"), StringComparer.Ordinal)
                .Any(group => group.Count() > 1 && group.Any(facet => facet["qualifier"] == null)))
                throw new ArgumentException("Repeated facet fields require qualifiers for every occurrence.", "request");
        }
    }
}
