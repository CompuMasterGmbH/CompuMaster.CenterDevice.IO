using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        private static string DatedVersionMetadata(string filename, long length, DateTime? documentDate)
        {
            var metadata = JObject.Parse(VersionMetadata(filename, length));
            if (documentDate.HasValue)
                metadata["metadata"]["document"][RestApiConstants.DOCUMENT_DATE] = NormalizeUploadDate(documentDate.Value);
            return metadata.ToString(Formatting.None);
        }

        internal static DateTime NormalizeUploadDate(DateTime date)
            => date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime();

        /// <summary>Uploads a local file as a new version with an explicit document date.</summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="id">The existing document identifier.</param>
        /// <param name="filename">The document filename.</param>
        /// <param name="filepath">The local source path.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        /// <param name="token">Cancels admission and active HTTP I/O.</param>
        /// <returns>The server-confirmed new version.</returns>
        public NewVersionUploadResponse UploadNewVersion(string userId, string id, string filename, string filepath, DateTime? documentDate, CancellationToken token)
            => UploadNewVersionAsync(userId, id, filename, filepath, documentDate, token).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Uploads streamed content as a new version with an explicit document date.</summary>
        /// <param name="userId">The authenticated user identifier.</param>
        /// <param name="id">The existing document identifier.</param>
        /// <param name="filename">The document filename.</param>
        /// <param name="fileDataStream">Creates a readable stream with an available length; the SDK owns the stream.</param>
        /// <param name="documentDate">The document date; local times are converted to UTC, unspecified times are treated as UTC, and null preserves the server default.</param>
        /// <param name="token">Cancels admission and active HTTP I/O.</param>
        /// <returns>The server-confirmed new version.</returns>
        public NewVersionUploadResponse UploadNewVersion(string userId, string id, string filename, Func<Stream> fileDataStream, DateTime? documentDate, CancellationToken token)
            => UploadNewVersionAsync(userId, id, filename, fileDataStream, documentDate, token).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
