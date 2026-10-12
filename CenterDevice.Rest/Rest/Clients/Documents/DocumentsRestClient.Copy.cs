using Newtonsoft.Json.Linq;
using CenterDevice.Rest.Exceptions;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Creates server-side copies of selected documents.</summary>
        /// <inheritdoc cref="CopyDocumentsAsync"/>
        public IReadOnlyList<string> CopyDocuments(string userId, IEnumerable<DocumentCopyRequest> documents, string collectionId = null, string folderId = null, CancellationToken cancellationToken = default(CancellationToken))
            => CopyDocumentsAsync(userId, documents, collectionId, folderId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates selected document copies using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documents">The nonempty source/filename selections, enumerated once before authorization. Repeated source identifiers are dispatched in separate batches to create separate copies.</param>
        /// <param name="collectionId">The optional destination collection; mutually exclusive with folderId.</param>
        /// <param name="folderId">The optional destination folder; mutually exclusive with collectionId.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O; cancellation does not establish that no copies were created.</param>
        /// <returns>An immutable list of server-confirmed new identifiers in batch order and server order within each batch; the response does not establish a mapping to source selections.</returns>
        /// <remarks>Requires an admin, internal or external user with source visibility and destination access. Copies each latest original representation without inheriting sharing, tags, comments, version history or extended metadata. Source documents and references remain unchanged. Each batch contains distinct source identities because servers may coalesce repeated identities in one command. Batches are sequential and not atomic; failure or cancellation can leave confirmed earlier copies and uncertain current copies. Non-idempotent: uncertain writes are not automatically replayed. A malformed or incomplete result does not establish that the server created no copies.</remarks>
        /// <exception cref="ArgumentNullException">The document sequence is null.</exception>
        /// <exception cref="ArgumentException">The sequence is empty, contains a null selection, or destination arguments are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        /// <exception cref="InvalidResponseDataException">A command returns incomplete, duplicated or source-reusing copy identifiers.</exception>
        public async Task<IReadOnlyList<string>> CopyDocumentsAsync(string userId, IEnumerable<DocumentCopyRequest> documents, string collectionId = null, string folderId = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCopyDestination(collectionId, folderId);
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            var selected = documents.ToArray();
            if (selected.Length == 0 || selected.Any(item => item == null)) throw new ArgumentException("At least one nonnull copy selection is required.", nameof(documents));
            cancellationToken.ThrowIfCancellationRequested();
            var batches = new List<List<DocumentCopyRequest>>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in selected)
            {
                var key = Guid.TryParse(item.DocumentId, out var guid) ? guid.ToString("D") : item.DocumentId;
                occurrences.TryGetValue(key, out var batchIndex);
                occurrences[key] = batchIndex + 1;
                if (batchIndex == batches.Count) batches.Add(new List<DocumentCopyRequest>());
                batches[batchIndex].Add(item);
            }
            var sourceIds = selected.Select(item => item.DocumentId).ToArray();
            var allIds = new List<string>();
            var created = new HashSet<string>(StringComparer.Ordinal);
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parameters = new JObject { ["documents"] = new JArray(batch.Select(item => new JObject { ["document-id"] = item.DocumentId, ["filename"] = item.Filename })) };
                if (collectionId != null) parameters["collection"] = collectionId;
                if (folderId != null) parameters["folder"] = folderId;
                var ids = await ExecuteDocumentCopyAsync(userId, URI_RESOURCE, parameters, sourceIds, true, cancellationToken).ConfigureAwait(false);
                if (ids.Length != batch.Count || ids.Any(id => !created.Add(Guid.TryParse(id, out var guid) ? guid.ToString("D") : id)))
                    throw new InvalidResponseDataException("The server did not confirm distinct copies for every selection.", new InvalidDataException("Copy results must be complete and must not reuse earlier batch identities."));
                allIds.AddRange(ids);
            }
            return Array.AsReadOnly(allIds.ToArray());
        }
    }
}
