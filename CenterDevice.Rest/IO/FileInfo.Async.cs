using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public partial class FileInfo
    {
        /// <summary>Opens a streaming document download using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="version">The version to retrieve, or zero for the current version.</param>
        /// <param name="cancellationToken">Cancels admission, active HTTP I/O, and subsequent reads of the returned stream.</param>
        /// <returns>A response-owning stream that the caller must dispose.</returns>
        public virtual Task<Stream> DownloadAsync(long version = 0, CancellationToken cancellationToken = default(CancellationToken)) =>
            ioClient.ApiClient.Document.DownloadDocumentAsync(ioClient.CurrentAuthenticationContextUserID, ID,
                version == 0 ? (long?)null : version, null, cancellationToken);

        /// <summary>Downloads a document to disk using bounded asynchronous stream copying.</summary>
        /// <param name="targetFileName">The target path, which is created or overwritten.</param>
        /// <param name="version">The version to retrieve, or zero for the current version.</param>
        /// <param name="cancellationToken">Cancels HTTP and local stream I/O.</param>
        /// <returns>A task representing the completed download and modification-time update.</returns>
        /// <remarks>A failure or cancellation can leave a partial target file, matching the existing overwrite contract. The modification timestamp is updated only after success.</remarks>
        public async Task DownloadAsync(string targetFileName, long version = 0, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var source = await DownloadAsync(version, cancellationToken).ConfigureAwait(false))
            using (var target = new FileStream(targetFileName, FileMode.Create, FileAccess.Write, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await source.CopyToAsync(target, 81920, cancellationToken).ConfigureAwait(false);
                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (ModificationDate.HasValue) System.IO.File.SetLastWriteTimeUtc(targetFileName, ModificationDate.Value);
        }

        /// <summary>Uploads a new version from a local file using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="localPath">The existing local file path.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing upload completion and file-cache invalidation.</returns>
        public async Task UploadNewVersionAsync(string localPath, CancellationToken cancellationToken = default(CancellationToken))
        {
            try { await ioClient.ApiClient.Document.UploadNewVersionAsync(ioClient.CurrentAuthenticationContextUserID, ID, FileName, localPath, cancellationToken).ConfigureAwait(false); }
            finally { parentDirectory?.ResetFilesCache(); }
        }

        /// <summary>Uploads a new version from a stream factory using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="fileDataStream">A factory returning a fresh readable stream at its beginning with an available length. The SDK owns each returned stream.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing upload completion and file-cache invalidation.</returns>
        public async Task UploadNewVersionAsync(Func<Stream> fileDataStream, CancellationToken cancellationToken = default(CancellationToken))
        {
            try { await ioClient.ApiClient.Document.UploadNewVersionAsync(ioClient.CurrentAuthenticationContextUserID, ID, FileName, fileDataStream, cancellationToken).ConfigureAwait(false); }
            finally { parentDirectory?.ResetFilesCache(); }
        }

        /// <summary>Deletes this document using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing deletion and file-cache invalidation.</returns>
        public async Task DeleteAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            try { await ioClient.ApiClient.Document.DeleteDocumentAsync(ioClient.CurrentAuthenticationContextUserID, ID, cancellationToken).ConfigureAwait(false); }
            finally { parentDirectory?.ResetFilesCache(); }
        }

        /// <summary>Renames this document using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="targetFileName">The replacement file name.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the rename and local metadata/cache update.</returns>
        public async Task RenameAsync(string targetFileName, CancellationToken cancellationToken = default(CancellationToken))
        {
            try { await ioClient.ApiClient.Document.RenameDocumentAsync(ioClient.CurrentAuthenticationContextUserID, ID, targetFileName, cancellationToken).ConfigureAwait(false); }
            finally { parentDirectory?.ResetFilesCache(); }
            fileName = targetFileName;
        }

        /// <summary>Moves this document to another directory using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="targetDirectory">The target collection or folder using the same REST client and authenticated context.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the move and local parent/cache update.</returns>
        /// <remarks>Retains document identity. Both file listings are invalidated after success or uncertain failure; the local parent changes only after server confirmation. Requests are not automatically replayed and cancellation does not establish whether a submitted move committed.</remarks>
        /// <exception cref="ArgumentNullException">The target directory is null.</exception>
        /// <exception cref="NotSupportedException">The target is the root directory or belongs to another authenticated instance.</exception>
        public async Task MoveAsync(DirectoryInfo targetDirectory, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (targetDirectory == null) throw new ArgumentNullException(nameof(targetDirectory));
            if (targetDirectory.IsRootDirectory) throw new NotSupportedException("Moving a file into root directory is not supported");
            targetDirectory.EnsureSameResourceContext(ioClient);
            try
            {
                await MoveCoreAsync(targetDirectory, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                parentDirectory.ResetFilesCache();
                targetDirectory.ResetFilesCache();
            }
            parentDirectory = targetDirectory;
        }

        /// <summary>Moves the selected document through the native server command.</summary>
        /// <param name="targetDirectory">The validated target collection or folder.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completed after the server confirms the move.</returns>
        /// <remarks>The caller handles cache invalidation and local parent publication. Overrides must preserve the document ID and must not replay uncertain writes.</remarks>
        protected virtual Task MoveCoreAsync(DirectoryInfo targetDirectory, CancellationToken cancellationToken)
            => ioClient.ApiClient.Documents.MoveDocumentsAsync(ioClient.CurrentAuthenticationContextUserID, new[] { ID },
                ParentDirectory.AssociatedCollection.CollectionID, parentDirectory.FolderID,
                targetDirectory.AssociatedCollection.CollectionID, targetDirectory.FolderID, cancellationToken);
    }
}
