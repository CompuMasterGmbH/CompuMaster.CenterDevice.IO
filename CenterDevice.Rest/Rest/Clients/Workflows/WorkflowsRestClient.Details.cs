using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Workflows
{
    public partial class WorkflowsRestClient
    {
        /// <summary>Retrieves original details of an explicitly selected visible workflow.</summary>
        /// <inheritdoc cref="GetWorkflowDetailsAsync"/>
        public WorkflowDetails GetWorkflowDetails(string userId, string workflowId, bool? includeDocumentVisibility = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetWorkflowDetailsAsync(userId, workflowId, includeDocumentVisibility, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves original workflow details using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="workflowId">The exact selected nonblank workflow identifier.</param>
        /// <param name="includeDocumentVisibility">True also considers document visibility; false or omission considers participant visibility. No local access grant is inferred.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Immutable original role-dependent detail retaining unknown versus empty fields and complete future nested data.</returns>
        /// <remarks>Finalizing actions are creator-visible; users/groups/not-responded/all/caller response projections depend on role and visibility. Metadata-form type is retained although the detail section's type list omits it. Safe GET retry follows existing transport defaults; reading performs no workflow action.</remarks>
        /// <exception cref="ArgumentException">The workflow identity is unusable.</exception>
        /// <exception cref="InvalidResponseDataException">The successful detail has invalid known shapes or identifies another workflow.</exception>
        public async Task<WorkflowDetails> GetWorkflowDetailsAsync(string userId, string workflowId, bool? includeDocumentVisibility = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(Resource(workflowId), Method.Get);
            if (includeDocumentVisibility.HasValue) request.AddQueryParameter("include-document-visibility", includeDocumentVisibility.Value ? "true" : "false");
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                using (var text = new StringReader(response.Content ?? "")) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                {
                    var data = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows workflow detail.");
                    var id = DetailText(data["id"], true); if (id == "." || id == ".." || Canonical(id) != Canonical(workflowId)) throw new InvalidDataException("Workflow identity differs from the selection.");
                    ValidateDetail(data); return new WorkflowDetails(id, data);
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException || error is OverflowException)
            { throw new InvalidResponseDataException("The workflow response has invalid identity or known detail shapes.", error); }
        }

        private static bool DetailHas(JObject data, string key) => data[key] != null && data[key].Type != JTokenType.Null;
        private static string Canonical(string id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : id;
        private static string DetailText(JToken token, bool nonblank)
        { if (token?.Type != JTokenType.String || nonblank && string.IsNullOrWhiteSpace(token.Value<string>())) throw new InvalidDataException("Workflow detail must be original text."); return token.Value<string>(); }
        private static long DetailVersion(JToken token)
        {
            if (token?.Type != JTokenType.Integer || !long.TryParse(token.ToString(Formatting.None), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version <= 0)
                throw new InvalidDataException("A positive Int64 document version is required."); return version;
        }
        private static JObject DetailObject(JToken token)
        { if (!(token is JObject data)) throw new InvalidDataException("Workflow detail must be an object."); return data; }
        private static JArray DetailArray(JToken token)
        { if (!(token is JArray entries)) throw new InvalidDataException("Workflow detail must be an array."); return entries; }
        private static void ValidateResponseDetail(JObject response, bool all)
        {
            if (all) DetailText(response["user-id"], true);
            if (DetailHas(response, "document-version")) DetailVersion(response["document-version"]);
            foreach (var key in new[] { "result", "response-date", "comment" }) if (DetailHas(response, key)) DetailText(response[key], false);
        }
        private static void ValidateDetail(JObject data)
        {
            foreach (var key in new[] { "document-id", "creator" }) if (DetailHas(data, key)) DetailText(data[key], true);
            foreach (var key in new[] { "type", "status", "result", "confirmation-mode", "visibility", "comment", "creation-date", "last-modified-date", "completion-date" }) if (DetailHas(data, key)) DetailText(data[key], false);
            if (DetailHas(data, "document-version")) DetailVersion(data["document-version"]);
            foreach (var key in new[] { "users", "groups", "assignees", "users-not-responded" }) if (DetailHas(data, key)) foreach (var id in DetailArray(data[key])) DetailText(id, true);
            if (DetailHas(data, "response")) ValidateResponseDetail(DetailObject(data["response"]), false);
            if (DetailHas(data, "responses")) foreach (var entry in DetailArray(data["responses"])) ValidateResponseDetail(DetailObject(entry), true);
            if (DetailHas(data, "attachments")) foreach (var entry in DetailArray(data["attachments"]))
            { var attachment = DetailObject(entry); DetailText(attachment["document-id"], true); DetailVersion(attachment["version"]); }
            if (DetailHas(data, "completion-condition"))
            {
                var condition = DetailObject(data["completion-condition"]); if (DetailHas(condition, "metadata")) foreach (var entry in DetailArray(condition["metadata"]))
                { var field = DetailObject(entry); DetailText(field["key"], true); if (DetailHas(field, "default-value")) DetailText(field["default-value"], false); }
            }
            if (DetailHas(data, "finalizing-actions"))
            {
                var finalizers = DetailObject(data["finalizing-actions"]); foreach (var key in new[] { "confirmed", "rejected" }) if (DetailHas(finalizers, key)) foreach (var entry in DetailArray(finalizers[key]))
                { var action = DetailObject(entry); DetailText(action["type"], true); DetailObject(action["data"]); }
            }
        }
    }
}
