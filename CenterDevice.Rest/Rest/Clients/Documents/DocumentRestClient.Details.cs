using CenterDevice.Rest.Clients.Documents.Metadata;
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
    public partial class DocumentRestClient
    {
        /// <summary>Retrieves immutable metadata for an explicitly selected document and optional version.</summary>
        /// <inheritdoc cref="GetDocumentDetailsAsync"/>
        public DocumentDetails GetDocumentDetails(string userId, string documentId, long? version = null, IEnumerable<string> includes = null, IEnumerable<string> excludes = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetDocumentDetailsAsync(userId, documentId, version, includes, excludes, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable selected document metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The explicitly selected document identifier.</param>
        /// <param name="version">The positive selected version, or null for the server's current version.</param>
        /// <param name="includes">Copied individual included fields, or null to omit them.</param>
        /// <param name="excludes">Copied individual excluded fields, or null to omit them; cannot coexist with includes or contain base.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original metadata preserving hidden identities, nullable counts, dates, representations, locks and extended information.</returns>
        /// <remarks>API 2.29 section 5.2.20 returns up-to-date metadata independently of search-index delay. The server controls visibility and guest history restrictions. Omitted fields remain unknown; no document-date fallback, permission, ready representation or absence of locks is inferred. Existing getters, models and interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected document or field selection is unusable.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The selected version is not positive.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata has invalid identity, selected version or known structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentDetails> GetDocumentDetailsAsync(string userId, string documentId, long? version = null, IEnumerable<string> includes = null, IEnumerable<string> excludes = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(documentId) || documentId == "." || documentId == "..") throw new ArgumentException("A selected document identifier is required.", nameof(documentId));
            if (version.HasValue && version.Value <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            var include = SnapshotDocumentFields(includes); var exclude = SnapshotDocumentFields(excludes);
            if (include != null && exclude != null || exclude?.Contains("base") == true) throw new ArgumentException("Includes and excludes are exclusive, and base cannot be excluded.");
            var path = URI_RESOURCE + Uri.EscapeDataString(documentId); if (version.HasValue) path += ";version=" + version.Value.ToString(CultureInfo.InvariantCulture);
            var request = CreateRestRequest(path, Method.Get); request.AddOrUpdateHeader("Accept", "application/json");
            if (include != null) request.AddQueryParameter("includes", string.Join(",", include)); if (exclude != null) request.AddQueryParameter("excludes", string.Join(",", exclude));
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return DocumentDetailsParser.Parse(response.Content, documentId, version, exclude?.Contains("id") == true);
        }
        private static string[] SnapshotDocumentFields(IEnumerable<string> values)
        { var fields = values?.ToArray(); if (fields != null && (fields.Length == 0 || fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(",")))) throw new ArgumentException("Explicit metadata fields must contain individual nonblank names."); return fields; }
    }
}
