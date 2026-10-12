using CenterDevice.Rest.Clients.OAuth;
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

namespace CenterDevice.Rest.Clients.UploadEmails
{
    /// <summary>Manages upload-email endpoints created by the authenticated caller.</summary>
    /// <remarks>The server enforces creator visibility, destination visibility and its ten-endpoint creation limit. Management does not send e-mail or import documents. Returned import addresses are confidential and are not included in client validation diagnostics.</remarks>
    public class UploadEmailsRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes an upload-email client using the existing authorization and transport.</summary>
        /// <param name="oauthInfoProvider">The authorization provider.</param>
        /// <param name="configuration">The origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public UploadEmailsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Creates an upload-email endpoint and returns its confirmed identifier.</summary>
        /// <inheritdoc cref="CreateUploadEmailAsync"/>
        public string CreateUploadEmail(string userId, string email, UploadEmailOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => CreateUploadEmailAsync(userId, email, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates an upload-email endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="email">The exact nonblank import address requested; format and availability are server-validated.</param>
        /// <param name="options">The nonnull options with a nonnull Name; all selections are snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new endpoint identifier.</returns>
        /// <remarks>Omitted storage/extraction/indexing fields retain documented creation defaults. Creation is non-idempotent; uncertain outcomes are not replayed. This does not send mail.</remarks>
        /// <exception cref="ArgumentException">Required data or a known option combination is invalid.</exception>
        /// <exception cref="InvalidResponseDataException">The success response does not confirm usable identity.</exception>
        public async Task<string> CreateUploadEmailAsync(string userId, string email, UploadEmailOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("A nonblank import address is required.", nameof(email));
            var body = Snapshot(options, true); body["email"] = email;
            var response = await SendAsync(userId, ApiVersionPrefix + "upload-emails", Method.Post, body, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => ParsedId(root["id"]));
        }

        /// <summary>Retrieves an upload-email endpoint created by the caller.</summary>
        /// <inheritdoc cref="GetUploadEmailAsync"/>
        public UploadEmail GetUploadEmail(string userId, string uploadEmailId, CancellationToken cancellationToken = default(CancellationToken))
            => GetUploadEmailAsync(userId, uploadEmailId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves a selected upload-email endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="uploadEmailId">The exact nonblank endpoint identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original metadata and the confidential import address, without inferred ownership or defaults.</returns>
        /// <exception cref="InvalidResponseDataException">Metadata is malformed or identity differs from the selection.</exception>
        public async Task<UploadEmail> GetUploadEmailAsync(string userId, string uploadEmailId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, Resource(uploadEmailId), Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => { var result = Record(root); if (Identity(result.Id) != Identity(uploadEmailId)) throw new InvalidDataException("The endpoint identity differs from the selection."); return result; });
        }

        /// <summary>Retrieves the caller's own upload-email endpoints.</summary>
        /// <inheritdoc cref="GetUploadEmailsAsync"/>
        public IReadOnlyList<UploadEmail> GetUploadEmails(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => GetUploadEmailsAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the caller's own upload-email endpoints using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only list preserving server order and original metadata.</returns>
        /// <remarks>The manufacturer defines an own-endpoint list without paging or total metadata. The ten-endpoint creation limit is enforced by the server, without a client completeness inference.</remarks>
        /// <exception cref="InvalidResponseDataException">The list has malformed entries or repeated identities.</exception>
        public async Task<IReadOnlyList<UploadEmail>> GetUploadEmailsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, ApiVersionPrefix + "upload-emails", Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse<IReadOnlyList<UploadEmail>>(response.Content, root =>
            {
                if (!(root["upload-emails"] is JArray entries)) throw new InvalidDataException("An endpoint array is required.");
                var result = new List<UploadEmail>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    if (!(entry is JObject item)) throw new InvalidDataException("An endpoint must be an object.");
                    var endpoint = Record(item); if (!ids.Add(Identity(endpoint.Id))) throw new InvalidDataException("Repeated endpoint identity."); result.Add(endpoint);
                }
                return result.AsReadOnly();
            });
        }

        /// <summary>Updates selected upload-email fields.</summary>
        /// <inheritdoc cref="UpdateUploadEmailAsync"/>
        public void UpdateUploadEmail(string userId, string uploadEmailId, UploadEmailOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateUploadEmailAsync(userId, uploadEmailId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates selected upload-email fields using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="uploadEmailId">The exact nonblank owned endpoint identifier.</param>
        /// <param name="options">The nonnull partial selection; omitted fields remain unchanged and empty arrays clear corresponding lists.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed success.</returns>
        /// <remarks>The import address is not updateable. Switching a nonempty whitelist/blacklist requires explicitly clearing the other list. Unknown existing storage/extraction/filter state is server-validated; creation defaults are not applied to updates. Uncertain writes are not replayed.</remarks>
        /// <exception cref="ArgumentException">A selected identifier or known option combination is invalid.</exception>
        public Task UpdateUploadEmailAsync(string userId, string uploadEmailId, UploadEmailOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var resource = Resource(uploadEmailId); var body = Snapshot(options, false);
            return SendAsync(userId, resource, Method.Put, body, HttpStatusCode.NoContent, cancellationToken);
        }

        /// <summary>Removes a selected owned upload-email endpoint.</summary>
        /// <inheritdoc cref="RemoveUploadEmailAsync"/>
        public void RemoveUploadEmail(string userId, string uploadEmailId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveUploadEmailAsync(userId, uploadEmailId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes a selected owned upload-email endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="uploadEmailId">The exact nonblank endpoint identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed removal.</returns>
        /// <remarks>The bodyless request removes the endpoint, preserving already imported documents. Creator restrictions and errors remain server-enforced. Uncertain outcomes are not replayed.</remarks>
        public Task RemoveUploadEmailAsync(string userId, string uploadEmailId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendAsync(userId, Resource(uploadEmailId), Method.Delete, null, HttpStatusCode.NoContent, cancellationToken); }

        private static JObject Snapshot(UploadEmailOptions options, bool create)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var name = options.Name; var store = options.StoreEmail; var extract = options.ExtractAttachments; var indexing = options.AttachmentTextIndexing;
            var collection = options.CollectionId; var folder = options.FolderId;
            var tags = Strings(options.Tags); var whitelist = Strings(options.WhitelistedFileExtensions); var blacklist = Strings(options.BlacklistedFileExtensions);
            if (create && name == null) throw new ArgumentException("A display name is required.", nameof(options));
            if (store.HasValue && !Enum.IsDefined(typeof(UploadEmailStorage), store.Value)) throw new ArgumentException("Unknown storage selection.", nameof(options));
            if (collection != null) RequireId(collection); if (folder != null) RequireId(folder);
            if (collection != null && folder != null) throw new ArgumentException("Only one destination may be selected.", nameof(options));
            if (whitelist?.Length > 0 && blacklist?.Length > 0) throw new ArgumentException("Nonempty whitelist and blacklist cannot coexist.", nameof(options));
            var effectiveStore = create ? store ?? UploadEmailStorage.None : store;
            var effectiveExtract = create ? extract ?? true : extract;
            if (effectiveStore == UploadEmailStorage.None && effectiveExtract == false ||
                indexing == true && (effectiveStore == UploadEmailStorage.None || effectiveStore == UploadEmailStorage.Text))
                throw new ArgumentException("The selected storage, extraction and indexing combination is invalid.", nameof(options));
            var result = new JObject();
            if (name != null) result["name"] = name;
            if (store.HasValue) result["store-email"] = store == UploadEmailStorage.None ? "none" : store == UploadEmailStorage.Text ? "text" : "original";
            if (extract.HasValue) result["extract-attachments"] = extract.Value;
            if (indexing.HasValue) result["attachment-text-indexing"] = indexing.Value;
            if (collection != null) result["collection"] = collection; if (folder != null) result["folder"] = folder;
            if (tags != null) result["tags"] = new JArray(tags); if (whitelist != null) result["whitelisted-file-extensions"] = new JArray(whitelist); if (blacklist != null) result["blacklisted-file-extensions"] = new JArray(blacklist);
            return result;
        }
        private static string[] Strings(IEnumerable<string> values)
        { var result = values?.ToArray(); if (result != null && result.Any(value => value == null)) throw new ArgumentException("Selected lists cannot contain null strings."); return result; }
        private string Resource(string id) { RequireId(id); return ApiVersionPrefix + "upload-email/" + Uri.EscapeDataString(id); }
        private static void RequireId(string id) { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A usable selected identifier is required."); }
        private static string Identity(string id) => Guid.TryParse(id, out var value) ? value.ToString("D") : id;
        private async Task<RestResponse> SendAsync(string userId, string resource, Method method, JObject body, HttpStatusCode expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var request = CreateRestRequest(resource, method);
            if (body != null) request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(expected)); return response;
        }
        private static T Parse<T>(string content, Func<JObject, T> convert)
        {
            try
            {
                using (var text = new StringReader(content ?? ""))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the endpoint response."); return convert(root); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException)
            { throw new InvalidResponseDataException("The upload-email response has invalid identity, metadata or list structure.", error); }
        }
        private static UploadEmail Record(JObject data)
        {
            var collection = OptionalText(data["collection"]); var folder = OptionalText(data["folder"]);
            if (collection != null && folder != null) throw new InvalidDataException("Conflicting endpoint destinations.");
            return new UploadEmail(ParsedId(data["id"]), Text(data["name"], false), Text(data["email"], true), OptionalText(data["store-email"]),
                Boolean(data["extract-attachments"]), Boolean(data["attachment-text-indexing"]), collection, folder,
                List(data["tags"]), List(data["whitelisted-file-extensions"]), List(data["blacklisted-file-extensions"]));
        }
        private static string ParsedId(JToken value)
        { var id = Text(value, true); if (id == "." || id == "..") throw new InvalidDataException("The endpoint identity is not usable."); return id; }
        private static string Text(JToken value, bool nonblank)
        { if (value?.Type != JTokenType.String || nonblank && string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Required endpoint metadata must be usable text."); return value.Value<string>(); }
        private static string OptionalText(JToken value) => value == null || value.Type == JTokenType.Null ? null : Text(value, true);
        private static bool? Boolean(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (value.Type != JTokenType.Boolean) throw new InvalidDataException("An endpoint flag must be boolean."); return value.Value<bool>(); }
        private static IReadOnlyList<string> List(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (!(value is JArray array)) throw new InvalidDataException("An endpoint list must be an array."); return Array.AsReadOnly(array.Select(item => Text(item, false)).ToArray()); }
    }
}
