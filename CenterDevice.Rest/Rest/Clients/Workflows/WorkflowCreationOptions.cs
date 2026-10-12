using CenterDevice.Rest.Clients.WorkflowTemplates;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Workflows
{
    /// <summary>Selects explicit original direct workflow-creation fields without imposing server defaults.</summary>
    /// <remarks>Sequences and nested objects snapshot once before authorization. Visibility, participation, group resolution, metadata requirements, notifications and actual finalizing behavior remain server-enforced.</remarks>
    public sealed class WorkflowCreationOptions
    {
        /// <summary>Initializes an explicit workflow request without default identity, version or type.</summary>
        public WorkflowCreationOptions() { }
        /// <summary>Gets or sets the exact required nonblank main-document identity.</summary>
        public string DocumentId { get; set; }
        /// <summary>Gets or sets the explicit positive main-document version.</summary>
        /// <remarks>The default zero is invalid; no current/latest version is inferred.</remarks>
        public long DocumentVersion { get; set; }
        /// <summary>Gets or sets the required read/approval/metadata-form type.</summary>
        public string Type { get; set; }
        /// <summary>Gets or sets original participant user IDs, an explicit empty sequence, or null to omit them.</summary>
        public IEnumerable<string> Users { get; set; }
        /// <summary>Gets or sets original group IDs, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>Groups resolve at workflow creation; visibility and resolved participant limits are server-enforced without a guessed client count.</remarks>
        public IEnumerable<string> Groups { get; set; }
        /// <summary>Gets or sets immutable explicit attachment selections, an empty sequence, or null to omit them.</summary>
        /// <remarks>Metadata-form cannot contain attachments; an explicit empty sequence remains a selection of no attachments.</remarks>
        public IEnumerable<WorkflowAttachment> Attachments { get; set; }
        /// <summary>Gets or sets one/all confirmation mode, or null to retain server omission behavior.</summary>
        /// <remarks>Metadata-form requires one or omission.</remarks>
        public string ConfirmationMode { get; set; }
        /// <summary>Gets or sets original metadata-form completion data, or null to omit it.</summary>
        /// <remarks>Metadata-form requires a supplied object. Current individual metadata keys/default strings are checked without normalizing decimal/date text or inferring valid document metadata; additional complete data remains original.</remarks>
        public JObject CompletionCondition { get; set; }
        /// <summary>Gets or sets confirmed-result finalizers, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>Currently at most one action is allowed per result.</remarks>
        public IEnumerable<WorkflowFinalizingAction> ConfirmedActions { get; set; }
        /// <summary>Gets or sets rejected-result finalizers, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>A nonempty rejected-result selection requires approval type.</remarks>
        public IEnumerable<WorkflowFinalizingAction> RejectedActions { get; set; }
        /// <summary>Gets or sets explicit automatic document and attachment sharing, or null to omit it.</summary>
        /// <remarks>False requires participant visibility to all selected documents. Direct-creation omission does not invent the template-specific true default.</remarks>
        public bool? ShareDocumentWithUsers { get; set; }
        /// <summary>Gets or sets the exact optional notification comment, retaining empty text.</summary>
        public string Comment { get; set; }
        /// <summary>Gets or sets document/participant visibility, or null to retain the participant server default.</summary>
        public string Visibility { get; set; }
        /// <summary>Gets or sets no/on-completion sharing removal, or null to retain the no server default.</summary>
        /// <remarks>This applies to workflow-created main-document and attachment sharings.</remarks>
        public string RemoveCreatedSharings { get; set; }
    }
}
