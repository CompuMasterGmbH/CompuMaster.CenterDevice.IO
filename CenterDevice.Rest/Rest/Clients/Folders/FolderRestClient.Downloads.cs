using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FolderRestClient
    {
        /// <summary>Downloads the selected folder as a ZIP archive.</summary>
        /// <inheritdoc cref="DownloadFolderAsZipAsync"/>
        public Stream DownloadFolderAsZip(string userId, string folderId, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadFolderAsZipAsync(userId, folderId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens a folder ZIP archive using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="folderId">The selected folder identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, queue admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned stream that the caller must dispose.</returns>
        /// <remarks>The server supplies the latest original representations of visible documents with folder structure and may omit a content length while generating the archive. This method does not extract the archive. Requests are not automatically redirected or replayed after transient failures.</remarks>
        /// <exception cref="ArgumentException">The folder identifier is missing or is a relative path segment.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<Stream> DownloadFolderAsZipAsync(string userId, string folderId, CancellationToken cancellationToken = default(CancellationToken))
            => OpenResourceZipAsync(userId, "folder", folderId, new { action = "download" }, cancellationToken);
    }
}
