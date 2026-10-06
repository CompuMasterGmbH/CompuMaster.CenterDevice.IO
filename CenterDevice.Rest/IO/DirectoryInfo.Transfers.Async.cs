using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public partial class DirectoryInfo
    {
        /// <summary>Uploads a new document from a stream factory using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="fileDataStream">A factory returning a fresh readable stream at its beginning with an available length. The SDK owns each returned stream.</param>
        /// <param name="fileName">The new document name; an existing document may remain as a name collision.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing upload completion.</returns>
        /// <remarks>The file cache is invalidated even after uncertain failures so a caller can reconcile server state before retrying.</remarks>
        public virtual async Task UploadAndCreateNewFileAsync(Func<Stream> fileDataStream, string fileName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory) throw new InvalidOperationException("Upload to root directory not allowed");
            try
            {
                await ioClient.ApiClient.Documents.UploadDocumentAsync(ioClient.CurrentAuthenticationContextUserID, fileName, fileDataStream,
                    CollectionID ?? ParentCollection.CollectionID, FolderID, cancellationToken).ConfigureAwait(false);
            }
            finally { ResetFilesCache(); }
        }

        /// <summary>Uploads a new document from disk using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="localPath">The existing local file path.</param>
        /// <param name="fileName">The new document name; an existing document may remain as a name collision.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing upload completion.</returns>
        public Task UploadAndCreateNewFileAsync(string localPath, string fileName, CancellationToken cancellationToken = default(CancellationToken)) =>
            UploadAndCreateNewFileAsync(() => new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan), fileName, cancellationToken);

        /// <summary>Copies a document using bounded asynchronous disk staging and upload.</summary>
        /// <param name="source">The source document.</param>
        /// <param name="newFileName">The target name, or null to retain the source name.</param>
        /// <param name="cancellationToken">Cancels lookup, download, local staging, or upload.</param>
        /// <returns>A task representing the completed copy and staging-file cleanup.</returns>
        /// <remarks>The download is completely consumed and disposed before upload to avoid holding a shared origin permit during another request. Memory use is bounded; sufficient temporary disk space is required. There is no client-side two-gigabyte array limit. Actual backend size limits remain independent. An ambiguous upload failure requires reconciliation, not an automatic replay.</remarks>
        /// <exception cref="Model.Exceptions.FileAlreadyExistsException">The target name already exists.</exception>
        /// <exception cref="InvalidOperationException">The target is the root directory.</exception>
        public async Task AddCopyAsync(FileInfo source, string newFileName = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory) throw new InvalidOperationException("Upload to root directory not allowed");
            var targetName = newFileName ?? source.FileName;
            if (await TryGetFileAsync(targetName, cancellationToken).ConfigureAwait(false) != null)
                throw new Model.Exceptions.FileAlreadyExistsException(ioClient.Paths.CombinePath(Path, targetName));
            var stagingPath = System.IO.Path.GetTempFileName();
            Exception primaryFailure = null;
            try
            {
                await source.DownloadAsync(stagingPath, 0, cancellationToken).ConfigureAwait(false);
                // Opening the next request only after disposal releases the download's origin lease.
                await UploadAndCreateNewFileAsync(stagingPath, targetName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) { primaryFailure = exception; throw; }
            finally
            {
                try { System.IO.File.Delete(stagingPath); }
                catch (Exception cleanupFailure)
                {
                    if (primaryFailure == null) throw;
                    primaryFailure.Data["TemporaryFileCleanupFailure"] = cleanupFailure;
                }
            }
        }
    }
}
