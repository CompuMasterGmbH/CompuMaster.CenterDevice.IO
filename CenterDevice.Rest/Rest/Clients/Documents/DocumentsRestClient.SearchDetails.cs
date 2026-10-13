using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Searches visible documents with complete additive typed parameters.</summary>
        /// <inheritdoc cref="SearchDocumentDetailsAsync"/>
        public DocumentQueryPage SearchDocumentDetails(string userId, DocumentSearchParameters parameters, CancellationToken cancellationToken = default(CancellationToken))
            => SearchDocumentDetailsAsync(userId, parameters, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Searches visible document metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="parameters">The copied explicit filters, nested named parameters, fields, sort, facets and paging choices.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The original immutable query page with total hits, complete metadata and exact raw JSON.</returns>
        /// <remarks>API 2.29 section 5.2.22 combines query/filter/named sections on the server. Public/archived/workflow named options stay explicit; unknown catalogs and negated raw selections are preserved. The server controls indexing delay, visibility, sorting, page limits and text truncation. No local normalization, default sort, automatic pagination, cursor, stable snapshot or permission inference is added. Existing request types, string-valued named Params, methods and interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The parameters are null.</exception>
        /// <exception cref="ArgumentException">A known query shape or explicit required named/sort selection is invalid.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata has an invalid query-page structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentQueryPage> SearchDocumentDetailsAsync(string userId, DocumentSearchParameters parameters, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            var snapshot = DocumentSearchContractValidation.Snapshot(parameters);
            var idExcluded = snapshot["fields"]?["excludes"] is JArray excluded && excluded.Values<string>().Contains("id");
            var body = new JObject { ["action"] = "search", ["params"] = snapshot };
            var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json"); request.AddOrUpdateHeader("Accept", "application/json"); request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return DocumentQueryPage.Parse(response.Content, true, idExcluded);
        }
    }
}
