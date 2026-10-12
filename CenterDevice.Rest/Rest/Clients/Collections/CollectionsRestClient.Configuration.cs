using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionsRestClient
    {
        /// <summary>Creates a collection with explicit complete configuration options.</summary>
        /// <inheritdoc cref="CreateConfiguredCollectionAsync"/>
        public CreateCollectionResponse CreateConfiguredCollection(string userId, CollectionCreationRequest configuration, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredCollectionAsync(userId, configuration, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates a configured collection using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="configuration">The complete copied name and explicit options without inferred defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The new identifier and original partial document/user/group failures, even when HTTP 201 confirms creation.</returns>
        /// <remarks>API 2.29 section 5.3.1 creates a new owner-held collection. Inspect failed selections rather than inferring atomicity or full sharing success. Duplicate-name conflicts and role/module restrictions remain original server failures. Creation is not idempotent; no new replay is added. Cancellation cannot establish that no collection was created. Existing name-only methods and defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentException">The known configuration structure is invalid.</exception>
        /// <exception cref="InvalidResponseDataException">Successful creation has no usable identifier.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<CreateCollectionResponse> CreateConfiguredCollectionAsync(string userId, CollectionCreationRequest configuration, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            var body = configuration.SnapshotForSend().ToString(Formatting.None);
            var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json"); request.AddStringBody(body, RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.Created));
            try
            {
                using (var source = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var data = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the collection creation result.");
                    var result = new CreateCollectionResponse { Id = CollectionDetailsParser.Text(data["id"]),
                        FailedDocuments = CollectionDetailsParser.Identifiers(data["failed-documents"])?.ToList(),
                        FailedUsers = CollectionDetailsParser.Identifiers(data["failed-users"])?.ToList(),
                        FailedGroups = CollectionDetailsParser.Identifiers(data["failed-groups"])?.ToList() };
                    if (string.IsNullOrWhiteSpace(result.Id)) throw new InvalidDataException("Expected the created collection identifier.");
                    return result;
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("Collection creation returned invalid identity or partial-failure metadata.", error); }
        }
    }
    public partial class CollectionRestClient
    {
        /// <summary>Updates explicit properties of a selected collection.</summary>
        /// <inheritdoc cref="UpdateConfigurationAsync"/>
        public void UpdateConfiguration(string userId, string collectionId, CollectionUpdateRequest configuration, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateConfigurationAsync(userId, collectionId, configuration, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates selected collection properties using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="configuration">The copied explicit name/unique/public/disclosure/filter changes; omitted settings remain unchanged.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.3.2 preserves original filter values and omitted settings. Ownership, internal/admin restrictions, uniqueness and normal/intelligent collection state remain server-authoritative. No lookup, inferred grant, default insertion or new replay is added. Existing rename methods remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The configuration is null.</exception>
        /// <exception cref="ArgumentException">The collection or known configuration structure is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no properties changed.</exception>
        public async Task UpdateConfigurationAsync(string userId, string collectionId, CollectionUpdateRequest configuration, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(collectionId) || collectionId == "." || collectionId == "..") throw new ArgumentException("A selected collection identifier is required.", nameof(collectionId));
            var body = configuration.SnapshotForSend().ToString(Formatting.None);
            var request = CreateRestRequest(URI_RESOURCE + "/" + Uri.EscapeDataString(collectionId), Method.Put, "application/json"); request.AddStringBody(body, RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
