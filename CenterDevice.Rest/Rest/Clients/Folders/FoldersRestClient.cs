using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FoldersRestClient : CenterDeviceRestClient, IFoldersRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "folders/";
            }
        }

        public FoldersRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        public FoldersResponse GetFolders(string userId, string collectionId, string parentId, string documentId)
        {
            return GetFolders(userId, collectionId, parentId, documentId, null, null);
        }

        public FoldersResponse GetFolders(string userId, string collectionId, string path)
        {
            return GetFolders(userId, collectionId, null, null, path, null);
        }

        public FoldersResponse GetFolders(string userId, string collectionId, string parentId, string documentId, string path, string[] fields)
        {
            return GetFolders(userId, collectionId, parentId, documentId, path, null, fields);
        }

        public FoldersResponse GetFolders(string userId, string collectionId, string folderId, string documentId, string path, IEnumerable<string> ids, string[] fields)
        {
            return GetFolders(userId, collectionId, folderId, documentId, path, ids, false, false, fields);
        }

        public FoldersResponse GetFolders(string userId, string collectionId, string folderId, string documentId, string path, IEnumerable<string> ids, bool onlySharedFolders, bool onlyTopMost, string[] fields)
        {
            var folderRequest = CreateRestRequest(URI_RESOURCE, Method.Get);
            if (collectionId != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.COLLECTION, collectionId);
            }
            if (documentId != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.DOCUMENT, documentId);
            }
            if (folderId != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.PARENT, folderId);
            }
            if (path != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.PATH, path.Replace(Path.DirectorySeparatorChar, '/'));
            }
            if (ids != null && ids.Any())
            {
                folderRequest.AddQueryParameter(RestApiConstants.IDS, string.Join(",", ids));
            }

            if (fields != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.FIELDS, string.Join(",", fields));
            }

            if (onlySharedFolders)
            {
                folderRequest.AddQueryParameter(RestApiConstants.ONLY_SHARED_FOLDERS, true.ToString());
                if (onlyTopMost)
                {
                    folderRequest.AddQueryParameter(RestApiConstants.ONLY_TOP_MOST, true.ToString());
                }
            }

            var response = Execute<FoldersResponse>(GetOAuthInfo(userId), folderRequest);
            return UnwrapResponse(response, new GetFoldersResponseHandler());
        }

        public FolderCreationResponse CreateFolder(string userId, string name, string collection, string parent)
        {
            var folderRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);
            if (string.IsNullOrWhiteSpace(parent) || parent == RestApiConstants.NONE)
            {
                folderRequest.AddJsonBody(new { name = name, parent = RestApiConstants.NONE, collection = collection });
            }
            else
            {
                folderRequest.AddJsonBody(new { name = name, parent = parent });
            }

            var response = Execute<FolderCreationResponse>(GetOAuthInfo(userId), folderRequest);
            return UnwrapResponse(response, new CreateFolderResponseHandler());
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element