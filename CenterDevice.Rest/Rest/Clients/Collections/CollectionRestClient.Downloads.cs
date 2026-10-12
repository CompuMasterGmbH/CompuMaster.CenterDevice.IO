using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Downloads the selected collection as a ZIP archive.</summary>
        /// <inheritdoc cref="DownloadCollectionAsZipAsync"/>
        public Stream DownloadCollectionAsZip(string userId, string collectionId, bool includeFolderStructure = false, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadCollectionAsZipAsync(userId, collectionId, includeFolderStructure, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens a collection ZIP archive using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The selected collection identifier.</param>
        /// <param name="includeFolderStructure">Includes the collection's folder structure when true; the default archive contains documents without folder structure.</param>
        /// <param name="cancellationToken">Cancels authorization, queue admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned stream that the caller must dispose.</returns>
        /// <remarks>The server supplies the latest original representations of visible documents, may rename duplicate filenames and may omit a content length while generating the archive. This method does not extract the archive. Requests are not automatically redirected or replayed after transient failures.</remarks>
        /// <exception cref="ArgumentException">The collection identifier is missing or is a relative path segment.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<Stream> DownloadCollectionAsZipAsync(string userId, string collectionId, bool includeFolderStructure = false, CancellationToken cancellationToken = default(CancellationToken))
            => OpenResourceZipAsync(userId, "collection", collectionId, new JObject
            {
                ["action"] = "download",
                ["params"] = new JObject { ["include-folder-structure"] = includeFolderStructure }
            }, cancellationToken);
    }
}
