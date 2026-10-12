using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantSettingsRestClient
    {
        /// <summary>Retrieves immutable original tenant settings.</summary>
        /// <inheritdoc cref="GetTenantSettingsDetailsAsync"/>
        public TenantSettingsDetails GetTenantSettingsDetails(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
            => GetTenantSettingsDetailsAsync(userId, id, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves immutable original tenant settings using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected tenant identifier, or current.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Complete original setting values and nullable editability indications.</returns>
        /// <remarks>The server restricts this resource to administrators. Read-only or empty selections do not establish caller permissions or remove minimum owner rights. Existing getters and models remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The selected tenant is unusable.</exception>
        /// <exception cref="InvalidResponseDataException">Successful metadata has invalid JSON or known setting structure.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<TenantSettingsDetails> GetTenantSettingsDetailsAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); RequireSettingsTenant(id);
            var request = CreateRestRequest(SettingsTenantPath(id), Method.Get);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                using (var source = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the tenant settings.");
                    return new TenantSettingsDetails(root);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is ArgumentException)
            { throw new InvalidResponseDataException("The tenant settings have invalid JSON or structure.", error); }
        }
        /// <summary>Updates explicitly selected complete tenant settings.</summary>
        /// <inheritdoc cref="UpdateTenantSettingsAsync(string,string,TenantSettingsUpdateRequest,CancellationToken)"/>
        public void UpdateTenantSettings(string userId, string id, TenantSettingsUpdateRequest settings, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateTenantSettingsAsync(userId, id, settings, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates explicitly selected complete tenant settings using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The explicitly selected tenant identifier, or current.</param>
        /// <param name="settings">The explicit update copied before authorization; known selected settings require complete users and roles arrays, and omitted settings remain unchanged.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server enforces administrator access, setting editability, role/user restrictions and minimum owner rights. MFA requirements can temporarily revoke selected users' tenant access. No current settings are fetched, defaults inserted or speculative updates replayed. Cancellation cannot establish that no update occurred. Existing single-setting overloads retain their previous behavior.</remarks>
        /// <exception cref="ArgumentNullException">The update is null.</exception>
        /// <exception cref="ArgumentException">The selected tenant or complete known setting shape is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task UpdateTenantSettingsAsync(string userId, string id, TenantSettingsUpdateRequest settings, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); RequireSettingsTenant(id);
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var body = settings.SnapshotForSend(); var request = CreateRestRequest(SettingsTenantPath(id), Method.Put, "application/json");
            request.AddStringBody(body, RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
        private string SettingsTenantPath(string id) => ApiVersionPrefix + "tenant/" + Uri.EscapeDataString(id) + "/settings";
        private static void RequireSettingsTenant(string id)
        { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A selected tenant identifier is required.", nameof(id)); }
    }
}
