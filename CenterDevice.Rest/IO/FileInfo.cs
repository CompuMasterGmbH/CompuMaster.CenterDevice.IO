using System.Collections.Generic;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.IO
{
    public partial class FileInfo
    {
        public FileInfo(CenterDevice.IO.IOClientBase client, CenterDevice.IO.DirectoryInfo parentDirectory, CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata document)
        {
            this.ioClient = client;
            this.parentDirectory = parentDirectory;
            this.document = document;
        }

        protected readonly CenterDevice.IO.IOClientBase ioClient;

        private CenterDevice.IO.DirectoryInfo parentDirectory;
        /// <summary>
        /// The directory containing this file
        /// </summary>
        public CenterDevice.IO.DirectoryInfo ParentDirectory { get => this.parentDirectory; }

        readonly CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata document;

        /// <summary>
        /// Archiving date
        /// </summary>
        public System.DateTime? ArchivedDate { get => this.document.ArchivedDate; }

        /// <summary>
        /// Modification date
        /// </summary>
        public System.DateTime? ModificationDate { get => this.document.DocumentDate; }

        /// <summary>
        /// Unique ID for low level API
        /// </summary>
        public string ID { get => this.document.Id; }

        /// <summary>
        /// User who locked this file
        /// </summary>
        public string LockedBy { get => this.document.LockedBy; }

        /// <summary>
        /// Locks
        /// </summary>
        public List<string> Locks { get => this.document.Locks; }

        /// <summary>
        /// File size
        /// </summary>
        public long Size { get => this.document.Size; }

        /// <summary>
        /// Upload date
        /// </summary>
        public System.DateTime UploadDate { get => this.document.UploadDate; }

        /// <summary>
        /// User who upload this file
        /// </summary>
        public string Uploader { get => this.document.Uploader; }

        /// <summary>
        /// Link
        /// </summary>
        public string Link { get => this.document.Link; }

        /// <summary>
        /// Owner
        /// </summary>
        public string Owner { get => this.document.Owner; }

        /// <summary>
        /// Version date
        /// </summary>
        public System.DateTime VersionDate { get => this.document.VersionDate; }

        /// <summary>
        /// Version number
        /// </summary>
        public long Version { get => this.document.Version; }

        /// <summary>
        /// Group sharings
        /// </summary>
        public CenterDevice.Rest.Clients.Common.Sharings Groups { get => this.document.Groups; }

        /// <summary>
        /// User sharings
        /// </summary>
        public CenterDevice.Rest.Clients.Common.Sharings Users { get => this.document.Users; }

        /// <summary>
        /// Shared property
        /// </summary>
        public bool IsShared
        {
            get
            {
                return this.document.IsShared;
            }
        }

        /// <summary>
        /// The file name
        /// </summary>
        public string FileName { get => this.document.Filename; }

        protected string fileName 
        { 
            set 
            {
                this.document.Filename = value;
            }
        }

        /// <summary>
        /// The full path starting from root
        /// </summary>
        public string FullName { get => this.parentDirectory.FullName + this.ioClient.Paths.DirectorySeparatorChar + this.FileName; }

        /// <summary>
        /// Collections referencing this file
        /// </summary>
        public Rest.Clients.Documents.SharingInfo ReferencedFromCollectionIDs { get => this.document.Collections; }

        /// <summary>
        /// Folders referencing this file
        /// </summary>
        public List<string> ReferencedFromFolderIDs { get => this.document.Folders; }

        /// <summary>
        /// Is there a file collision because of multiple files uploaded and created with equal names
        /// </summary>
        public bool HasCollidingDuplicateFile
        {
            get
            {
                foreach (FileInfo file in this.parentDirectory.GetFiles())
                {
                    if ((file.ID != this.ID) && (file.FileName == this.FileName))
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// The full path starting from root
        /// </summary>
        public string[] Path
        {
            get
            {
                var result = new List<string>(this.parentDirectory.Path);
                result.Add(this.FileName);
                return result.ToArray();
            }
        }

        /// <summary>
        /// The full name
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return this.FullName;
        }

        /// <summary>
        /// Indent a string
        /// </summary>
        private string Indent(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            else
            {
                string result = "    " + value.Replace("\n", "\n    ");
                if (result.EndsWith("\n    "))
                    result = result.Substring(0, result.Length - ("\n    ".Length - 1));
                return result;
            }
        }

        /// <summary>
        /// Print details on this entry
        /// </summary>
        /// <returns></returns>
        public string ToStringDetails()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(this.FullName + " {");
            sb.AppendLine(this.Indent("FileName=" + this.FileName));
            sb.AppendLine(this.Indent("ID=" + this.ID));
            sb.AppendLine(this.Indent("Size=" + this.Size));
            sb.AppendLine(this.Indent("Link=" + this.Link));
            sb.AppendLine(this.Indent("ArchivedDate=" + this.ArchivedDate));
            sb.AppendLine(this.Indent("ModificationDate=" + this.ModificationDate));
            sb.AppendLine(this.Indent("UploadDate=" + this.UploadDate));
            sb.AppendLine(this.Indent("VersionDate=" + this.VersionDate));
            sb.AppendLine(this.Indent("Version=" + this.Version));
            sb.AppendLine(this.Indent("Owner=" + this.Owner));
            sb.AppendLine(this.Indent("LockedBy=" + this.LockedBy));
            sb.AppendLine(this.Indent("Locks[]=" + ((this.Locks != null) ? string.Join("|", this.Locks.ToArray()) : null)));
            sb.AppendLine(this.Indent("HasCollidingDuplicateFile=" + this.HasCollidingDuplicateFile));
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Overwrite the file with a new version
        /// </summary>
        /// <param name="localPath"></param>
        public void UploadNewVersion(string localPath)
        {
            this.ioClient.ApiClient.Document.UploadNewVersion(this.ioClient.CurrentAuthenticationContextUserID, this.ID, this.FileName, localPath, System.Threading.CancellationToken.None);
            this.parentDirectory.getFiles = null; //force reload on next request since changed file properties must be reloaded
        }

        /// <summary>
        /// Overwrite the file with a new version
        /// </summary>
        /// <param name="fileDataStream"></param>
        public void UploadNewVersion(System.Func<System.IO.Stream> fileDataStream)
        {
            this.ioClient.ApiClient.Document.UploadNewVersion(this.ioClient.CurrentAuthenticationContextUserID, this.ID, this.FileName, fileDataStream, System.Threading.CancellationToken.None);
            this.parentDirectory.getFiles = null; //force reload on next request since changed file properties must be reloaded
        }

        /// <summary>
        /// Remove the file
        /// </summary>
        public void Delete()
        {
            this.ioClient.ApiClient.Document.DeleteDocument(this.ioClient.CurrentAuthenticationContextUserID, this.ID);
            this.parentDirectory.getFiles = null; //force reload on next request since changed file properties must be reloaded
        }

        /// <summary>
        /// Rename the file
        /// </summary>
        /// <param name="targetFileName"></param>
        public void Rename(string targetFileName)
        {
            this.ioClient.ApiClient.Document.RenameDocument(this.ioClient.CurrentAuthenticationContextUserID, this.ID, targetFileName);
            this.parentDirectory.getFiles = null; //force reload on next request since changed file properties must be reloaded
            this.fileName = targetFileName;
        }

        /// <summary>
        /// Download using a stream
        /// </summary>
        /// <returns></returns>
        public System.IO.Stream Download()
        {
            return this.Download(0);
        }

        /// <summary>
        /// Download using a stream
        /// </summary>
        /// <param name="version"></param>
        /// <returns></returns>
        public System.IO.Stream Download(long version)
        {
            if (version == 0)
                return this.ioClient.ApiClient.Document.DownloadDocument(this.ioClient.CurrentAuthenticationContextUserID, this.ID, null, null);
            else
                return this.ioClient.ApiClient.Document.DownloadDocument(this.ioClient.CurrentAuthenticationContextUserID, this.ID, version, null);
        }

        /// <summary>
        /// Download to disk
        /// </summary>
        /// <param name="targetFileName"></param>
        public void Download(string targetFileName)
        {
            this.Download(targetFileName, 0);
        }

        /// <summary>
        /// Download a specific version to disk
        /// </summary>
        /// <param name="targetFileName"></param>
        /// <param name="version"></param>
        public void Download(string targetFileName, long version)
        {
            System.IO.FileStream fs = null;
            System.IO.Stream s = null;
            try
            {
                fs = new System.IO.FileStream(targetFileName, System.IO.FileMode.Create);
                s = this.Download(version);
                //s.Seek(0, System.IO.SeekOrigin.Begin);
                s.CopyTo(fs);
                fs.Flush(true);
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                    fs.Dispose();
                }
                if (s != null)
                {
                    s.Close();
                    s.Dispose();
                }
            }
            if (this.ModificationDate.HasValue)
                System.IO.File.SetLastWriteTimeUtc(targetFileName, this.ModificationDate.Value);
        }

        /// <summary>
        /// Move a file into a target directory
        /// </summary>
        /// <param name="targetDirectory"></param>
        public void Move(DirectoryInfo targetDirectory)
        {
            if (targetDirectory.IsRootDirectory)
                throw new System.NotSupportedException("Moving a file into root directory is not supported");
            this.ioClient.ApiClient.Documents.MoveDocuments(this.ioClient.CurrentAuthenticationContextUserID, new string[] { this.ID },
                this.ParentDirectory.AssociatedCollection.CollectionID, this.parentDirectory.FolderID,
                targetDirectory.AssociatedCollection.CollectionID, targetDirectory.FolderID);
            this.parentDirectory.getFiles = null; //force reload on next request since changed file properties must be reloaded
            targetDirectory.getFiles = null; //force reload on next request since changed file properties must be reloaded
            this.parentDirectory = targetDirectory; //correctly re-assign current file to new parent directory
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element