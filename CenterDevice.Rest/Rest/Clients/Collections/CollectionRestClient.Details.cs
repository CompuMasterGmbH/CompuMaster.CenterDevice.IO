using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Retrieves immutable details for an explicitly selected collection.</summary>
        /// <inheritdoc cref="GetCollectionDetailsAsync"/>
        public CollectionDetails GetCollectionDetails(string userId, string collectionId, IEnumerable<string> fields = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetCollectionDetailsAsync(userId, collectionId, fields, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves immutable selected collection details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="fields">The copied nonempty selection of field names, or null for server defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original nullable metadata, preserving extended metadata, filters, visibility counts and archive dates.</returns>
        /// <remarks>Owner-only auditing-users and auditing-email-settings require explicit selection and server permission. Has-folders is optional; missing flags/counts remain unknown. Hidden-owner placeholders are preserved without inferring permissions. No old getter, interface or default behavior is changed.</remarks>
        /// <exception cref="ArgumentException">The collection or field selection is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata has invalid structure or identifies another collection.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<CollectionDetails> GetCollectionDetailsAsync(string userId, string collectionId, IEnumerable<string> fields = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(collectionId) || collectionId == "." || collectionId == "..") throw new ArgumentException("A selected collection identifier is required.", nameof(collectionId));
            var selection = fields?.ToArray();
            if (selection != null && (selection.Length == 0 || selection.Any(field => string.IsNullOrWhiteSpace(field) || field.Contains(",")))) throw new ArgumentException("A supplied field selection must contain individual nonblank names.", nameof(fields));
            var request = CreateRestRequest(URI_RESOURCE + "/" + Uri.EscapeDataString(collectionId), Method.Get);
            if (selection != null) request.AddQueryParameter("fields", string.Join(",", selection));
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            return CollectionDetailsParser.Parse(response.Content, collectionId);
        }
    }
}
