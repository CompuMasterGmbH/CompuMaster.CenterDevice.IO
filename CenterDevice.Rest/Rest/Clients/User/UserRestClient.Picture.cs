using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CenterDevice.Rest.Exceptions;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UserRestClient
    {
        /// <summary>Downloads the selected user's profile picture.</summary>
        /// <inheritdoc cref="DownloadUserPictureAsync"/>
        public Stream DownloadUserPicture(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
            => DownloadUserPictureAsync(userId, id, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Opens the selected user's profile picture using asynchronous streaming HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission, HTTP I/O and the returned stream.</param>
        /// <returns>An unbuffered response-owned PNG or JPEG stream that the caller must dispose.</returns>
        /// <remarks>The server enforces user visibility and picture availability. The original image bytes are returned without Base64 conversion. Redirects are not followed; an absent picture is not replaced with an empty stream.</remarks>
        /// <exception cref="ArgumentException">The selected user identifier is missing or a relative path segment.</exception>
        /// <exception cref="InvalidResponseDataException">A successful response does not contain the documented PNG or JPEG media type.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<Stream> DownloadUserPictureAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..")
                throw new ArgumentException("A selected user identifier is required.", nameof(id));
            var uri = BuildResourceDownloadUri(URI_RESOURCE + "/" + Uri.EscapeDataString(id) + "/picture");
            return await OpenResourceDownloadAsync(userId, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
                return request;
            }, HttpStatusCode.OK, new[] { "image/png", "image/jpeg" }, false, cancellationToken).ConfigureAwait(false);
        }
    }
}
