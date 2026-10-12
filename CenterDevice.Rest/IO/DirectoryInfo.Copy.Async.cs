using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public partial class DirectoryInfo
    {
        /// <summary>Copies a directory with optional visible subdirectories using asynchronous server commands.</summary>
        /// <param name="source">The source collection or folder using the same REST client and authenticated context.</param>
        /// <param name="newDirName">The new immediate child name, or null to retain the source name.</param>
        /// <param name="includeSubdirectories">Copies the visible folder structure when true; otherwise copies only immediate files.</param>
        /// <param name="cancellationToken">Cancels planning, authorization, admission and HTTP I/O.</param>
        /// <returns>A task completed after all planned destination resources are server confirmed.</returns>
        /// <remarks>Snapshots the visible source tree before creating a new child collection at the root or a new folder elsewhere. Each document uses the latest original representation and a new ID, without inherited sharing, tags, comments, history or extended metadata. The operation is not atomic; cancellation or failure can leave partial destination contents, which are not automatically removed or replayed. Source documents and references remain unchanged. Source listings can change after planning; concurrent mutation/listing on the same objects is unsupported.</remarks>
        /// <exception cref="ArgumentNullException">The source is null.</exception>
        /// <exception cref="ArgumentException">A name is invalid or the destination is the source or one of its descendants.</exception>
        /// <exception cref="NotSupportedException">The source is the root, belongs to a different instance/context or has no usable identity.</exception>
        /// <exception cref="IOException">The destination child already exists or the source structure repeats a directory identity.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested; it does not prove that no destination resources were created.</exception>
        public async Task AddDirectoryCopyAsync(DirectoryInfo source, string newDirName = null, bool includeSubdirectories = true, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.IsRootDirectory) throw new NotSupportedException("The complete DMS root cannot be copied.");
            EnsureSameCopyContext(source);
            var name = newDirName ?? source.Name;
            ValidateCopyDirectoryName(name);
            var sourceKey = CopyDirectoryKey(source);
            var ancestors = new HashSet<string>(StringComparer.Ordinal);
            for (var ancestor = this; ancestor != null && !ancestor.IsRootDirectory; ancestor = ancestor.ParentDirectory)
            {
                var key = CopyDirectoryKey(ancestor);
                if (key == sourceKey) throw new ArgumentException("A directory cannot be copied into itself or a descendant.", nameof(source));
                if (!ancestors.Add(key)) throw new IOException("The destination parent chain repeats a directory identity.");
            }
            var existing = await GetDirectoriesAsync(cancellationToken).ConfigureAwait(false);
            if (existing.Any(child => child.Name == name))
                throw new IOException("The destination child directory already exists.");
            var sourceIdentities = new HashSet<string>(StringComparer.Ordinal);
            var plan = await PlanDirectoryCopyAsync(source, includeSubdirectories, sourceIdentities, cancellationToken).ConfigureAwait(false);
            sourceIdentities.UnionWith(ancestors);
            foreach (var child in existing) sourceIdentities.Add(CopyDirectoryKey(child));
            cancellationToken.ThrowIfCancellationRequested();
            var destination = await CreateCopyChildAsync(name, cancellationToken).ConfigureAwait(false);
            ValidateCreatedCopyDirectory(destination, this, sourceIdentities);
            await ApplyDirectoryCopyAsync(plan, destination, sourceIdentities, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Creates a new immediate copy destination without inheriting source metadata.</summary>
        /// <param name="name">The validated child name.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The newly confirmed collection or folder with its correct parent.</returns>
        /// <remarks>Invalidates the child listing even after uncertain failures. Overrides must not replay ambiguous creation or return an existing directory.</remarks>
        protected virtual async Task<DirectoryInfo> CreateCopyChildAsync(string name, CancellationToken cancellationToken)
        {
            try
            {
                if (IsRootDirectory)
                {
                    var result = await ioClient.ApiClient.Collections.CreateCollectionAsync(ioClient.CurrentAuthenticationContextUserID, name, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(result?.Id)) throw new InvalidResponseDataException("The collection creation returned no identity.", new InvalidDataException("A new collection ID is required."));
                    return new DirectoryInfo(ioClient, this, new Collection { Id = result.Id, Name = name });
                }
                var folder = await ioClient.ApiClient.Folders.CreateFolderAsync(ioClient.CurrentAuthenticationContextUserID, name, CollectionID, FolderID, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(folder?.Id)) throw new InvalidResponseDataException("The folder creation returned no identity.", new InvalidDataException("A new folder ID is required."));
                return new DirectoryInfo(ioClient, this, new Folder { Id = folder.Id, Name = name, Collection = AssociatedCollection.CollectionID, Parent = FolderID ?? Rest.RestApiConstants.NONE });
            }
            finally { ResetDirectoriesCache(); }
        }

        /// <summary>Copies immediate documents into this directory through the server copy command.</summary>
        /// <param name="documents">The immutable source and filename selections.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completed after the server confirms one new document for each selection.</returns>
        /// <remarks>Invalidates the file listing after any outcome. Overrides must preserve source references and must not replay uncertain writes.</remarks>
        protected virtual async Task CopyDirectoryFilesAsync(IReadOnlyList<DocumentCopyRequest> documents, CancellationToken cancellationToken)
        {
            try
            {
                var result = await ioClient.ApiClient.Documents.CopyDocumentsAsync(ioClient.CurrentAuthenticationContextUserID, documents,
                    FolderID == null ? CollectionID : null, FolderID, cancellationToken).ConfigureAwait(false);
                if (result.Count != documents.Count) throw new InvalidResponseDataException("The server did not confirm every requested copy.", new InvalidDataException("The number of new documents differs from the source selection count."));
            }
            finally { ResetFilesCache(); }
        }

        private async Task<CopyPlan> PlanDirectoryCopyAsync(DirectoryInfo source, bool recursive, HashSet<string> visited, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!visited.Add(CopyDirectoryKey(source))) throw new IOException("The source structure repeats a directory identity.");
            var result = new CopyPlan { Name = source.Name, Documents = await SnapshotCopyDocumentsAsync(source, token).ConfigureAwait(false) };
            if (recursive)
            {
                var children = await source.GetDirectoriesAsync(token).ConfigureAwait(false);
                foreach (var child in children)
                {
                    ValidateCopyDirectoryName(child.Name);
                    result.Children.Add(await PlanDirectoryCopyAsync(child, true, visited, token).ConfigureAwait(false));
                }
            }
            token.ThrowIfCancellationRequested();
            return result;
        }

        private static async Task<DocumentCopyRequest[]> SnapshotCopyDocumentsAsync(DirectoryInfo source, CancellationToken token)
        {
            const int pageSize = 500;
            var documents = new List<DocumentCopyRequest>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int? total = null;
            do
            {
                token.ThrowIfCancellationRequested();
                var page = await source.ioClient.LookupChildDocumentPageAsync(source.CollectionID,
                    source.FolderID ?? Rest.RestApiConstants.NONE, documents.Count, pageSize, token).ConfigureAwait(false);
                if (page?.Documents == null || page.Hits < 0 || (total.HasValue && total.Value != page.Hits) ||
                    page.Documents.Count > pageSize || (long)documents.Count + page.Documents.Count > page.Hits ||
                    (page.Documents.Count == 0 && documents.Count < page.Hits))
                    throw new IOException("The source document listing is incomplete or changed during copy planning.");
                total = page.Hits;
                foreach (var document in page.Documents)
                {
                    if (document == null || string.IsNullOrWhiteSpace(document.Id))
                        throw new IOException("The source document listing has no usable identity.");
                    var id = Guid.TryParse(document.Id, out var guid) ? guid.ToString("D") : document.Id;
                    if (!identities.Add(id)) throw new IOException("The source document listing repeats an identity during copy planning.");
                    documents.Add(new DocumentCopyRequest(document.Id, document.Filename));
                }
            }
            while (documents.Count < total.Value);
            return documents.ToArray();
        }

        private static async Task ApplyDirectoryCopyAsync(CopyPlan source, DirectoryInfo destination, HashSet<string> usedIdentities, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (source.Documents.Length > 0) await destination.CopyDirectoryFilesAsync(Array.AsReadOnly(source.Documents), token).ConfigureAwait(false);
            foreach (var child in source.Children)
            {
                token.ThrowIfCancellationRequested();
                var created = await destination.CreateCopyChildAsync(child.Name, token).ConfigureAwait(false);
                ValidateCreatedCopyDirectory(created, destination, usedIdentities);
                await ApplyDirectoryCopyAsync(child, created, usedIdentities, token).ConfigureAwait(false);
            }
        }

        private static void ValidateCreatedCopyDirectory(DirectoryInfo created, DirectoryInfo parent, HashSet<string> usedIdentities)
        {
            if (created == null || !ReferenceEquals(created.ParentDirectory, parent) || !usedIdentities.Add(CopyDirectoryKey(created)))
                throw new InvalidResponseDataException("The copy destination is not a new child directory.", new InvalidDataException("A copy cannot reuse source, existing or previously created directory identities."));
        }

        private void ValidateCopyDirectoryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name == "." || name == ".." || name.Contains(ioClient.Paths.DirectorySeparatorChar.ToString()) || name.Contains(ioClient.Paths.AltDirectorySeparatorChar.ToString()))
                throw new ArgumentException("A copy directory name must be one nonempty path segment.", nameof(name));
        }

        private void EnsureSameCopyContext(DirectoryInfo source)
            => EnsureSameResourceContext(source.ioClient);

        internal void EnsureSameResourceContext(IOClientBase sourceClient)
        {
            if (ReferenceEquals(sourceClient, ioClient)) return;
            // A matching bridge URL/user alone does not prove the same selected tenant.
            if (ioClient.ApiClient == null || !ReferenceEquals(sourceClient.ApiClient, ioClient.ApiClient) ||
                !string.Equals(sourceClient.CurrentAuthenticationContextUserID, ioClient.CurrentAuthenticationContextUserID, StringComparison.Ordinal))
                throw new NotSupportedException("Resource transfers require the same REST client and authenticated context.");
        }

        private static string CopyDirectoryKey(DirectoryInfo directory)
        {
            var id = directory.FolderID ?? directory.CollectionID;
            if (string.IsNullOrWhiteSpace(id)) throw new NotSupportedException("A directory identity is required for copying.");
            if (Guid.TryParse(id, out var guid)) id = guid.ToString("D");
            return (directory.FolderID != null ? "folder:" : "collection:") + id;
        }

        private sealed class CopyPlan
        {
            internal string Name;
            internal DocumentCopyRequest[] Documents;
            internal readonly List<CopyPlan> Children = new List<CopyPlan>();
        }
    }
}
