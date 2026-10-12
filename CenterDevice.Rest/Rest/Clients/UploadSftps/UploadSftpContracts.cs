using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.UploadSftps
{
    /// <summary>Selects optional SFTP endpoint fields without imposing defaults.</summary>
    /// <remarks>Creation requires nonnull name and password; partial updates omit null fields. Password policy, destination visibility and creator restrictions remain server-enforced.</remarks>
    public sealed class UploadSftpOptions
    {
        /// <summary>Gets or sets the display name.</summary>
        /// <value>The exact string, including empty, or null to omit it on update.</value>
        public string Name { get; set; }
        /// <summary>Gets or sets the confidential import password.</summary>
        /// <value>The exact string, including empty/whitespace for server validation, or null to omit it on update.</value>
        public string Password { get; set; }
        /// <summary>Gets or sets the visible destination collection.</summary>
        /// <value>The exact nonblank identifier, or null to omit it; a folder cannot also be selected.</value>
        public string CollectionId { get; set; }
        /// <summary>Gets or sets the visible destination folder.</summary>
        /// <value>The exact nonblank identifier, or null to omit it; a collection cannot also be selected.</value>
        public string FolderId { get; set; }
        /// <summary>Gets or sets tags applied to imported documents.</summary>
        /// <value>The exact strings to snapshot once, an explicit empty sequence, or null to omit the field.</value>
        public IEnumerable<string> Tags { get; set; }
    }

    /// <summary>Contains immutable server-confirmed SFTP connection data.</summary>
    /// <remarks>Username and password are confidential import credentials. The original public key is provided for caller-controlled host-key verification; receiving it does not verify a subsequent SFTP server or authorize trusting an unrelated host.</remarks>
    public sealed class UploadSftpConnection
    {
        internal UploadSftpConnection(string id, string hostname, int port, string username, string password, string publicKey)
        { Id = id; Hostname = hostname; Port = port; Username = username; Password = password; ServerPublicKey = publicKey; }
        /// <summary>Gets the original endpoint identifier.</summary>
        public string Id { get; }
        /// <summary>Gets the original server hostname.</summary>
        public string Hostname { get; }
        /// <summary>Gets the confirmed TCP port.</summary>
        /// <value>A port in the range 1 through 65535.</value>
        public int Port { get; }
        /// <summary>Gets the confidential import username.</summary>
        public string Username { get; }
        /// <summary>Gets the confidential original import password.</summary>
        public string Password { get; }
        /// <summary>Gets the original server public-key text without parsing or normalization.</summary>
        public string ServerPublicKey { get; }
    }

    /// <summary>Contains immutable owned SFTP endpoint metadata.</summary>
    public sealed class UploadSftp
    {
        internal UploadSftp(string name, string collection, string folder, IReadOnlyList<string> tags, UploadSftpConnection connection)
        { Name = name; CollectionId = collection; FolderId = folder; Tags = tags; Connection = connection; }
        /// <summary>Gets the original endpoint identifier.</summary>
        public string Id => Connection.Id;
        /// <summary>Gets the original display name.</summary>
        public string Name { get; }
        /// <summary>Gets the selected destination collection.</summary>
        /// <value>The original identifier, or null if absent or JSON null.</value>
        public string CollectionId { get; }
        /// <summary>Gets the selected destination folder.</summary>
        /// <value>The original identifier, or null if absent or JSON null.</value>
        public string FolderId { get; }
        /// <summary>Gets immutable original tags.</summary>
        /// <value>The read-only list, or null if absent or JSON null.</value>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>Gets confirmed confidential connection data and the original public key.</summary>
        public UploadSftpConnection Connection { get; }
    }
}
