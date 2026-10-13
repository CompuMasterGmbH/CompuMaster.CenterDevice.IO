using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Retrieves an immutable page of current visible document metadata.</summary>
        /// <inheritdoc cref="GetDocumentPageAsync"/>
        public DocumentQueryPage GetDocumentPage(string userId, DocumentMetadataListingOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetDocumentPageAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves explicitly filtered current document metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">Copied identifier/location/date/public/field/paging selections, or null for server defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The immutable original page with total hits and exact raw JSON.</returns>
        /// <remarks>API 2.29 section 5.2.21 returns latest-version metadata ordered by descending version date. Last-change lower/upper bounds are inclusive/exclusive, respectively. The server controls visible identities, public inclusion and page maxima. No date conversion, implicit offset/size, automatic iteration, cursor or stable snapshot is added. Existing listings and interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentException">A supplied identifier/field selection is unusable or includes/excludes conflict.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Offset is negative or rows is not positive.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">A successful page has invalid identity or structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentQueryPage> GetDocumentPageAsync(string userId, DocumentMetadataListingOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(URI_RESOURCE, Method.Get); request.AddOrUpdateHeader("Accept", "application/json"); var idExcluded = false;
            if (options != null)
            {
                var ids = SnapshotPageSelection(options.Ids); var includes = SnapshotPageSelection(options.Includes); var excludes = SnapshotPageSelection(options.Excludes);
                var collection = options.Collection; var folder = options.Folder; var from = options.LastChangeFrom; var to = options.LastChangeTo; var includePublic = options.IncludePublic; var offset = options.Offset; var rows = options.Rows;
                if (offset < 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Offset must be nonnegative and rows positive when supplied.");
                if (includes != null && excludes != null || excludes?.Contains("base") == true) throw new ArgumentException("Includes and excludes are exclusive, and base cannot be excluded.");
                idExcluded = excludes?.Contains("id") == true;
                foreach (var pair in new[] { new KeyValuePair<string, string>("collection", collection), new KeyValuePair<string, string>("folder", folder), new KeyValuePair<string, string>("last-change-from", from), new KeyValuePair<string, string>("last-change-to", to) }) if (pair.Value != null) request.AddQueryParameter(pair.Key, pair.Value);
                if (ids != null) request.AddQueryParameter("ids", string.Join(",", ids)); if (includes != null) request.AddQueryParameter("includes", string.Join(",", includes)); if (excludes != null) request.AddQueryParameter("excludes", string.Join(",", excludes));
                if (includePublic.HasValue) request.AddQueryParameter("include-public", includePublic.Value ? "true" : "false");
                if (offset.HasValue) request.AddQueryParameter("offset", offset.Value.ToString(CultureInfo.InvariantCulture)); if (rows.HasValue) request.AddQueryParameter("rows", rows.Value.ToString(CultureInfo.InvariantCulture));
            }
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK)); return DocumentQueryPage.Parse(response.Content, false, idExcluded);
        }
        private static string[] SnapshotPageSelection(IEnumerable<string> values)
        { var selected = values?.ToArray(); if (selected != null && (selected.Length == 0 || selected.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(",")))) throw new ArgumentException("A supplied page selection requires individual nonblank comma-free values."); return selected; }
    }
}
