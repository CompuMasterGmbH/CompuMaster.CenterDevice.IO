using CenterDevice.Rest.Clients.Comments;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tags
{
    /// <summary>Retrieves the manufacturer's deprecated read-only visible-tag inventory.</summary>
    /// <remarks>API 2.29 section 5.10.1 recommends a document search with rows zero and a tags facet. This separate endpoint preserves historical protocol access without changing search defaults or modifying tags.</remarks>
    public class TagsRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a tag client using the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The existing authorization provider.</param>
        /// <param name="configuration">The selected REST origin and user agent.</param>
        /// <param name="errorHandler">The existing error/expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public TagsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }
        /// <summary>Retrieves original tags from documents visible to the authenticated caller.</summary>
        /// <inheritdoc cref="GetTagsAsync"/>
        [Obsolete("The manufacturer deprecated GET /tags. Use document search with Rows = 0 and a tags facet for new workflows.")]
        public VisibleTags GetTags(string userId, CancellationToken cancellationToken = default(CancellationToken)) => GetTagsAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves visible tags using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The original immutable tag strings and exact raw response JSON.</returns>
        /// <remarks>No tag normalization, sorting, deduplication, paging or permission inference is applied. The server determines visibility; omitted tags are invalid rather than assumed empty.</remarks>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">The successful response has invalid tag metadata.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        [Obsolete("The manufacturer deprecated GET /tags. Use document search with Rows = 0 and a tags facet for new workflows.")]
        public async Task<VisibleTags> GetTagsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(ApiVersionPrefix + "tags", Method.Get); request.AddOrUpdateHeader("Accept", "application/json");
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK)); return CommentJson.Parse(response.Content, data => new VisibleTags(data, response.Content));
        }
    }
    /// <summary>Contains the immutable original visible-tag inventory.</summary>
    public sealed class VisibleTags
    {
        private readonly JObject data;
        internal VisibleTags(JObject data, string rawJson)
        { this.data = (JObject)data.DeepClone(); RawJson = rawJson; if (!(data["tags"] is JArray tags) || tags.Any(tag => tag.Type != JTokenType.String)) throw new InvalidDataException("The tags response requires original string entries."); Tags = Array.AsReadOnly(tags.Values<string>().ToArray()); }
        /// <summary>Gets original tag strings in server order, including duplicates and casing.</summary>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>Gets the exact original response JSON.</summary>
        public string RawJson { get; }
        /// <summary>Copies the complete original response including future fields.</summary>
        /// <returns>A defensive copy of the original response.</returns>
        public JObject GetRawDetails() => (JObject)data.DeepClone();
    }
}
