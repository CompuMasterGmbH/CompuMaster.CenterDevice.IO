using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Workflows
{
    /// <summary>Contains an immutable explicit document-version attachment to a workflow.</summary>
    public sealed class WorkflowAttachment
    {
        /// <summary>Initializes an explicit attachment document version without performing a workflow action.</summary>
        /// <param name="documentId">The exact nonblank attachment document identifier.</param>
        /// <param name="version">The explicit positive attachment version; no latest-version sentinel is inferred.</param>
        /// <exception cref="ArgumentException">The document identity or version is unusable.</exception>
        public WorkflowAttachment(string documentId, long version)
        {
            if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("An attachment document identifier is required.", nameof(documentId));
            if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version)); DocumentId = documentId; Version = version;
        }
        /// <summary>Gets the original attachment document identifier.</summary>
        public string DocumentId { get; }
        /// <summary>Gets the original explicit positive attachment version.</summary>
        public long Version { get; }
    }

    /// <summary>Contains immutable original workflow details without inferring the caller's role or hidden fields.</summary>
    /// <remarks>Missing or JSON-null selections remain unknown rather than empty. Complete nested objects are defensively copied; returned future types, states and results remain original.</remarks>
    public sealed class WorkflowDetails
    {
        private readonly JObject data;
        internal WorkflowDetails(string id, JObject data) { Id = id; this.data = (JObject)data.DeepClone(); }
        /// <summary>Gets the original workflow identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original document identifier, or null when omitted or JSON null.</summary>
        public string DocumentId => data.Value<string>("document-id");
        /// <summary>Gets the original explicit document version, or null when omitted or JSON null.</summary>
        public long? DocumentVersion => data.Value<long?>("document-version");
        /// <summary>Gets the original creator identifier without inferring it from the caller.</summary>
        public string CreatorId => data.Value<string>("creator");
        /// <summary>Gets the original workflow type, including metadata-form and future names.</summary>
        public string Type => data.Value<string>("type");
        /// <summary>Gets the original workflow status without deriving it from a response.</summary>
        public string Status => data.Value<string>("status");
        /// <summary>Gets the original overall result without inferring completion or caller response.</summary>
        public string Result => data.Value<string>("result");
        /// <summary>Gets the original confirmation mode without inventing a default.</summary>
        public string ConfirmationMode => data.Value<string>("confirmation-mode");
        /// <summary>Gets original workflow visibility without inferring document access or participation.</summary>
        public string Visibility => data.Value<string>("visibility");
        /// <summary>Gets the exact optional creator comment.</summary>
        public string Comment => data.Value<string>("comment");
        /// <summary>Gets original ISO8601 creation text without changing timezone or precision.</summary>
        public string CreationDateRaw => data.Value<string>("creation-date");
        /// <summary>Gets original ISO8601 last-modification text without changing timezone or precision.</summary>
        public string LastModifiedDateRaw => data.Value<string>("last-modified-date");
        /// <summary>Gets original ISO8601 completion text, or null without inferring completed state.</summary>
        public string CompletionDateRaw => data.Value<string>("completion-date");
        /// <summary>Gets read-only original user selections, or null when omitted or JSON null.</summary>
        public IReadOnlyList<string> Users => ReadIds("users");
        /// <summary>Gets read-only original group selections rather than inferred resolved participants.</summary>
        public IReadOnlyList<string> Groups => ReadIds("groups");
        /// <summary>Gets read-only original assignees, or null without inferring unassigned state.</summary>
        public IReadOnlyList<string> Assignees => ReadIds("assignees");
        /// <summary>Gets read-only original not-responded user IDs, or null when hidden or omitted.</summary>
        public IReadOnlyList<string> UsersNotResponded => ReadIds("users-not-responded");
        /// <summary>Gets copied complete user-response objects in a read-only list, or null when hidden or omitted.</summary>
        /// <remarks>Each access returns fresh objects preserving original user/version/result/date/comment and future fields; editing them does not mutate this detail.</remarks>
        public IReadOnlyList<JObject> Responses => data["responses"] is JArray entries ? Array.AsReadOnly(entries.Select(entry => (JObject)entry.DeepClone()).ToArray()) : null;
        /// <summary>Gets a defensive copy of the caller's original response, or null without inferring that no response exists.</summary>
        /// <remarks>No caller user-id or response is fabricated from authentication.</remarks>
        public JObject CallerResponse => CopyObject("response");
        /// <summary>Gets immutable explicit attachment document versions, or null when omitted or JSON null.</summary>
        public IReadOnlyList<WorkflowAttachment> Attachments => data["attachments"] is JArray entries ? Array.AsReadOnly(entries.Select(entry => new WorkflowAttachment(entry.Value<string>("document-id"), entry.Value<long>("version"))).ToArray()) : null;
        /// <summary>Gets a defensive copy of original metadata completion data without evaluating document metadata.</summary>
        public JObject CompletionCondition => CopyObject("completion-condition");
        /// <summary>Gets a defensive copy of creator-visible finalizing data, or null when hidden or omitted.</summary>
        /// <remarks>Hidden data does not mean that no actions exist; construction and reading execute nothing.</remarks>
        public JObject FinalizingActions => CopyObject("finalizing-actions");
        /// <summary>Gets a defensive copy of the complete original role-dependent envelope and future fields.</summary>
        public JObject Data => (JObject)data.DeepClone();
        private JObject CopyObject(string key) => (JObject)(data[key] as JObject)?.DeepClone();
        private IReadOnlyList<string> ReadIds(string key) => data[key] is JArray entries ? Array.AsReadOnly(entries.Values<string>().ToArray()) : null;
    }
}
