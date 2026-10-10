using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CenterDevice.Rest.Clients.Documents;

namespace CenterDevice.IO
{
    public partial class DirectoryInfo
    {
        /// <summary>Uploads a new document with an explicit document date.</summary>
        /// <param name="fileDataStream">Creates a readable stream with an available length; the SDK owns the stream.</param>
        /// <param name="fileName">The remote document name.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing server-confirmed completion and cache invalidation.</returns>
        public virtual async Task UploadAndCreateNewFileAsync(Func<Stream> fileDataStream, string fileName, DateTime? documentDate, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory) throw new InvalidOperationException("Upload to root directory not allowed");
            try
            {
                await ioClient.ApiClient.Documents.UploadDocumentAsync(ioClient.CurrentAuthenticationContextUserID, fileName, fileDataStream,
                    documentDate.HasValue ? (DateTime?)DocumentRestClient.NormalizeUploadDate(documentDate.Value) : null,
                    new List<string> { CollectionID ?? ParentCollection.CollectionID }, new List<string> { FolderID }, cancellationToken).ConfigureAwait(false);
            }
            finally { ResetFilesCache(); }
        }

        /// <summary>Uploads a local file with an explicit document date.</summary>
        /// <param name="localPath">The existing source file path.</param>
        /// <param name="fileName">The remote document name.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing server-confirmed completion and cache invalidation.</returns>
        public Task UploadAndCreateNewFileAsync(string localPath, string fileName, DateTime? documentDate, CancellationToken cancellationToken)
            => UploadAndCreateNewFileAsync(() => new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan), fileName, documentDate, cancellationToken);

        /// <summary>Uploads a local file with an explicit document date synchronously.</summary>
        /// <param name="localPath">The existing source file path.</param>
        /// <param name="fileName">The remote document name.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        public void UploadAndCreateNewFile(string localPath, string fileName, DateTime? documentDate)
            => UploadAndCreateNewFileAsync(localPath, fileName, documentDate, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public partial class FileInfo
    {
        /// <summary>Uploads a new version with an explicit document date.</summary>
        /// <param name="fileDataStream">Creates a readable stream with an available length; the SDK owns the stream.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing server-confirmed completion and cache invalidation.</returns>
        public async Task UploadNewVersionAsync(Func<Stream> fileDataStream, DateTime? documentDate, CancellationToken cancellationToken)
        {
            try { await ioClient.ApiClient.Document.UploadNewVersionAsync(ioClient.CurrentAuthenticationContextUserID, ID, FileName, fileDataStream, documentDate, cancellationToken).ConfigureAwait(false); }
            finally { parentDirectory?.ResetFilesCache(); }
        }

        /// <summary>Uploads a local file as a new version with an explicit document date.</summary>
        /// <param name="localPath">The existing source file path.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing server-confirmed completion and cache invalidation.</returns>
        public async Task UploadNewVersionAsync(string localPath, DateTime? documentDate, CancellationToken cancellationToken)
        {
            try { await ioClient.ApiClient.Document.UploadNewVersionAsync(ioClient.CurrentAuthenticationContextUserID, ID, FileName, localPath, documentDate, cancellationToken).ConfigureAwait(false); }
            finally { parentDirectory?.ResetFilesCache(); }
        }

        /// <summary>Uploads a local file as a new version with an explicit document date synchronously.</summary>
        /// <param name="localPath">The existing source file path.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        public void UploadNewVersion(string localPath, DateTime? documentDate)
            => UploadNewVersionAsync(localPath, documentDate, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
