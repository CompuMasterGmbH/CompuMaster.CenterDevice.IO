using CenterDevice.Rest.Clients.Common;
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

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal async Task<DocumentMembershipResult> ChangeDocumentMembershipAsync(string userId, string resource, string id,
            IEnumerable<string> documents, bool add, bool? removeFromCollection, bool allowPartial, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A selected membership resource identifier is required.", nameof(id));
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            var selected = documents.ToArray();
            if (selected.Length == 0 || selected.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Explicit nonblank document identifiers are required.", nameof(documents));
            var parameters = new JObject { ["documents"] = new JArray(selected) };
            if (removeFromCollection.HasValue) parameters["remove-from-collection"] = removeFromCollection.Value;
            var body = new JObject { ["action"] = add ? "add-documents" : "remove-documents", ["params"] = parameters };
            var request = CreateRestRequest(ApiVersionPrefix + resource + "/" + Uri.EscapeDataString(id), Method.Post, "application/json");
            request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, allowPartial ? new StatusCodeResponseHandler(HttpStatusCode.NoContent, HttpStatusCode.OK) : new StatusCodeResponseHandler(HttpStatusCode.NoContent));
            if (response.StatusCode == HttpStatusCode.NoContent) return new DocumentMembershipResult(Array.AsReadOnly(Array.Empty<string>()));
            try
            {
                using (var source = new StringReader(response.Content ?? ""))
                using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Unexpected data follows the membership response.");
                    if (!(root["failed-documents"] is JArray failed)) throw new InvalidDataException("A failed-documents array is required for HTTP 200.");
                    var selectedIds = new HashSet<string>(selected.Select(MembershipIdentity), StringComparer.Ordinal); var result = new List<string>();
                    foreach (var value in failed)
                    {
                        if (value.Type != JTokenType.String || string.IsNullOrWhiteSpace(value.Value<string>()) || !selectedIds.Contains(MembershipIdentity(value.Value<string>())))
                            throw new InvalidDataException("A failed document must identify an explicitly selected document.");
                        result.Add(value.Value<string>());
                    }
                    return new DocumentMembershipResult(result.AsReadOnly());
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { throw new InvalidResponseDataException("The membership response has invalid failure metadata or structure.", error); }
        }
        private static string MembershipIdentity(string id) => Guid.TryParse(id, out var value) ? value.ToString("D") : id;
    }
}
