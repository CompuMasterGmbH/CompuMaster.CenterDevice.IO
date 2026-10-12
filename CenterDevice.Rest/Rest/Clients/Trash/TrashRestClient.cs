using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Trash
{
    /// <summary>Retrieves and changes the authenticated user's own trash contents.</summary>
    /// <remarks>Requires an admin, internal or external user. Even administrators cannot access another user's trash through this resource. Uncertain writes are not automatically replayed.</remarks>
    public partial class TrashRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a trash client with the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The authorization information provider.</param>
        /// <param name="configuration">The REST origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public TrashRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        private string Resource => ApiVersionPrefix + "trash";

        /// <summary>Retrieves one page of trash documents.</summary>
        /// <inheritdoc cref="GetDocumentsAsync"/>
        public IReadOnlyList<TrashDocument> GetDocuments(string userId, int offset = 0, int rows = 500, CancellationToken cancellationToken = default(CancellationToken))
            => GetDocumentsAsync(userId, offset, rows, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves one page of trash documents using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="offset">The nonnegative zero-based start position.</param>
        /// <param name="rows">The requested number of documents, between 1 and the server's 500-item limit.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only page in descending server deletion-time order, without an inferred total count.</returns>
        /// <remarks>Only the user's own trash is accessible. A full page does not prove that more documents exist. Omitted metadata remains distinct from a known empty value.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">The offset or page size is invalid.</exception>
        /// <exception cref="InvalidResponseDataException">The successful response omits the listing or contains unusable, repeated or oversized document entries.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<IReadOnlyList<TrashDocument>> GetDocumentsAsync(string userId, int offset = 0, int rows = 500, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (rows < 1 || rows > 500) throw new ArgumentOutOfRangeException(nameof(rows));
            var request = CreateRestRequest(Resource, Method.Get);
            request.AddQueryParameter("offset", offset.ToString(CultureInfo.InvariantCulture));
            request.AddQueryParameter("rows", rows.ToString(CultureInfo.InvariantCulture));
            var response = await ExecuteAsync<TrashPage>(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            var documents = response.Data?.Documents;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (documents == null || documents.Count > rows || documents.Any(document => document == null || string.IsNullOrWhiteSpace(document.Id) ||
                (document.Size.HasValue && document.Size.Value < 0) || !seen.Add(Guid.TryParse(document.Id, out var guid) ? guid.ToString("D") : document.Id)))
                throw new InvalidResponseDataException("The trash response has no complete usable document page.", new InvalidDataException("The listing must retain distinct identities and valid sizes."));
            return documents.AsReadOnly();
        }

        /// <summary>Restores selected documents from the user's trash.</summary>
        /// <inheritdoc cref="RestoreDocumentsAsync"/>
        public DocumentCommandResult RestoreDocuments(string userId, IEnumerable<string> documentIds, CancellationToken cancellationToken = default(CancellationToken))
            => RestoreDocumentsAsync(userId, documentIds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Restores selected documents from the user's trash using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The nonempty identifiers, enumerated once and snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The exact server-reported failed identifiers, or complete success.</returns>
        /// <remarks>Attempts to retain metadata and properties from deletion; removed collections or other unavailable resources cannot be restored. Side effects are idempotent, while partial-failure responses can change. HTTP success can include partial failures. Uncertain writes are not automatically replayed.</remarks>
        /// <exception cref="ArgumentNullException">The selection is null.</exception>
        /// <exception cref="ArgumentException">The selection is empty or contains an unusable identifier.</exception>
        /// <exception cref="InvalidResponseDataException">The partial response omits usable failed identifiers or identifies an unselected document.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and does not establish whether restoration committed.</exception>
        public Task<DocumentCommandResult> RestoreDocumentsAsync(string userId, IEnumerable<string> documentIds, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteSelectedAsync(userId, "restore-documents", documentIds, cancellationToken);

        /// <summary>Permanently erases selected documents from the user's trash.</summary>
        /// <inheritdoc cref="EraseDocumentsAsync"/>
        public DocumentCommandResult EraseDocuments(string userId, IEnumerable<string> documentIds, CancellationToken cancellationToken = default(CancellationToken))
            => EraseDocumentsAsync(userId, documentIds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Permanently erases selected documents from the user's trash using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="RestoreDocumentsAsync"/>
        /// <remarks>Erases every version and representation of each selected trash document permanently; deleted data cannot be restored. Only the user's own trash is accessible. Side effects are idempotent, but partial-failure responses can change. Cancellation or failure can leave some documents erased; writes are not automatically replayed.</remarks>
        /// <exception cref="OperationCanceledException">Cancellation is requested and does not establish whether erasure committed.</exception>
        public Task<DocumentCommandResult> EraseDocumentsAsync(string userId, IEnumerable<string> documentIds, CancellationToken cancellationToken = default(CancellationToken))
            => ExecuteSelectedAsync(userId, "erase-documents", documentIds, cancellationToken);

        private Task<DocumentCommandResult> ExecuteSelectedAsync(string userId, string action, IEnumerable<string> documentIds, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var ids = SnapshotDocumentIds(documentIds, nameof(documentIds));
            token.ThrowIfCancellationRequested();
            return ExecuteDocumentListCommandAsync(userId, Resource, action, new { documents = ids }, ids, token);
        }

        /// <summary>Permanently erases all documents in the user's trash.</summary>
        /// <inheritdoc cref="EraseAllDocumentsAsync"/>
        public DocumentCommandResult EraseAllDocuments(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => EraseAllDocumentsAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Permanently erases all documents in the user's trash using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The exact server-reported failed identifiers, or complete success.</returns>
        /// <remarks>Explicitly targets the whole own-user trash, including documents unrelated to the calling workflow. Every version and representation is permanently removed and cannot be restored. Use selected erasure for test cleanup. Cancellation or failure can leave partial deletion; the command is not automatically replayed.</remarks>
        /// <exception cref="InvalidResponseDataException">The partial response omits usable failed identifiers.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and does not establish whether deletion committed.</exception>
        public async Task<DocumentCommandResult> EraseAllDocumentsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(Resource, Method.Delete);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (response.StatusCode == HttpStatusCode.NoContent) return new DocumentCommandResult(new string[0]);
            try
            {
                var failed = JObject.Parse(response.Content)["failed-documents"] as JArray;
                if (failed == null || failed.Count == 0 || failed.Any(id => id.Type != JTokenType.String || string.IsNullOrWhiteSpace(id.Value<string>())))
                    throw new InvalidDataException("A partial erasure response must identify failed documents.");
                return new DocumentCommandResult(failed.Values<string>());
            }
            catch (Exception error) when (error is Newtonsoft.Json.JsonException || error is InvalidDataException || error is ArgumentNullException)
            { throw new InvalidResponseDataException("The trash erasure returned an invalid partial-failure response.", error); }
        }

        private sealed class TrashPage
        {
            public TrashPage() { }
            public List<TrashDocument> Documents { get; set; }
        }
    }
}
