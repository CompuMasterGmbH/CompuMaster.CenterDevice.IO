using CenterDevice.Rest.Clients.MetadataTemplates;
using CenterDevice.Rest.Clients.WorkflowTemplates;
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
        /// <summary>Creates a workflow for explicit document versions and confirms its new identity.</summary>
        /// <inheritdoc cref="CreateWorkflowAsync"/>
        public string CreateWorkflow(string userId, WorkflowCreationOptions options, CancellationToken cancellationToken = default(CancellationToken))
            => CreateWorkflowAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates a direct workflow using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">The required current type and explicit main-document/version, with selected original fields snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new workflow identity; no document or template identity is inferred.</returns>
        /// <remarks>Administrator/internal/external users may create workflows for visible documents; metadata-form cannot contain attachments. Creation can send notifications, create document/attachment sharings and later execute finalizing actions. Group resolution, participant roles, document/attachment visibility and required metadata remain server-enforced. It is non-idempotent; uncertain outcomes are not replayed.</remarks>
        /// <exception cref="ArgumentException">A required identity/version/type, attachment or known nested shape is unusable.</exception>
        /// <exception cref="InvalidResponseDataException">Successful creation does not confirm usable workflow identity.</exception>
        public async Task<string> CreateWorkflowAsync(string userId, WorkflowCreationOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var body = SnapshotWorkflowCreation(options);
            var request = CreateRestRequest(ApiVersionPrefix + "workflows", Method.Post); request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.Created));
            try
            {
                using (var text = new StringReader(response.Content ?? "")) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
                {
                    var data = JObject.Load(reader); if (reader.Read()) throw new InvalidDataException("Unexpected data follows workflow creation.");
                    var id = DetailText(data["id"], true); if (id == "." || id == "..") throw new InvalidDataException("Workflow identity is unusable."); return id;
                }
            }
            catch (Exception error) when (error is JsonException || error is InvalidDataException || error is FormatException)
            { throw new InvalidResponseDataException("The workflow creation response does not confirm usable identity.", error); }
        }

        private static JObject SnapshotWorkflowCreation(WorkflowCreationOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var document = options.DocumentId; var version = options.DocumentVersion; var type = options.Type; var userValues = options.Users; var groupValues = options.Groups; var attachmentValues = options.Attachments;
            var mode = options.ConfirmationMode; var condition = (JObject)options.CompletionCondition?.DeepClone(); var confirmedValues = options.ConfirmedActions; var rejectedValues = options.RejectedActions;
            var share = options.ShareDocumentWithUsers; var comment = options.Comment; var visibility = options.Visibility; var remove = options.RemoveCreatedSharings;
            RequireId(document); if (version <= 0) throw new ArgumentOutOfRangeException(nameof(options), "An explicit positive document version is required.");
            if (type != "read" && type != "approval" && type != "metadata-form") throw new ArgumentException("A current workflow type is required.", nameof(options));
            var users = WorkflowParticipants(userValues); var groups = WorkflowParticipants(groupValues); var attachments = attachmentValues?.ToArray();
            if (attachments != null && attachments.Any(entry => entry == null)) throw new ArgumentException("An attachment selection must identify a document version.", nameof(options));
            if (type == "metadata-form" && attachments?.Length > 0) throw new ArgumentException("Metadata-form does not support attached documents.", nameof(options));
            if (mode != null && mode != "one" && mode != "all") throw new ArgumentException("A current confirmation mode is required.", nameof(options));
            if (type == "metadata-form" && (mode == "all" || condition == null)) throw new ArgumentException("Metadata-form requires completion data and omitted/one confirmation.", nameof(options));
            if (visibility != null && visibility != "document" && visibility != "participant") throw new ArgumentException("A current visibility selection is required.", nameof(options));
            if (remove != null && remove != "no" && remove != "on-completion") throw new ArgumentException("A current sharing-removal selection is required.", nameof(options));
            var confirmed = WorkflowFinalizers(confirmedValues); var rejected = WorkflowFinalizers(rejectedValues);
            if (type != "approval" && rejected?.Count > 0) throw new ArgumentException("Rejected finalizers require approval type.", nameof(options));
            if (condition != null) ValidateWorkflowCompletionInput(condition);
            var body = new JObject { ["type"] = type, ["document-id"] = document, ["document-version"] = version };
            if (users != null) body["users"] = new JArray(users); if (groups != null) body["groups"] = new JArray(groups);
            if (attachments != null) body["attachments"] = new JArray(attachments.Select(entry => new JObject { ["document-id"] = entry.DocumentId, ["version"] = entry.Version }));
            if (mode != null) body["confirmation-mode"] = mode; if (condition != null) body["completion-condition"] = condition;
            if (confirmed != null || rejected != null) { var finalizers = new JObject(); if (confirmed != null) finalizers["confirmed"] = confirmed; if (rejected != null) finalizers["rejected"] = rejected; body["finalizing-actions"] = finalizers; }
            if (share.HasValue) body["share-document-with-users"] = share.Value; if (comment != null) body["comment"] = comment;
            if (visibility != null) body["visibility"] = visibility; if (remove != null) body["remove-created-sharings"] = remove; return body;
        }
        private static string[] WorkflowParticipants(IEnumerable<string> values)
        { var ids = values?.ToArray(); if (ids != null) foreach (var id in ids) RequireId(id); return ids; }
        private static JArray WorkflowFinalizers(IEnumerable<WorkflowFinalizingAction> values)
        {
            if (values == null) return null; var entries = values.ToArray(); if (entries.Length > 1 || entries.Any(entry => entry == null)) throw new ArgumentException("At most one nonnull finalizer is allowed per result.");
            var result = new JArray(); foreach (var entry in entries) { entry.Validate(); result.Add(entry.ToJson()); } return result;
        }
        private static void ValidateWorkflowCompletionInput(JObject condition)
        {
            var fields = condition["metadata"]; if (fields == null || fields.Type == JTokenType.Null) return;
            if (!(fields is JArray entries)) throw new ArgumentException("Completion metadata must be an array.");
            foreach (var token in entries)
            {
                if (!(token is JObject field) || field["key"]?.Type != JTokenType.String) throw new ArgumentException("Completion fields require original metadata keys.");
                MetadataTemplateField.ValidateKey(field.Value<string>("key")); var value = field["default-value"];
                if (value != null && value.Type != JTokenType.Null && value.Type != JTokenType.String) throw new ArgumentException("Completion defaults must be original strings.");
            }
        }
    }
}
