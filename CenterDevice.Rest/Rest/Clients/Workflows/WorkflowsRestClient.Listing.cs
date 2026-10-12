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

namespace CenterDevice.Rest.Clients.Workflows
{
    public partial class WorkflowsRestClient
    {
        /// <summary>Retrieves the server-selected visible workflows using original optional filters.</summary>
        /// <inheritdoc cref="GetWorkflowsAsync"/>
        public IReadOnlyList<WorkflowDetails> GetWorkflows(string userId, WorkflowQueryOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
            => GetWorkflowsAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves filtered visible workflows using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">The original optional query, snapshotted before authorization; null retains every server default.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Read-only original role-dependent details in server order, without invented paging, totals or tenant completeness.</returns>
        /// <remarks>All seventeen documented fields remain optional. Document visibility requires a document and omitted caller role; attachment inclusion requires a document. Date lower bounds are inclusive, upper bounds exclusive; text and server ordering remain original. Unknown textual choices remain server-validated. Reading performs no workflow action; safe GET retries retain existing defaults.</remarks>
        /// <exception cref="ArgumentException">A document/visibility/role dependency or CSV selection is unusable.</exception>
        /// <exception cref="InvalidResponseDataException">Successful list metadata has invalid known shapes or repeated identity.</exception>
        public async Task<IReadOnlyList<WorkflowDetails>> GetWorkflowsAsync(string userId, WorkflowQueryOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var query = SnapshotWorkflowQuery(options);
            var request = CreateRestRequest(ApiVersionPrefix + "workflows", Method.Get);
            foreach (var parameter in query) request.AddQueryParameter(parameter.Key, parameter.Value);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
            try
            {
                using (var text = new StringReader(response.Content ?? "")) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the workflow list.");
                    var entries = DetailArray(root["workflows"]); var result = new List<WorkflowDetails>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var entry in entries)
                    {
                        var data = DetailObject(entry); var id = DetailText(data["id"], true);
                        if (id == "." || id == ".." || !ids.Add(Canonical(id))) throw new InvalidDataException("Workflow list identity is unusable or repeated.");
                        ValidateDetail(data); result.Add(new WorkflowDetails(id, data));
                    }
                    return result.AsReadOnly();
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException || error is OverflowException)
            { throw new InvalidResponseDataException("The workflow response has invalid identity or known list shapes.", error); }
        }

        private static List<KeyValuePair<string, string>> SnapshotWorkflowQuery(WorkflowQueryOptions options)
        {
            var query = new List<KeyValuePair<string, string>>(); if (options == null) return query;
            var document = options.DocumentId; var role = options.Role; var status = options.Status; var result = options.Result;
            var visibility = options.IncludeDocumentVisibility; var types = options.Types; var creators = options.Creators; var responders = options.Responders; var responded = options.Responded;
            var creationFrom = options.CreationDateFrom; var creationTo = options.CreationDateTo; var modifiedFrom = options.LastModifiedDateFrom; var modifiedTo = options.LastModifiedDateTo;
            var responderStatus = options.ResponderStatus; var attachment = options.IncludeWhereAttachment; var sort = options.Sort; var order = options.Order;
            if (document != null) RequireId(document);
            if (visibility == true && (document == null || role != null)) throw new ArgumentException("Document visibility requires a document and omitted caller role.", nameof(options));
            if (attachment == true && document == null) throw new ArgumentException("Attachment inclusion requires a document.", nameof(options));
            AddWorkflowQuery(query, "document-id", document); AddWorkflowQuery(query, "role", role); AddWorkflowQuery(query, "status", status); AddWorkflowQuery(query, "result", result);
            AddWorkflowQuery(query, "include-document-visibility", QueryBoolean(visibility)); AddWorkflowQuery(query, "types", WorkflowCsv(types));
            AddWorkflowQuery(query, "creators", WorkflowCsv(creators)); AddWorkflowQuery(query, "responders", WorkflowCsv(responders)); AddWorkflowQuery(query, "responded", QueryBoolean(responded));
            AddWorkflowQuery(query, "creation-date-from", creationFrom); AddWorkflowQuery(query, "creation-date-to", creationTo);
            AddWorkflowQuery(query, "last-modified-date-from", modifiedFrom); AddWorkflowQuery(query, "last-modified-date-to", modifiedTo);
            AddWorkflowQuery(query, "responder-status", responderStatus); AddWorkflowQuery(query, "include-where-attachment", QueryBoolean(attachment));
            AddWorkflowQuery(query, "sort", sort); AddWorkflowQuery(query, "order", order); return query;
        }
        private static string QueryBoolean(bool? value) => value.HasValue ? (value.Value ? "true" : "false") : null;
        private static void AddWorkflowQuery(List<KeyValuePair<string, string>> query, string key, string value) { if (value != null) query.Add(new KeyValuePair<string, string>(key, value)); }
        private static string WorkflowCsv(IEnumerable<string> values)
        {
            if (values == null) return null; var entries = values.ToArray();
            if (entries.Any(value => string.IsNullOrWhiteSpace(value) || value.IndexOf(',') >= 0)) throw new ArgumentException("A workflow CSV selection requires original nonblank entries without commas.");
            return string.Join(",", entries);
        }
    }
}
