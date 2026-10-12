using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UserRestClient
    {
        /// <summary>Updates explicitly selected user profile fields.</summary>
        /// <inheritdoc cref="UpdateUserDetailsAsync"/>
        public void UpdateUserDetails(string userId, string id, UserUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateUserDetailsAsync(userId, id, request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates explicitly selected user profile fields using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected user identifier; the server permits updating only the caller's own profile.</param>
        /// <param name="request">The nonempty field selection, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>Null fields stay unchanged. Picture data uses Base64-encoded PNG/JPEG of at most 5 MiB. The server enforces own-profile access, image validity and dimension scaling to 400 by 400 pixels. Null PictureData does not remove a picture. No user name, ID or permissions are inferred. Uncertain writes are not replayed; existing expired-token refresh remains inherited. Cancellation cannot establish that no change occurred. Existing getters, interfaces and default fields remain unchanged.</remarks>
        /// <exception cref="ArgumentNullException">The request is null.</exception>
        /// <exception cref="ArgumentException">The selected user or profile values are unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task UpdateUserDetailsAsync(string userId, string id, UserUpdateRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null) throw new ArgumentNullException(nameof(request));
            var first = request.FirstName; var last = request.LastName; var picture = request.PictureData;
            var body = new JObject();
            if (first != null) body["first-name"] = first;
            if (last != null) body["last-name"] = last;
            if (picture != null)
            {
                byte[] decoded;
                try { decoded = Convert.FromBase64String(picture); }
                catch (FormatException error) { throw new ArgumentException("Picture data must use Base64 encoding.", nameof(request), error); }
                if (decoded.Length == 0 || decoded.Length > 5 * 1024 * 1024)
                    throw new ArgumentException("Picture data must contain at most 5 MiB of nonempty decoded bytes.", nameof(request));
                body["picture"] = new JObject { ["data"] = picture };
            }
            if (!body.HasValues) throw new ArgumentException("Select at least one user profile field.", nameof(request));
            return SendUserProfileAsync(userId, id, body, cancellationToken);
        }

        /// <summary>Removes the selected user's picture explicitly.</summary>
        /// <inheritdoc cref="RemoveUserPictureAsync"/>
        public void RemoveUserPicture(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveUserPictureAsync(userId, id, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes the selected user's picture using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected user identifier; the server permits removing only the caller's own picture.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>A task completing after the server confirms HTTP 204.</returns>
        /// <remarks>An explicit empty picture object removes the image while preserving names. Own-profile access is server-enforced. Uncertain writes are not replayed; existing expired-token refresh remains inherited. Cancellation cannot establish that no change occurred.</remarks>
        /// <exception cref="ArgumentException">The selected user is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task RemoveUserPictureAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SendUserProfileAsync(userId, id, new JObject { ["picture"] = new JObject() }, cancellationToken);
        }

        private async Task SendUserProfileAsync(string userId, string id, JObject body, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..")
                throw new ArgumentException("A selected user identifier is required.", nameof(id));
            var request = CreateRestRequest(URI_RESOURCE + "/" + Uri.EscapeDataString(id), Method.Put, "application/json");
            request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
