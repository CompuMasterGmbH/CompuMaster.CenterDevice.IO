using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.Documents
{
    /// <summary>Selects explicit recipients of document sharing commands.</summary>
    /// <remarks>Null lists are omitted; empty lists are sent unchanged. IDs and e-mail spelling are preserved. At least one recipient is required for selected sharing or unsharing; removing all direct sharings uses a separate explicit command.</remarks>
    public sealed class DocumentSharingRecipients
    {
        /// <summary>Gets or sets selected user IDs.</summary>
        public IEnumerable<string> Users { get; set; }
        /// <summary>Gets or sets selected e-mail addresses, including addresses the server may reject or invite.</summary>
        public IEnumerable<string> Emails { get; set; }
        /// <summary>Gets or sets selected group IDs.</summary>
        public IEnumerable<string> Groups { get; set; }
    }

    /// <summary>Describes a successful invitation created while sharing documents.</summary>
    public sealed class DocumentSharingInvitation
    {
        internal DocumentSharingInvitation(string email, string userId) { Email = email; UserId = userId; }
        /// <summary>Gets the exact server-reported e-mail address.</summary>
        public string Email { get; }
        /// <summary>Gets the new user's identifier.</summary>
        public string UserId { get; }
    }

    /// <summary>Describes an invitation the server could not create.</summary>
    public sealed class DocumentSharingInvitationFailure
    {
        internal DocumentSharingInvitationFailure(string email, long code) { Email = email; Code = code; }
        /// <summary>Gets the exact server-reported e-mail address.</summary>
        public string Email { get; }
        /// <summary>Gets the numeric invitation error code, including future values.</summary>
        /// <remarks>API 2.29 defines 1 for invalid syntax, 2 for an existing tenant address, 3 for tenant restrictions and 100 for an unknown error.</remarks>
        public long Code { get; }
    }

    /// <summary>Reports invitations and exact partial failures from a document sharing command.</summary>
    /// <remarks>A successful HTTP response can report partial failures. Invitations alone are successful outcomes. Empty failure lists do not establish that existing sharing state changed or that an owner lost access.</remarks>
    public sealed class DocumentSharingResult
    {
        internal DocumentSharingResult(IEnumerable<string> documents, IEnumerable<string> users, IEnumerable<string> emails,
            IEnumerable<string> groups, IEnumerable<DocumentSharingInvitation> invited, IEnumerable<DocumentSharingInvitationFailure> notInvited)
        {
            FailedDocuments = documents.ToList().AsReadOnly(); FailedUsers = users.ToList().AsReadOnly();
            FailedEmails = emails.ToList().AsReadOnly(); FailedGroups = groups.ToList().AsReadOnly();
            Invited = invited.ToList().AsReadOnly(); NotInvited = notInvited.ToList().AsReadOnly();
        }
        /// <summary>Gets failed selected document IDs in server order.</summary>
        public IReadOnlyList<string> FailedDocuments { get; }
        /// <summary>Gets failed selected user IDs in server order.</summary>
        public IReadOnlyList<string> FailedUsers { get; }
        /// <summary>Gets failed selected e-mail addresses from an unsharing command.</summary>
        public IReadOnlyList<string> FailedEmails { get; }
        /// <summary>Gets failed selected group IDs in server order.</summary>
        public IReadOnlyList<string> FailedGroups { get; }
        /// <summary>Gets successful invitations in server order.</summary>
        public IReadOnlyList<DocumentSharingInvitation> Invited { get; }
        /// <summary>Gets invitation failures in server order.</summary>
        public IReadOnlyList<DocumentSharingInvitationFailure> NotInvited { get; }
        /// <summary>Gets whether the server reported no sharing, unsharing or invitation failures.</summary>
        public bool AllSucceeded => FailedDocuments.Count == 0 && FailedUsers.Count == 0 && FailedEmails.Count == 0
            && FailedGroups.Count == 0 && NotInvited.Count == 0;
    }
}
