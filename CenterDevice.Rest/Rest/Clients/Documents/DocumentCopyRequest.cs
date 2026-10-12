using System;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Identifies a source document and the filename for a new server-side copy.</summary>
    /// <remarks>The server copies the latest original representation. This request does not carry sharing, tags, comments, version history or extended metadata.</remarks>
    public sealed class DocumentCopyRequest
    {
        /// <summary>Creates an immutable source and filename selection.</summary>
        /// <param name="documentId">The source document identifier.</param>
        /// <param name="filename">The filename of the new copy.</param>
        /// <exception cref="ArgumentException">The source identifier or filename is missing, or the identifier is a relative path segment.</exception>
        public DocumentCopyRequest(string documentId, string filename)
        {
            if (string.IsNullOrWhiteSpace(documentId) || documentId == "." || documentId == "..")
                throw new ArgumentException("A source document identifier is required.", nameof(documentId));
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentException("The copy filename is required.", nameof(filename));
            DocumentId = documentId;
            Filename = filename;
        }
        /// <summary>Gets the source document identifier.</summary>
        public string DocumentId { get; }
        /// <summary>Gets the filename for the new document copy.</summary>
        public string Filename { get; }
    }
}
