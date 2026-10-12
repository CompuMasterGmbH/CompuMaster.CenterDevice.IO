using CenterDevice.Rest.Clients.MetadataTemplates;
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

namespace CenterDevice.Rest.Clients.WorkflowTemplates
{
    /// <summary>Manages own workflow templates and explicit creation of a workflow from a template.</summary>
    /// <remarks>Administrators, internal and external users may create templates. Retrieval/use/removal is creator-restricted; the server enforces update roles, visibility and notification/sharing/action behavior. Management does not apply changes to workflows already created from a template.</remarks>
    public class WorkflowTemplatesRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a workflow-template client using existing authorization and transport.</summary>
        /// <param name="oauthInfoProvider">The authorization provider.</param>
        /// <param name="configuration">The origin/user-agent configuration.</param>
        /// <param name="errorHandler">The existing expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing resource prefix.</param>
        public WorkflowTemplatesRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>Creates an own workflow template and returns its confirmed identity.</summary>
        /// <inheritdoc cref="CreateWorkflowTemplateAsync"/>
        public string CreateWorkflowTemplate(string userId, WorkflowTemplateOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => CreateWorkflowTemplateAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an own workflow template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">The nonnull name/type and selected original fields, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new template identifier.</returns>
        /// <remarks>Creation is non-idempotent and uncertain writes are not replayed. Automatic sharing defaults to true, visibility to participant and sharing removal to no through omission; template creation itself does not create a workflow or send its notifications.</remarks>
        /// <exception cref="ArgumentException">Current required shapes or selections are invalid.</exception>
        /// <exception cref="InvalidResponseDataException">The success response does not confirm usable identity.</exception>
        public async Task<string> CreateWorkflowTemplateAsync(string userId, WorkflowTemplateOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var body = Snapshot(options, true);
            var response = await SendAsync(userId, ApiVersionPrefix + "workflow-templates", Method.Post, body, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => ParsedId(root["id"]));
        }

        /// <summary>Creates a workflow for an explicit document version using an own template.</summary>
        /// <inheritdoc cref="CreateWorkflowAsync"/>
        public string CreateWorkflow(string userId, string templateId, string documentId, long documentVersion, CancellationToken cancellationToken = default(CancellationToken))
            => CreateWorkflowAsync(userId, templateId, documentId, documentVersion, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates a workflow from an own template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="templateId">The exact nonblank selected own-template identifier.</param>
        /// <param name="documentId">The exact nonblank visible document identifier.</param>
        /// <param name="documentVersion">The positive explicit version; no latest-version sentinel is inferred.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new workflow identifier, distinct from the template identity.</returns>
        /// <remarks>This non-idempotent operation can send notifications, create sharings and later execute finalizing actions. Groups/metadata templates resolve at workflow creation; the server enforces effective participants (at most 200), nonempty metadata fields, creator and document visibility. Uncertain outcomes are not replayed.</remarks>
        /// <exception cref="ArgumentException">An identity or version selection is invalid.</exception>
        public async Task<string> CreateWorkflowAsync(string userId, string templateId, string documentId, long documentVersion, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var resource = Resource(templateId); RequireId(documentId); if (documentVersion <= 0) throw new ArgumentOutOfRangeException(nameof(documentVersion));
            var body = new JObject { ["action"] = "workflow", ["data"] = new JObject { ["document-id"] = documentId, ["document-version"] = documentVersion } };
            var response = await SendAsync(userId, resource, Method.Post, body, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => ParsedId(root["id"]));
        }

        /// <summary>Retrieves an own workflow template.</summary>
        /// <inheritdoc cref="GetWorkflowTemplateAsync"/>
        public WorkflowTemplate GetWorkflowTemplate(string userId, string templateId, CancellationToken cancellationToken = default(CancellationToken))
            => GetWorkflowTemplateAsync(userId, templateId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves an own workflow template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="templateId">The exact selected nonblank template identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Immutable original selections and defensive complete data without inferred creator, defaults or effective participants.</returns>
        /// <exception cref="InvalidResponseDataException">The success metadata is malformed or identifies another template.</exception>
        public async Task<WorkflowTemplate> GetWorkflowTemplateAsync(string userId, string templateId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var response = await SendAsync(userId, Resource(templateId), Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse(response.Content, root => { var template = Record(root); if (Identity(template.Id) != Identity(templateId)) throw new InvalidDataException("The template identity differs from the selection."); return template; });
        }

        /// <summary>Retrieves the caller's own workflow templates.</summary>
        /// <inheritdoc cref="GetWorkflowTemplatesAsync"/>
        public IReadOnlyList<WorkflowTemplate> GetWorkflowTemplates(string userId, CancellationToken cancellationToken = default(CancellationToken))
            => GetWorkflowTemplatesAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves the caller's own workflow templates using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A read-only list retaining server order and original complete detail.</returns>
        /// <remarks>No paging, total, timestamp or tenant-wide selection is inferred.</remarks>
        public async Task<IReadOnlyList<WorkflowTemplate>> GetWorkflowTemplatesAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var response = await SendAsync(userId, ApiVersionPrefix + "workflow-templates", Method.Get, null, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return Parse<IReadOnlyList<WorkflowTemplate>>(response.Content, root =>
            {
                if (!(root["workflow-templates"] is JArray entries)) throw new InvalidDataException("A workflow-template array is required."); var result = new List<WorkflowTemplate>(); var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var token in entries) { if (!(token is JObject data)) throw new InvalidDataException("A workflow template must be an object."); var template = Record(data); if (!ids.Add(Identity(template.Id))) throw new InvalidDataException("Repeated workflow-template identity."); result.Add(template); }
                return result.AsReadOnly();
            });
        }

        /// <summary>Updates selected workflow-template fields.</summary>
        /// <inheritdoc cref="UpdateWorkflowTemplateAsync"/>
        public void UpdateWorkflowTemplate(string userId, string templateId, WorkflowTemplateOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateWorkflowTemplateAsync(userId, templateId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates selected workflow-template fields using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="templateId">The exact selected nonblank template identifier.</param>
        /// <param name="options">The nonnull selected request; omitted fields remain unchanged and automatic-sharing selection is prohibited.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed success.</returns>
        /// <remarks>Existing workflows are unaffected. Name/removal fields use the manufacturer's same-as-creation entity contract; the server validates accepted state transitions and unknown existing template type. Uncertain outcomes are not replayed.</remarks>
        public Task UpdateWorkflowTemplateAsync(string userId, string templateId, WorkflowTemplateOptions options, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var resource = Resource(templateId); var body = Snapshot(options, false); return SendAsync(userId, resource, Method.Put, body, HttpStatusCode.NoContent, cancellationToken); }

        /// <summary>Removes an own workflow template while preserving created workflows.</summary>
        /// <inheritdoc cref="RemoveWorkflowTemplateAsync"/>
        public void RemoveWorkflowTemplate(string userId, string templateId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveWorkflowTemplateAsync(userId, templateId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes an own workflow template using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="templateId">The exact selected nonblank template identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after confirmed removal.</returns>
        /// <remarks>It removes the selected template rather than a workflow ID. Creator restrictions remain server-enforced and existing workflows remain unchanged. Uncertain writes are not replayed.</remarks>
        public Task RemoveWorkflowTemplateAsync(string userId, string templateId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendAsync(userId, Resource(templateId), Method.Delete, null, HttpStatusCode.NoContent, cancellationToken); }

        private static JObject Snapshot(WorkflowTemplateOptions options, bool create)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var name = options.Name; var type = options.Type; var users = Ids(options.Users); var groups = Ids(options.Groups);
            var mode = options.ConfirmationMode; var condition = (JObject)options.CompletionCondition?.DeepClone();
            var confirmed = Actions(options.ConfirmedActions); var rejected = Actions(options.RejectedActions); var share = options.ShareDocumentWithUsers;
            var comment = options.Comment; var visibility = options.Visibility; var remove = options.RemoveCreatedSharings;
            if (create && (name == null || type == null)) throw new ArgumentException("Creation requires a name and current workflow type.");
            if (type != null && type != "read" && type != "approval" && type != "metadata-form") throw new ArgumentException("Unsupported workflow type.");
            if (mode != null && mode != "one" && mode != "all") throw new ArgumentException("Unsupported confirmation mode.");
            if (visibility != null && visibility != "document" && visibility != "participant") throw new ArgumentException("Unsupported visibility.");
            if (remove != null && remove != "no" && remove != "on-completion") throw new ArgumentException("Unsupported sharing-removal mode.");
            if (!create && share.HasValue) throw new ArgumentException("Automatic sharing cannot be updated.");
            if (type == "metadata-form" && mode == "all" || create && type == "metadata-form" && condition == null) throw new ArgumentException("Metadata-form requires completion data and omitted/one confirmation.");
            if (type != null && type != "approval" && rejected?.Count > 0) throw new ArgumentException("Rejected finalizing actions require approval type.");
            if (condition != null)
            {
                try { ValidateCondition(condition, true); }
                catch (InvalidDataException error) { throw new ArgumentException("Completion metadata has an invalid current shape.", nameof(options), error); }
            }
            var body = new JObject(); if (name != null) body["name"] = name; if (type != null) body["type"] = type;
            if (users != null) body["users"] = new JArray(users); if (groups != null) body["groups"] = new JArray(groups);
            if (mode != null) body["confirmation-mode"] = mode; if (condition != null) body["completion-condition"] = condition;
            if (confirmed != null || rejected != null) { var actions = new JObject(); if (confirmed != null) actions["confirmed"] = confirmed; if (rejected != null) actions["rejected"] = rejected; body["finalizing-actions"] = actions; }
            if (share.HasValue) body["share-document-with-users"] = share.Value; if (comment != null) body["comment"] = comment;
            if (visibility != null) body["visibility"] = visibility; if (remove != null) body["remove-created-sharings"] = remove; return body;
        }
        private static string[] Ids(IEnumerable<string> values)
        { var ids = values?.ToArray(); if (ids != null) foreach (var id in ids) RequireId(id); return ids; }
        private static JArray Actions(IEnumerable<WorkflowFinalizingAction> values)
        {
            if (values == null) return null; var entries = values.ToArray(); if (entries.Length > 1 || entries.Any(entry => entry == null)) throw new ArgumentException("At most one nonnull finalizer is allowed per result.");
            var result = new JArray(); foreach (var entry in entries) { entry.Validate(); result.Add(entry.ToJson()); } return result;
        }
        private static void ValidateCondition(JObject condition, bool input)
        {
            var templates = condition["metadata-templates"]; if (templates != null && templates.Type != JTokenType.Null)
            { if (!(templates is JArray array)) throw Shape(input, "Metadata template references must be an array."); foreach (var id in array) Text(id, true); }
            var fields = condition["metadata"]; if (fields == null || fields.Type == JTokenType.Null) return;
            if (!(fields is JArray metadata)) throw Shape(input, "Completion metadata must be an array.");
            foreach (var token in metadata)
            {
                if (!(token is JObject field)) throw Shape(input, "Completion fields must be objects."); var key = Text(field["key"], true);
                if (input) MetadataTemplateField.ValidateKey(key);
                var value = field["default-value"]; if (value != null && value.Type != JTokenType.Null) Text(value, false);
            }
        }
        private static Exception Shape(bool input, string message) => input ? (Exception)new ArgumentException(message) : new InvalidDataException(message);
        private static WorkflowTemplate Record(JObject data)
        {
            var id = ParsedId(data["id"]); var name = Text(data["name"], false); var type = Text(data["type"], true);
            foreach (var key in new[] { "confirmation-mode", "comment", "visibility", "remove-created-sharings" }) if (data[key] != null && data[key].Type != JTokenType.Null) Text(data[key], false);
            var share = data["share-document-with-users"]; if (share != null && share.Type != JTokenType.Null && share.Type != JTokenType.Boolean) throw new InvalidDataException("The automatic-sharing flag must be boolean.");
            foreach (var key in new[] { "users", "groups" }) if (data[key] != null && data[key].Type != JTokenType.Null)
            { if (!(data[key] is JArray entries)) throw new InvalidDataException("Participant selections must be arrays."); foreach (var entry in entries) Text(entry, true); }
            var condition = data["completion-condition"]; if (condition != null && condition.Type != JTokenType.Null) { if (!(condition is JObject obj)) throw new InvalidDataException("Completion data must be an object."); ValidateCondition(obj, false); }
            var actions = data["finalizing-actions"]; if (actions != null && actions.Type != JTokenType.Null)
            {
                if (!(actions is JObject results)) throw new InvalidDataException("Finalizing results must be an object.");
                foreach (var key in new[] { "confirmed", "rejected" }) if (results[key] != null && results[key].Type != JTokenType.Null)
                {
                    if (!(results[key] is JArray entries)) throw new InvalidDataException("Finalizing selections must be arrays.");
                    foreach (var entry in entries) { if (!(entry is JObject action) || !(action["data"] is JObject)) throw new InvalidDataException("A finalizer requires object data."); Text(action["type"], true); }
                }
            }
            return new WorkflowTemplate(id, name, type, data);
        }
        private string Resource(string id) { RequireId(id); return ApiVersionPrefix + "workflow-template/" + Uri.EscapeDataString(id); }
        private static void RequireId(string id) { if (string.IsNullOrWhiteSpace(id) || id == "." || id == "..") throw new ArgumentException("A usable selected identity is required."); }
        private static string Identity(string id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : id;
        private static string ParsedId(JToken value) { var id = Text(value, true); if (id == "." || id == "..") throw new InvalidDataException("The identity is not usable."); return id; }
        private static string Text(JToken value, bool nonblank)
        { if (value?.Type != JTokenType.String || nonblank && string.IsNullOrWhiteSpace(value.Value<string>())) throw new InvalidDataException("Workflow-template detail must be usable text."); return value.Value<string>(); }
        private async Task<RestResponse> SendAsync(string userId, string resource, Method method, JObject body, HttpStatusCode expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); var request = CreateRestRequest(resource, method); if (body != null) request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(expected)); return response;
        }
        private static T Parse<T>(string content, Func<JObject, T> convert)
        {
            try
            {
                using (var text = new StringReader(content ?? "")) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                { var root = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows the template response."); return convert(root); }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException)
            { throw new InvalidResponseDataException("The workflow-template response has invalid identity, detail or list structure.", error); }
        }
    }
}
