using CenterDevice.Rest.Clients.Groups;
using System.Collections.Generic;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.IO
{
    /// <summary>
    /// IOClient base implementations for high level API access to CenterDevice services
    /// </summary>
    public abstract partial class IOClientBase
    {
        /// <summary>
        /// Create a new instance of IOClientBase
        /// </summary>
        /// <param name="apiClient"></param>
        /// <param name="userID"></param>
        protected IOClientBase(CenterDevice.Rest.Clients.CenterDeviceClientBase apiClient, string userID)
        {
            this.apiClient = apiClient;
            this.currentAuthenticationContextUserID = userID;
        }

        private DirectoryInfo directoryInfo = null;
        /// <summary>
        /// A buffered access to the remote directory structure
        /// </summary>
        public DirectoryInfo RootDirectory
        {
            get
            {
                if (this.directoryInfo == null)
                    this.directoryInfo = new DirectoryInfo(this);
                return this.directoryInfo;
            }
        }

        /// <summary>
        /// Reset the local directory cache forcing future directory access to reload from server
        /// </summary>
        public void ResetDirectoryCache()
        {
            this.directoryInfo = null;
        }

        protected readonly string currentAuthenticationContextUserID;
        /// <summary>
        /// The ID of the authentication user for directory and file access (might be different from CenterDevice user ID if used as client for a 3rd party server like e.g. Scopevisio Teamwork)
        /// </summary>
        /// <remarks>Each user has got individual permissions to access directories and files. This user ID is used to setup the authentication/filtering context for visible/accessible directories and files when requesting directory listings.</remarks>
        public string CurrentAuthenticationContextUserID { get => this.currentAuthenticationContextUserID; }

        readonly CenterDevice.Rest.Clients.CenterDeviceClientBase apiClient;
        /// <summary>
        /// A reference to the API client for the CenterDevice REST interface
        /// </summary>
        public CenterDevice.Rest.Clients.CenterDeviceClientBase ApiClient { get => this.apiClient; }

        /// <summary>
        /// Provide path transformations configuration and tools
        /// </summary>
        public virtual IO.PathTransformations Paths { get; set; } = new IO.PathTransformations();

        #region Internal API access
        internal List<CenterDevice.Rest.Clients.Collections.Collection> LookupCollections()
        {
            return this.apiClient.Collections.GetCollections(this.currentAuthenticationContextUserID, null, true)?.Collections;
        }

        internal List<Rest.Clients.Folders.Folder> LookupCollectionFolders(string collectionId)
        {
            return this.ApiClient.Folders.GetFolders(this.currentAuthenticationContextUserID, collectionId, CenterDevice.Rest.RestApiConstants.NONE, null, null, new string[] { "collection", "id", "name", "parent", "users", "groups", "link" }).Folders;
        }

        internal void LookupCollectionFolders(CenterDevice.Rest.Clients.Collections.Collection collection)
        {
            collection.SubFolders = LookupCollectionFolders(collection.Id);
        }

        internal List<Rest.Clients.Folders.Folder> LookupFolders(string parentFolderId)
        {
            return this.ApiClient.Folders.GetFolders(this.currentAuthenticationContextUserID, null, parentFolderId, null, null, new string[] { "collection", "id", "name", "parent", "users", "groups", "link" }).Folders;
        }

        internal void LookupFolders(Rest.Clients.Folders.Folder parentFolder)
        {
            parentFolder.SubFolders = LookupFolders(parentFolder.Id);
        }

        internal List<Rest.Clients.Documents.Metadata.DocumentFullMetadata> LookupAllCollectionDocumentsRecursively(Rest.Clients.Collections.Collection collection)
        {
            return LookupAllCollectionDocumentsRecursively(collection.Id);
        }

        internal List<Rest.Clients.Documents.Metadata.DocumentFullMetadata> LookupAllCollectionDocumentsRecursively(string collectionId)
        {
            return this.ApiClient.Documents.Get<Rest.Clients.Documents.Metadata.DocumentFullMetadata>(this.currentAuthenticationContextUserID, collectionId, null, null, 0, int.MaxValue).Documents;
        }

        internal List<Rest.Clients.Documents.Metadata.DocumentFullMetadata> LookupCollectionDocuments(string collectionId)
        {
            return this.ApiClient.Documents.Get<Rest.Clients.Documents.Metadata.DocumentFullMetadata>(this.currentAuthenticationContextUserID, collectionId, null, Rest.RestApiConstants.NONE, 0, int.MaxValue).Documents;
        }

        internal void LookupCollectionDocuments(Rest.Clients.Collections.Collection collection)
        {
            collection.Documents = LookupCollectionDocuments(collection.Id);
        }

        internal List<Rest.Clients.Documents.Metadata.DocumentFullMetadata> LookupDocuments(string parentFolderId)
        {
            return this.ApiClient.Documents.Get<Rest.Clients.Documents.Metadata.DocumentFullMetadata>(this.currentAuthenticationContextUserID, null, null, parentFolderId, 0, int.MaxValue).Documents;
        }

        internal void LookupDocuments(Rest.Clients.Folders.Folder parentFolder)
        {
            parentFolder.Documents = LookupDocuments(parentFolder.Id);
        }
        #endregion

        /// <summary>
        /// Get link details
        /// </summary>
        /// <param name="linkId"></param>
        /// <returns></returns>
        public Rest.Clients.Link.Link GetLink(string linkId)
        {
            return this.ApiClient.Link.GetLink(this.CurrentAuthenticationContextUserID, linkId);
        }

        /// <summary>
        /// Get upload link details
        /// </summary>
        /// <param name="linkId"></param>
        /// <returns></returns>
        public Rest.Clients.Link.UploadLink GetUploadLink(string linkId)
        {
            return this.ApiClient.UploadLink.GetLink(this.CurrentAuthenticationContextUserID, linkId);
        }

        private readonly Dictionary<string, string> CachedKnownGroupNames = new Dictionary<string, string>();
        public string GroupName(string groupId)
        {
            if (this.CachedKnownGroupNames.ContainsKey(groupId))
            {
                return this.CachedKnownGroupNames[groupId];
            }
            else
            {
                string Result = this.ApiClient.Group.GetGroup(this.CurrentAuthenticationContextUserID, groupId).Name;
                this.CachedKnownGroupNames.Add(groupId, Result);
                return Result;
            }
        }

        private readonly Dictionary<string, string> CachedKnownUserNames = new Dictionary<string, string>();
        public string UserName(string userId)
        {
            if (this.CachedKnownUserNames.ContainsKey(userId))
            {
                return this.CachedKnownUserNames[userId];
            }
            else
            {
                string Result = this.ApiClient.User.GetUserData(this.CurrentAuthenticationContextUserID, userId).GetFullName();
                this.CachedKnownUserNames.Add(userId, Result);
                return Result;
            }
        }

        private readonly Dictionary<string, string> CachedKnownUserEMailAddresses = new Dictionary<string, string>();
        public string UserEMailAddress(string userId)
        {
            if (this.CachedKnownUserEMailAddresses.ContainsKey(userId))
            {
                return this.CachedKnownUserEMailAddresses[userId];
            }
            else
            {
                string Result = this.ApiClient.User.GetUserData(this.CurrentAuthenticationContextUserID, userId).Email;
                this.CachedKnownUserEMailAddresses.Add(userId, Result);
                return Result;
            }
        }

        private string _CachedCurrentContextUserIdResult = null;
        private string _CachedCurrentContextUserIdForAuthUserId = null;

        /// <summary>
        /// The CenterDevice user ID of current context (= user ID as used for sharings, etc.)
        /// </summary>
        /// <returns></returns>
        public string CurrentContextUserId()
        {
            if (this._CachedCurrentContextUserIdForAuthUserId == this.CurrentAuthenticationContextUserID)
            {
                return this._CachedCurrentContextUserIdResult;
            }
            else
            {
                string Result = this.ApiClient.User.GetLoggedInUserData(this.apiClient.oAuthInfoProvider.GetOAuthInfo(CurrentAuthenticationContextUserID)).Id;
                _CachedCurrentContextUserIdResult = Result;
                _CachedCurrentContextUserIdForAuthUserId = this.CurrentAuthenticationContextUserID;
                return Result;
            }
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element