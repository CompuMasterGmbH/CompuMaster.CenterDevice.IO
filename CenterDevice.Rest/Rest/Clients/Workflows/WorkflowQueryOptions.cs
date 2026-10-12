using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Workflows
{
    /// <summary>Selects optional original workflow-list filters without imposing server defaults.</summary>
    /// <remarks>Null omits fields; explicit false, empty text and empty sequences remain explicit. Current textual choice validation, filtering, ordering and visibility remain server-authoritative. Sequences are captured once before authorization.</remarks>
    public sealed class WorkflowQueryOptions
    {
        /// <summary>Initializes a query with every server default retained through omission.</summary>
        public WorkflowQueryOptions() { }
        /// <summary>Gets or sets the exact selected nonblank main document identifier, or null to omit it.</summary>
        public string DocumentId { get; set; }
        /// <summary>Gets or sets the original caller-role filter, or null to omit it.</summary>
        /// <remarks>Current values are creator/responder. It must be omitted when document visibility is explicitly included.</remarks>
        public string Role { get; set; }
        /// <summary>Gets or sets the original overall status filter, or null to omit it.</summary>
        /// <remarks>Current values are started/completed; server validation remains authoritative.</remarks>
        public string Status { get; set; }
        /// <summary>Gets or sets the original overall result filter, or null to omit it.</summary>
        /// <remarks>Current values are rejected/confirmed; no completion state is inferred.</remarks>
        public string Result { get; set; }
        /// <summary>Gets or sets inclusion of document-visible workflows, or null to omit it.</summary>
        /// <remarks>True requires DocumentId and omitted Role. False or omission considers participant visibility; this does not grant access locally.</remarks>
        public bool? IncludeDocumentVisibility { get; set; }
        /// <summary>Gets or sets original workflow-type selections, an explicit empty sequence, or null to omit them.</summary>
        /// <remarks>The list section documents read/approval; additional selected strings remain server-validated rather than inventing metadata-form filter support from its creation/detail support.</remarks>
        public IEnumerable<string> Types { get; set; }
        /// <summary>Gets or sets original creator identifiers, an explicit empty sequence, or null to omit them.</summary>
        public IEnumerable<string> Creators { get; set; }
        /// <summary>Gets or sets original responder identifiers, an explicit empty sequence, or null to omit them.</summary>
        public IEnumerable<string> Responders { get; set; }
        /// <summary>Gets or sets whether the caller has responded, or null to omit that filter.</summary>
        public bool? Responded { get; set; }
        /// <summary>Gets or sets the original inclusive creation-date lower bound in ISO8601 text.</summary>
        /// <value>The exact text without date/timezone conversion or local range ordering, or null to omit it.</value>
        public string CreationDateFrom { get; set; }
        /// <summary>Gets or sets the original exclusive creation-date upper bound in ISO8601 text.</summary>
        /// <inheritdoc cref="CreationDateFrom"/>
        public string CreationDateTo { get; set; }
        /// <summary>Gets or sets the original inclusive last-modification lower bound in ISO8601 text.</summary>
        /// <inheritdoc cref="CreationDateFrom"/>
        public string LastModifiedDateFrom { get; set; }
        /// <summary>Gets or sets the original exclusive last-modification upper bound in ISO8601 text.</summary>
        /// <inheritdoc cref="CreationDateFrom"/>
        public string LastModifiedDateTo { get; set; }
        /// <summary>Gets or sets original started/completed status from the responder's perspective.</summary>
        /// <remarks>Null omits it; overall completion and whether the caller has responded remain server-derived.</remarks>
        public string ResponderStatus { get; set; }
        /// <summary>Gets or sets inclusion of workflows containing DocumentId as an attachment.</summary>
        /// <remarks>True requires DocumentId. Explicit false without DocumentId remains an ignored server selection rather than failing locally; null omits it.</remarks>
        public bool? IncludeWhereAttachment { get; set; }
        /// <summary>Gets or sets the original sort field, or null to retain the creation-date server default.</summary>
        /// <remarks>Current fields are creation-date/completion-date; selected text remains server-validated.</remarks>
        public string Sort { get; set; }
        /// <summary>Gets or sets original sort order, or null to retain the ascending server default.</summary>
        /// <remarks>Current values are asc/desc; selected text remains server-validated.</remarks>
        public string Order { get; set; }
    }
}
