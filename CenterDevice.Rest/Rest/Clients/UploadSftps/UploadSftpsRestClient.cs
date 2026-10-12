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

namespace CenterDevice.Rest.Clients.UploadSftps
{
    /// <summary>Manages SFTP upload endpoints created by the authenticated caller.</summary>
    /// <remarks>The server enforces creator visibility, destination visibility and its ten-endpoint creation limit. Management does not connect to SFTP or upload documents. Returned credentials are confidential and are not included in new client validation diagnostics.</remarks>
    public class UploadSftpsRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes an upload-sftp client using the existing authorization and transport.</summary>
        /// <param name="oauthInfoProvider">The authorization provider.</param>
        /// <param name="configuration">The origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public UploadSftpsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Creates an SFTP upload endpoint and returns confirmed connection data.</summary>
        /// <inheritdoc cref="CreateUploadSftpAsync"/>
        public UploadSftpConnection CreateUploadSftp(string userId, UploadSftpOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => CreateUploadSftpAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates an SFTP upload endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">The nonnull options with nonnull name/password; selections snapshot before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Confirmed endpoint identity, hostname, port, confidential credentials and original server public key.</returns>
        /// <remarks>Creation is non-idempotent; uncertain outcomes are not replayed. The server validates password policy, visibility and its ten-endpoint limit. This does not connect to SFTP, verify a server public key or upload documents.</remarks>
        /// <exception cref="ArgumentException">Required data or destinations are invalid.</exception>
        /// <exception cref="InvalidResponseDataException">Identity or connection data is malformed.</exception>
        public async Task<UploadSftpConnection> CreateUploadSftpAsync(string userId, UploadSftpOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var body = Snapshot(options, true);
            var response = await SendAsync(userId, ApiVersionPrefix + "upload-sftps", Method.Post, body, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, Connection);
        }

        /// <summary>Retrieves an upload-sftp endpoint created by the caller.</summary>
        /// <inheritdoc cref="GetUploadSftpAsync"/>
        public UploadSftp GetUploadSftp(string userId, string uploadSftpId, CancellationToken cancellationToken = default(CancellationToken))
            => GetUploadSftpAsync(userId, uploadSftpId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves a selected upload-sftp endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="uploadSftpId">The exact nonblank endpoint identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original metadata, confidential connection data and the server public key, without inferred ownership.</returns>
        /// <exception cref="InvalidResponseDataException">Metadata is malformed or identity differs from the selection.</exception>
        public async Task<UploadSftp> GetUploadSftpAsync(string userId, string uploadSftpId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, Resource(uploadSftpId), Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => { var result = Record(root); if (Identity(result.Id) != Identity(uploadSftpId)) throw new InvalidDataException("The endpoint identity differs from the selection."); return result; });
        }

        /// <summary>Retrieves the caller's own upload-sftp endpoints.</summary>
        /// <inheritdoc cref="GetUploadSftpsAsync"/>
        public IReadOnlyList<UploadSftp> GetUploadSftps(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => GetUploadSftpsAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves the caller's own upload-sftp endpoints using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only list preserving server order and original metadata.</returns>
        /// <remarks>The manufacturer defines an own-endpoint list without paging or total metadata. The ten-endpoint creation limit is enforced by the server, without a client completeness inference.</remarks>
        /// <exception cref="InvalidResponseDataException">The list has malformed entries or repeated identities.</exception>
        public async Task<IReadOnlyList<UploadSftp>> GetUploadSftpsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, ApiVersionPrefix + "upload-sftps", Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse<IReadOnlyList<UploadSftp>>(response.Content, root =>
            {
                if (!(root["upload-sftps"] is JArray entries)) throw new InvalidDataException("An endpoint array is required.");
                var result = new List<UploadSftp>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    if (!(entry is JObject item)) throw new InvalidDataException("An endpoint must be an object.");
                    var endpoint = Record(item); if (!ids.Add(Identity(endpoint.Id))) throw new InvalidDataException("Repeated endpoint identity."); result.Add(endpoint);
                }
                return result.AsReadOnly();
            });
        }

        /// <summary>Updates selected upload-sftp fields.</summary>
        /// <inheritdoc cref="UpdateUploadSftpAsync"/>
        public void UpdateUploadSftp(string userId, string uploadSftpId, UploadSftpOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateUploadSftpAsync(userId, uploadSftpId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates selected upload-sftp fields using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="uploadSftpId">The exact nonblank owned endpoint identifier.</param>
        /// <param name="options">The nonnull partial selection; omitted fields remain unchanged and empty arrays clear corresponding lists.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed success.</returns>
        /// <remarks>Only selected name, password, destination and tags change. Omitted fields remain unchanged; no null-based destination clearing is inferred. Password policy and existing state remain server-validated. Uncertain writes are not replayed.</remarks>
        /// <exception cref="ArgumentException">A selected identifier or known option combination is invalid.</exception>
        public Task UpdateUploadSftpAsync(string userId, string uploadSftpId, UploadSftpOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var resource = Resource(uploadSftpId); var body = Snapshot(options, false);
            return SendAsync(userId, resource, Method.Put, body, HttpStatusCode.NoContent, cancellationToken);
        }

        /// <summary>Removes a selected owned upload-sftp endpoint.</summary>
        /// <inheritdoc cref="RemoveUploadSftpAsync"/>
        public void RemoveUploadSftp(string userId, string uploadSftpId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveUploadSftpAsync(userId, uploadSftpId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes a selected owned upload-sftp endpoint using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="uploadSftpId">The exact nonblank endpoint identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed removal.</returns>
        /// <remarks>The bodyless request removes the endpoint, preserving already imported documents. Creator restrictions and errors remain server-enforced. Uncertain outcomes are not replayed.</remarks>
        public Task RemoveUploadSftpAsync(string userId, string uploadSftpId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendAsync(userId, Resource(uploadSftpId), Method.Delete, null, HttpStatusCode.NoContent, cancellationToken); }

        private static JObject Snapshot(UploadSftpOptions options, bool create)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var name = options.Name; var password = options.Password; var collection = options.CollectionId; var folder = options.FolderId;
            var tags = Strings(options.Tags);
            if (create && (name == null || password == null)) throw new ArgumentException("A name and password selection are required.", nameof(options));
            if (collection != null) RequireId(collection); if (folder != null) RequireId(folder);
            if (collection != null && folder != null) throw new ArgumentException("Only one destination may be selected.", nameof(options));
            var result = new JObject();
            if (name != null) result["name"] = name; if (password != null) result["password"] = password;
            if (collection != null) result["collection"] = collection; if (folder != null) result["folder"] = folder;
            if (tags != null) result["tags"] = new JArray(tags); return result;
        }
        private static string[] Strings(IEnumerable<string> values)
        { var result = values?.ToArray(); if (result != null && result.Any(value => value == null)) throw new ArgumentException("Selected lists cannot contain null strings."); return result; }
        private string Resource(string id) { RequireId(id); return ApiVersionPrefix + "upload-sftp/" + Uri.EscapeDataString(id); }
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
            { throw new InvalidResponseDataException("The upload-sftp response has invalid identity, metadata or list structure.", error); }
        }
        private static UploadSftp Record(JObject data)
        {
            var collection = OptionalText(data["collection"]); var folder = OptionalText(data["folder"]);
            if (collection != null && folder != null) throw new InvalidDataException("Conflicting endpoint destinations.");
            return new UploadSftp(Text(data["name"], false), collection, folder, List(data["tags"]), Connection(data));
        }
        private static UploadSftpConnection Connection(JObject data)
        {
            var portToken = data["port"];
            if (portToken?.Type != JTokenType.Integer || !long.TryParse(portToken.ToString(), out var port) || port < 1 || port > 65535)
                throw new InvalidDataException("The endpoint port must be a valid TCP port.");
            return new UploadSftpConnection(ParsedId(data["id"]), Text(data["hostname"], true), (int)port, Text(data["username"], true), Text(data["password"], false), Text(data["server-public-key"], true));
        }
        private static string ParsedId(JToken value)
        { var id = Text(value, true); if (id == "." || id == "..") throw new InvalidDataException("The endpoint identity is not usable."); return id; }
        private static string Text(JToken value, bool nonblank)
        { if (value?.Type != JTokenType.String || nonblank && string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Required endpoint metadata must be usable text."); return value.Value<string>(); }
        private static string OptionalText(JToken value) => value == null || value.Type == JTokenType.Null ? null : Text(value, true);
        private static IReadOnlyList<string> List(JToken value)
        { if (value == null || value.Type == JTokenType.Null) return null; if (!(value is JArray array)) throw new InvalidDataException("An endpoint list must be an array."); return Array.AsReadOnly(array.Select(item => Text(item, false)).ToArray()); }
    }
}
