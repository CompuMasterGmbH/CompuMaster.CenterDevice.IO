using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UserSettingsRestClient
    {
        /// <summary>Retrieves immutable selected own-user settings.</summary>
        /// <inheritdoc cref="GetUserSettingsDetailsAsync"/>
        public UserSettingsDetails GetUserSettingsDetails(string userId, string id, IEnumerable<string> data = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetUserSettingsDetailsAsync(userId, id, data, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves immutable selected own-user settings using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected own user identifier, or current.</param>
        /// <param name="data">The nonempty individual field selection, or null to retain the server default of all.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original nullable settings with complete defensive copies of configuration objects.</returns>
        /// <remarks>The server permits reading only the caller's settings. Read-only tenant indications retain their conditional permission meaning. Existing getters and models remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected user or supplied field selection is unusable.</exception>
        /// <exception cref="InvalidResponseDataException">Successful settings have invalid JSON or known-field structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<UserSettingsDetails> GetUserSettingsDetailsAsync(string userId, string id, IEnumerable<string> data = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); RequireSettingsUser(id);
            var fields = data?.ToArray();
            if (fields != null && (fields.Length == 0 || fields.Any(value => string.IsNullOrWhiteSpace(value) || value.Contains(","))))
                throw new ArgumentException("A supplied data selection must contain nonblank individual field names.", nameof(data));
            var request = CreateRestRequest(SettingsPath(id), Method.Get);
            if (fields != null) request.AddQueryParameter("data", string.Join(",", fields));
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                using (var source = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the settings.");
                    return new UserSettingsDetails(root);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException)
            { throw new InvalidResponseDataException("The selected user settings have invalid JSON or structure.", error); }
        }

        /// <summary>Updates explicitly selected own-user settings.</summary>
        /// <inheritdoc cref="UpdateUserSettingsAsync"/>
        public void UpdateUserSettings(string userId, string id, UserSettingsUpdateRequest settings, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateUserSettingsAsync(userId, id, settings, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates explicitly selected own-user settings using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected own user identifier, or current.</param>
        /// <param name="settings">The explicit settings copied before authorization. Omitted fields remain unchanged; supplied configurations must be complete except individual favorites and selected notification channels.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server permits updating only the caller's settings. Setting use-search-history to false also clears stored history. No existing configuration is fetched, merged or guessed, and no new mutation retry is added. Cancellation cannot establish that no update occurred.</remarks>
        /// <exception cref="ArgumentNullException">The settings are null.</exception>
        /// <exception cref="ArgumentException">The selected user or complete known setting structure is unusable, or read-only tenant settings are supplied.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task UpdateUserSettingsAsync(string userId, string id, UserSettingsUpdateRequest settings, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); RequireSettingsUser(id);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var body = settings.SnapshotForSend(); var request = CreateRestRequest(SettingsPath(id), Method.Put, "application/json");
            request.AddStringBody(body, RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        private string SettingsPath(string id) => ApiVersionPrefix + "user/" + Uri.EscapeDataString(id) + "/settings";
        private static void RequireSettingsUser(string id)
        { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A selected own-user identifier is required.", nameof(id)); }
    }
}
