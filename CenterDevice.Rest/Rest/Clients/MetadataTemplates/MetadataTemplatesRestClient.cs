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

namespace CenterDevice.Rest.Clients.MetadataTemplates
{
    /// <summary>Manages metadata templates according to server-enforced sharing rights.</summary>
    /// <remarks>Administrators, internal and external users can create templates. Owners, coowners, editors and viewers can retrieve visible metadata; only owner/coowner can retrieve/update sharings or remove a template. No document metadata is applied.</remarks>
    public class MetadataTemplatesRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a metadata-template client using the existing authorization and transport.</summary>
        /// <param name="oauthInfoProvider">The authorization provider.</param>
        /// <param name="configuration">The origin and user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public MetadataTemplatesRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Creates a user metadata template and returns its confirmed identifier.</summary>
        /// <inheritdoc cref="CreateMetadataTemplateAsync"/>
        public string CreateMetadataTemplate(string userId, MetadataTemplateCreateRequest selection, CancellationToken cancellationToken = default(CancellationToken))
            => CreateMetadataTemplateAsync(userId, selection, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Creates a user metadata template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="selection">The nonnull name and complete fields/sharings; explicit empty arrays are allowed and sequences snapshot before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The server-confirmed new template identifier.</returns>
        /// <remarks>Creation is non-idempotent and does not apply metadata to a document. Only current user template type and well-known field namespaces are offered. Default string/decimal/date text remains original for server validation.</remarks>
        /// <exception cref="ArgumentException">Required selections are absent or unsupported.</exception>
        /// <exception cref="InvalidResponseDataException">The success response does not confirm usable identity.</exception>
        public async Task<string> CreateMetadataTemplateAsync(string userId, MetadataTemplateCreateRequest selection, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); if (selection == null) throw new ArgumentNullException(nameof(selection));
            var name = selection.Name; var fields = Fields(selection.Fields); var sharings = Sharings(selection.Sharings);
            if (name == null || fields == null || sharings == null) throw new ArgumentException("A name and explicit complete fields/sharings are required.", nameof(selection));
            var body = new JObject { ["type"] = "user", ["name"] = name, ["fields"] = fields, ["sharings"] = sharings };
            var response = await SendAsync(userId, ApiVersionPrefix + "metadata-templates", Method.Post, body, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => ParsedId(root["id"]));
        }

        /// <summary>Retrieves visible selected metadata-template detail.</summary>
        /// <inheritdoc cref="GetMetadataTemplateAsync"/>
        public MetadataTemplate GetMetadataTemplate(string userId, string metadataTemplateId, CancellationToken cancellationToken = default(CancellationToken))
            => GetMetadataTemplateAsync(userId, metadataTemplateId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves a selected metadata template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="metadataTemplateId">The exact nonblank selected template identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Original metadata with explicit owner/current-user right and nullable hidden sharings.</returns>
        /// <exception cref="InvalidResponseDataException">Metadata is malformed or identity differs from the selection.</exception>
        public async Task<MetadataTemplate> GetMetadataTemplateAsync(string userId, string metadataTemplateId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, Resource(metadataTemplateId), Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => { var result = Record(root); if (Identity(result.Id) != Identity(metadataTemplateId)) throw new InvalidDataException("The template identity differs from the selection."); return result; });
        }

        /// <summary>Retrieves server-selected metadata templates.</summary>
        /// <inheritdoc cref="GetMetadataTemplatesAsync"/>
        public IReadOnlyList<MetadataTemplate> GetMetadataTemplates(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => GetMetadataTemplatesAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Retrieves server-selected metadata templates using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only list preserving server order and original metadata.</returns>
        /// <remarks>The manufacturer describes templates created by the requesting user while also documenting owner/coowner/editor/viewer access. The client preserves the returned selection without filtering by owner or inferring tenant completeness, paging or totals.</remarks>
        /// <exception cref="InvalidResponseDataException">The list has malformed entries or repeated identities.</exception>
        public async Task<IReadOnlyList<MetadataTemplate>> GetMetadataTemplatesAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = await SendAsync(userId, ApiVersionPrefix + "metadata-templates", Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse<IReadOnlyList<MetadataTemplate>>(response.Content, root =>
            {
                if (!(root["metadata-templates"] is JArray entries)) throw new InvalidDataException("A template array is required.");
                var result = new List<MetadataTemplate>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    if (!(entry is JObject item)) throw new InvalidDataException("A template must be an object.");
                    var template = Record(item); if (!ids.Add(Identity(template.Id))) throw new InvalidDataException("Repeated template identity."); result.Add(template);
                }
                return result.AsReadOnly();
            });
        }

        /// <summary>Updates selected metadata-template properties.</summary>
        /// <inheritdoc cref="UpdateMetadataTemplateAsync"/>
        public void UpdateMetadataTemplate(string userId, string metadataTemplateId, MetadataTemplateUpdateRequest options, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateMetadataTemplateAsync(userId, metadataTemplateId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Updates selected metadata-template properties using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="metadataTemplateId">The exact nonblank selected template identifier.</param>
        /// <param name="options">The nonnull selected request; omitted fields are not sent and supplied arrays replace complete state.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed success.</returns>
        /// <remarks>Owners/coowners/editors may update template fields; only owner/coowner may update sharings. Arrays replace complete state, including explicit empty selections. Hidden sharings are never replaced by an inferred empty list; omitted-field acceptance remains server-validated. Uncertain writes are not replayed.</remarks>
        /// <exception cref="ArgumentException">A selected identifier or known option combination is invalid.</exception>
        public Task UpdateMetadataTemplateAsync(string userId, string metadataTemplateId, MetadataTemplateUpdateRequest options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var resource = Resource(metadataTemplateId); var body = Snapshot(options);
            return SendAsync(userId, resource, Method.Put, body, HttpStatusCode.NoContent, cancellationToken);
        }

        /// <summary>Removes a selected metadata template as owner or coowner.</summary>
        /// <inheritdoc cref="RemoveMetadataTemplateAsync"/>
        public void RemoveMetadataTemplate(string userId, string metadataTemplateId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveMetadataTemplateAsync(userId, metadataTemplateId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes a selected metadata template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="metadataTemplateId">The exact nonblank selected template identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed removal.</returns>
        /// <remarks>The bodyless request removes only the template. Owner/coowner restrictions and original failures remain server-enforced. Uncertain outcomes are not replayed.</remarks>
        public Task RemoveMetadataTemplateAsync(string userId, string metadataTemplateId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendAsync(userId, Resource(metadataTemplateId), Method.Delete, null, HttpStatusCode.NoContent, cancellationToken); }

        private static JObject Snapshot(MetadataTemplateUpdateRequest options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var name = options.Name; var type = options.Type; var fields = Fields(options.Fields); var sharings = Sharings(options.Sharings);
            if (type != null && type != "user") throw new ArgumentException("Only the current user template type may be selected.", nameof(options));
            var result = new JObject(); if (name != null) result["name"] = name; if (type != null) result["type"] = type;
            if (fields != null) result["fields"] = fields; if (sharings != null) result["sharings"] = sharings; return result;
        }
        private static JArray Fields(IEnumerable<MetadataTemplateField> source)
        {
            if (source == null) return null; var entries = source.ToArray(); var result = new JArray();
            foreach (var field in entries)
            {
                if (field == null) throw new ArgumentException("Field definitions cannot contain null entries."); MetadataTemplateField.ValidateKey(field.Key);
                var data = new JObject { ["key"] = field.Key }; if (field.DefaultValue != null) data["default-value"] = field.DefaultValue; result.Add(data);
            }
            return result;
        }
        private static JArray Sharings(IEnumerable<MetadataTemplateSharing> source)
        {
            if (source == null) return null; var entries = source.ToArray(); var result = new JArray();
            foreach (var sharing in entries)
            {
                if (sharing == null) throw new ArgumentException("Sharing definitions cannot contain null entries.");
                new MetadataTemplateSharing(sharing.Id, sharing.Right, sharing.Type);
                result.Add(new JObject { ["id"] = sharing.Id, ["right"] = sharing.Right, ["type"] = sharing.Type });
            }
            return result;
        }
        private string Resource(string id) { RequireId(id); return ApiVersionPrefix + "metadata-template/" + Uri.EscapeDataString(id); }
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
                { var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the template response."); return convert(root); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException)
            { throw new InvalidResponseDataException("The metadata-template response has invalid identity, metadata or list structure.", error); }
        }
        private static MetadataTemplate Record(JObject data)
        {
            if (!(data["fields"] is JArray fieldData)) throw new InvalidDataException("Complete template fields must be an array.");
            var fields = new List<MetadataTemplateField>();
            foreach (var token in fieldData)
            {
                if (!(token is JObject field)) throw new InvalidDataException("A template field must be an object.");
                fields.Add(new MetadataTemplateField(Text(field["key"], true), NullableText(field["default-value"]), false));
            }
            List<MetadataTemplateSharing> sharings = null; var sharingData = data["sharings"];
            if (sharingData != null && sharingData.Type != JTokenType.Null)
            {
                if (!(sharingData is JArray entries)) throw new InvalidDataException("Exposed template sharings must be an array."); sharings = new List<MetadataTemplateSharing>();
                foreach (var token in entries)
                {
                    if (!(token is JObject sharing)) throw new InvalidDataException("A template sharing must be an object.");
                    sharings.Add(new MetadataTemplateSharing(Text(sharing["id"], true), Text(sharing["right"], true), Text(sharing["type"], true), false));
                }
            }
            var creation = Text(data["creation-date"], true); var modified = Text(data["last-modified-date"], true);
            return new MetadataTemplate(ParsedId(data["id"]), Text(data["name"], false), Text(data["type"], true), Text(data["owner"], true), Text(data["sharing-right"], true),
                creation, ParseDate(creation), modified, ParseDate(modified), fields.AsReadOnly(), sharings?.AsReadOnly());
        }
        private static string NullableText(JToken value) => value == null || value.Type == JTokenType.Null ? null : Text(value, false);
        private static DateTimeOffset? ParseDate(string value)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}(?:T|$)") || !DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
                throw new InvalidDataException("A template date must be usable ISO8601 text.");
            return System.Text.RegularExpressions.Regex.IsMatch(value, @"(?:[Zz]|[+-]\d{2}:?\d{2})$") ? (DateTimeOffset?)parsed : null;
        }
        private static string ParsedId(JToken value)
        { var id = Text(value, true); if (id == "." || id == "..") throw new InvalidDataException("The template identity is not usable."); return id; }
        private static string Text(JToken value, bool nonblank)
        { if (value?.Type != JTokenType.String || nonblank && string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Required template metadata must be usable text."); return value.Value<string>(); }
    }
}
