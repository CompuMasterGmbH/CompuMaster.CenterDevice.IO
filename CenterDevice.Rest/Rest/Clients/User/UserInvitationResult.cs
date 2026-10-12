using System.Collections.Generic;
using System.Linq;

namespace CenterDevice.Rest.Clients.User
{
    /// <summary>Reports a user invitation created for an explicit e-mail address.</summary>
    public sealed class UserInvitation
    {
        internal UserInvitation(string email, string userId) { Email = email; UserId = userId; }
        /// <summary>Gets the original server-reported e-mail address.</summary>
        public string Email { get; }
        /// <summary>Gets the newly created user's identifier.</summary>
        public string UserId { get; }
    }

    /// <summary>Reports an e-mail address the server could not invite.</summary>
    public sealed class UserInvitationFailure
    {
        internal UserInvitationFailure(string email, long code) { Email = email; Code = code; }
        /// <summary>Gets the original server-reported e-mail address, including invalid syntax.</summary>
        public string Email { get; }
        /// <summary>Gets the invitation error code, including future numeric values.</summary>
        /// <remarks>API 2.29 defines 1 for invalid syntax, 2 for an existing tenant address, 3 for tenant restrictions and 100 for an unknown error.</remarks>
        public long Code { get; }
    }

    /// <summary>Reports complete user invitation outcomes in the server's original order.</summary>
    /// <remarks>HTTP 200 can contain failures. Invited users must confirm registration and receive the server's default guest role. Existing tenant addresses can fail with code 2; they are not silently counted as newly invited.</remarks>
    public sealed class UserInvitationResult
    {
        internal UserInvitationResult(IEnumerable<UserInvitation> invited, IEnumerable<UserInvitationFailure> failures)
        { Invited = invited.ToList().AsReadOnly(); NotInvited = failures.ToList().AsReadOnly(); }
        /// <summary>Gets successfully created invitations.</summary>
        public IReadOnlyList<UserInvitation> Invited { get; }
        /// <summary>Gets invitations the server rejected.</summary>
        public IReadOnlyList<UserInvitationFailure> NotInvited { get; }
        /// <summary>Gets whether every reported invitation succeeded after all selected addresses were accounted for.</summary>
        /// <remarks>This does not establish accepted registration, delivery of mail or permission to access tenant documents.</remarks>
        public bool AllSucceeded => NotInvited.Count == 0;
    }
}
