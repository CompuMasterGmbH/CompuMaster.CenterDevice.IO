using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Provides document REST operations.</summary>
    public partial class DocumentRestClient
    {
        /// <summary>Creates a new current version from an existing document version.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="version">The positive version number whose contents become a new version.</param>
        /// <returns>The server-confirmed new version number.</returns>
        /// <remarks>Preserves the document ID and version history. This operation creates a new version on each successful call and must not be retried after an uncertain failure. The server requires a visible document and an admin, internal or external user; if create-new-version is locked, the caller must own the lock.</remarks>
        /// <exception cref="ArgumentException">The document identifier is missing.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The version is not positive.</exception>
        public NewVersionUploadResponse RevertDocumentVersion(string userId, string id, long version)
            => RevertDocumentVersionAsync(userId, id, version).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates a new current version asynchronously from an existing document version.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="version">The positive version number whose contents become a new version.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>The server-confirmed new version number.</returns>
        /// <remarks>Does not reorder, remove or renumber existing versions. Non-idempotent: each successful call creates a new version. Cancellation does not prove the server did not commit the change. Role, visibility and create-new-version lock restrictions are enforced by the server; ambiguous writes are not automatically replayed.</remarks>
        /// <exception cref="ArgumentException">The document identifier is missing.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The version is not positive.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<NewVersionUploadResponse> RevertDocumentVersionAsync(string userId, string id, long version, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
            var request = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            request.AddJsonBody(new { action = "revert", @params = new { version } });
            var info = await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false);
            var result = await ExecuteAsync<NewVersionUploadResponse>(info, request, cancellationToken).ConfigureAwait(false);
            return UnwrapResponse(result, new StatusCodeResponseHandler<NewVersionUploadResponse>(HttpStatusCode.Created));
        }

        /// <summary>Removes selected versions of a document.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="versions">A nonempty sequence of positive version numbers to remove.</param>
        /// <remarks>The server requires at least one version to remain and makes the largest remaining version current. Deleted numbers are not reused. Archived documents and documents in trash cannot have versions removed. Admin, internal or external roles are required; ownership, document-deletion tenant settings and lock ownership can further restrict removal. The client does not fetch or infer history/permissions before writing.</remarks>
        /// <exception cref="ArgumentException">The document identifier or version sequence is empty, or a version is not positive.</exception>
        /// <exception cref="ArgumentNullException">The version sequence is null.</exception>
        public void RemoveDocumentVersions(string userId, string id, IEnumerable<long> versions)
            => RemoveDocumentVersionsAsync(userId, id, versions).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes selected versions of a document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="versions">A nonempty sequence of positive version numbers, enumerated once before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>A task completed after the server confirms removal.</returns>
        /// <remarks>Idempotent under the manufacturer contract. The server enforces remaining-version, archive/trash, role, document-deletion and ownership/lock restrictions. Preserves supplied numbers without renumbering or changing the document ID. Uses a snapshot of the sequence and does not automatically replay an uncertain write. Cancellation does not prove no versions were removed.</remarks>
        /// <exception cref="ArgumentException">The document identifier or version sequence is empty, or a version is not positive.</exception>
        /// <exception cref="ArgumentNullException">The version sequence is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task RemoveDocumentVersionsAsync(string userId, string id, IEnumerable<long> versions, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (versions == null) throw new ArgumentNullException(nameof(versions));
            var selected = versions.ToArray();
            if (selected.Length == 0 || selected.Any(version => version <= 0))
                throw new ArgumentException("At least one positive version number is required.", nameof(versions));
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            request.AddJsonBody(new { action = "delete-versions", @params = new { versions = selected } });
            var info = await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false);
            var result = await ExecuteAsync(info, request, cancellationToken).ConfigureAwait(false);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        private static void ValidateVersionDocumentId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A selected document identifier is required.", nameof(id));
        }
    }
}
