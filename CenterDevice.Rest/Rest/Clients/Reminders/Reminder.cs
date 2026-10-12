using System;

namespace CenterDevice.Rest.Clients.Reminders
{
    /// <summary>Contains a reminder returned for its authenticated creator.</summary>
    /// <remarks>The server does not supply creator or recipient identifiers in this schema; the authentication context is not substituted for omitted ownership metadata.</remarks>
    public sealed class Reminder
    {
        internal Reminder(string id, string type, string status, string entity, string note, string expiryText, DateTimeOffset? expiry, string creationText, DateTimeOffset? creation)
        { Id = id; Type = type; Status = status; EntityId = entity; Note = note; ExpiryDateText = expiryText; ExpiryDate = expiry; CreationDateText = creationText; CreationDate = creation; }
        /// <summary>Gets the server-confirmed reminder identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the server type name, retaining unknown future types.</summary>
        public string Type { get; }
        /// <summary>Gets the server status, retaining unknown future status names.</summary>
        public string Status { get; }
        /// <summary>Gets the referenced entity identifier, currently a document identifier.</summary>
        public string EntityId { get; }
        /// <summary>Gets the supplied note, or null when omitted.</summary>
        /// <remarks>An explicit empty note remains empty.</remarks>
        public string Note { get; }
        /// <summary>Gets the exact ISO8601 expiry text supplied by the server.</summary>
        public string ExpiryDateText { get; }
        /// <summary>Gets the expiry date when its server text has an explicit timezone offset.</summary>
        /// <remarks>Offset-free date text does not acquire the workstation timezone.</remarks>
        public DateTimeOffset? ExpiryDate { get; }
        /// <summary>Gets the exact ISO8601 creation text supplied by the server.</summary>
        public string CreationDateText { get; }
        /// <summary>Gets the creation date when its server text has an explicit timezone offset.</summary>
        public DateTimeOffset? CreationDate { get; }
    }
}
