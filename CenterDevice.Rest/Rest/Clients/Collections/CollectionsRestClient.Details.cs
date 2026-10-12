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

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionsRestClient
    {
        /// <summary>Retrieves immutable visible collection details with explicit optional filters.</summary>
        /// <inheritdoc cref="GetCollectionDetailsAsync"/>
        public IReadOnlyList<CollectionDetails> GetCollectionDetails(string userId, CollectionListingOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetCollectionDetailsAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves immutable visible collection details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">Copied optional public/name/group/identifier/metadata/field filters, or null for server defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>An immutable collection list, including an explicit empty list for HTTP 204.</returns>
        /// <remarks>API 2.29 section 5.3.21 applies visibility and optional public inclusion on the server. Metadata filters encode each key and value first, then the full query; duplicates preserve server last-value semantics. Missing projected fields remain unknown. Existing listings/interfaces/defaults are unchanged.</remarks>
        /// <exception cref="ArgumentException">A supplied selection, group or metadata pair is unusable.</exception>
        /// <exception cref="InvalidResponseDataException">A successful listing lacks a collection array or contains malformed metadata.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<IReadOnlyList<CollectionDetails>> GetCollectionDetailsAsync(string userId, CollectionListingOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE, Method.Get);
            if (options != null)
            {
                var includePublic = options.IncludePublic;
#pragma warning disable CS0618
                var all = options.All;
#pragma warning restore CS0618
                var name = options.Name; var group = options.Group;
                var ids = SnapshotCollectionSelection(options.Ids, "ids"); var fields = SnapshotCollectionSelection(options.Fields, "fields");
                var metadata = options.ExtendedMetadata?.ToArray();
                if (group != null && string.IsNullOrWhiteSpace(group)) throw new ArgumentException("An explicit group identifier must be nonblank.", nameof(options));
                if (metadata != null && (metadata.Length == 0 || metadata.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null))) throw new ArgumentException("Explicit metadata filters require nonblank keys and nonnull values.", nameof(options));
                if (includePublic.HasValue) request.AddQueryParameter("include-public", includePublic.Value ? "true" : "false");
                if (all.HasValue) request.AddQueryParameter("all", all.Value ? "true" : "false");
                if (name != null) request.AddQueryParameter("name", name);
                if (group != null) request.AddQueryParameter("group", group);
                if (ids != null) request.AddQueryParameter("ids", string.Join(",", ids));
                if (fields != null) request.AddQueryParameter("fields", string.Join(",", fields));
                if (metadata != null) request.AddQueryParameter("extended-metadata", string.Join(",", metadata.Select(pair => Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value))));
            }
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (response.StatusCode == HttpStatusCode.NoContent) return new List<CollectionDetails>().AsReadOnly();
            try
            {
                using (var source = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read() || !(root["collections"] is JArray entries)) throw new InvalidDataException("Expected a single collection-list object with a collections array.");
                    var result = new List<CollectionDetails>();
                    foreach (var entry in entries)
                    {
                        if (!(entry is JObject data)) throw new InvalidDataException("Each collection entry must be an object.");
                        var details = new CollectionDetails(data);
                        if (details.Id != null && string.IsNullOrWhiteSpace(details.Id)) throw new InvalidDataException("A supplied collection identifier must be nonblank.");
                        result.Add(details);
                    }
                    return result.AsReadOnly();
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The collection listing has invalid metadata or structure.", error); }
        }

        private static string[] SnapshotCollectionSelection(IEnumerable<string> values, string name)
        {
            var selection = values?.ToArray();
            if (selection != null && (selection.Length == 0 || selection.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(",")))) throw new ArgumentException("A supplied " + name + " selection must contain individual nonblank values.", name);
            return selection;
        }
    }
}
