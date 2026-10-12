using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Reports complete success or exact per-document failures from a document command.</summary>
    /// <remarks>Successful transport can carry partial failures. Failed IDs remain in the server's spelling/order; failures are not retried automatically. This result does not establish a role grant or prove that existing idempotent state changed.</remarks>
    public sealed class DocumentCommandResult
    {
        internal DocumentCommandResult(IEnumerable<string> failedDocuments)
            => FailedDocuments = failedDocuments.ToList().AsReadOnly();
        /// <summary>Gets the server-reported failed document IDs, or an empty list after complete success.</summary>
        public IReadOnlyList<string> FailedDocuments { get; }
        /// <summary>Gets whether the server reported complete command success.</summary>
        public bool AllSucceeded => FailedDocuments.Count == 0;
    }
}
