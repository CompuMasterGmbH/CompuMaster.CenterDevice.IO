using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Common
{
    /// <summary>Contains immutable server-reported failures of an explicit document membership operation.</summary>
    /// <remarks>Membership changes preserve document identities and do not erase documents. When removing through a folder with collection removal selected, failures describe the collection-removal portion; folder references may already have been removed. This result does not establish transactionality or infer other document locations.</remarks>
    public sealed class DocumentMembershipResult
    {
        internal DocumentMembershipResult(IReadOnlyList<string> failedDocuments) { FailedDocuments = failedDocuments; }
        /// <summary>Gets original failed document identifiers in server order.</summary>
        /// <value>An immutable list; confirmed HTTP 204 produces an empty list.</value>
        public IReadOnlyList<string> FailedDocuments { get; }
        /// <summary>Gets whether the server reported failed document identifiers.</summary>
        public bool HasFailedDocuments => FailedDocuments.Count > 0;
        /// <summary>Gets whether the server reported no failures for the selected operation.</summary>
        /// <remarks>This does not infer ownership, other references or an atomic all-or-nothing update.</remarks>
        public bool AllSucceeded => FailedDocuments.Count == 0;
    }
}
