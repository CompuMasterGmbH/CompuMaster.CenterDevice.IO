using CenterDevice.Rest.Clients.Common;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Linq;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Folders
{
    public partial class FolderRestClient : CenterDeviceRestClient, IFolderRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "folder/";
            }
        }

        public FolderRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        public Folder GetFolder(string userId, string id, string[] fields)
        {
            var folderRequest = CreateRestRequest(URI_RESOURCE + id, Method.Get);
            if (fields != null)
            {
                folderRequest.AddQueryParameter(RestApiConstants.FIELDS, string.Join(",", fields));
            }

            var result = Execute<Folder>(GetOAuthInfo(userId), folderRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<Folder>(HttpStatusCode.OK));
        }

        public void DeleteFolder(string userId, string id)
        {
            var folderRequest = CreateRestRequest(URI_RESOURCE + id, Method.Delete, ContentType.APPLICATION_JSON);

            var result = Execute(GetOAuthInfo(userId), folderRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public FolderEraseResponse EraseFolder(string userId, string id, bool onlyOwnedDocuments)
        {
            var eraseFolder = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.ACTION] = RestApiConstants.ERASE;
            parameters[RestApiConstants.PARAMS] = CreateEraseParameters(onlyOwnedDocuments);
            eraseFolder.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = Execute<FolderEraseResponse>(GetOAuthInfo(userId), eraseFolder);
            return UnwrapResponse(result, new StatusCodeResponseHandler<FolderEraseResponse>(new List<HttpStatusCode>() { HttpStatusCode.NoContent, HttpStatusCode.OK }));
        }

        private static JObject CreateEraseParameters(bool onlyOwnedDocuments)
        {
#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var eraseParameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            eraseParameters[RestApiConstants.ONLY_OWNED_DOCUMENTS] = onlyOwnedDocuments;
            return eraseParameters;
        }

        public void RenameFolder(string userId, string id, string newName)
        {
            var folderRequest = CreateRestRequest(URI_RESOURCE + id, Method.Put, ContentType.APPLICATION_JSON);
            folderRequest.AddJsonBody(new { name = newName });

            var result = Execute(GetOAuthInfo(userId), folderRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void RemoveDocument(string userId, string documentId, string folderId, bool removeFromCollection)
        {
            var removeDocumentRequest = CreateRestRequest(URI_RESOURCE + folderId, Method.Post, ContentType.APPLICATION_JSON);

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var removeDetails = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            removeDetails[RestApiConstants.DOCUMENTS] = new JArray(documentId);
            removeDetails[RestApiConstants.REMOVE_FROM_COLLECTION] = removeFromCollection;

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.ACTION] = RestApiConstants.REMOVE_DOCUMENTS;
            parameters[RestApiConstants.PARAMS] = removeDetails;
            removeDocumentRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = Execute(GetOAuthInfo(userId), removeDocumentRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void AddDocument(string userId, string documentId, string targetFolderId)
        {
            List<string> documentsIds = new List<string>(new string[] { documentId });
            var addOrRemoveDocumentRequest = CreateRestRequest(URI_RESOURCE + targetFolderId, Method.Post, ContentType.APPLICATION_JSON);
            addOrRemoveDocumentRequest.AddJsonBody(new { action = RestApiConstants.ADD_DOCUMENTS, @params = new { documents = documentsIds } });

            var result = Execute(GetOAuthInfo(userId), addOrRemoveDocumentRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void MoveFolder(string userId, string folderId, string targetFolderId, string targetCollectionId)
        {
            var moveFolderRequest = CreateRestRequest(URI_RESOURCE + folderId, Method.Put, ContentType.APPLICATION_JSON);

            if (targetFolderId == null || targetFolderId == RestApiConstants.NONE)
            {
                moveFolderRequest.AddJsonBody(new { parent = RestApiConstants.NONE, collection = targetCollectionId });
            }
            else
            {
                moveFolderRequest.AddJsonBody(new { parent = targetFolderId });
            }

            var result = Execute(GetOAuthInfo(userId), moveFolderRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public SharingResponse ShareFolder(string userId, string folderId, IEnumerable<string> userIds, IEnumerable<string> groupIds)
        {
            return UpdateFolderSharing(userId, folderId, userIds, groupIds, RestApiConstants.SHARE);
        }

        public SharingResponse UnshareFolder(string userId, string folderId, IEnumerable<string> userIds, IEnumerable<string> groupIds)
        {
            return UpdateFolderSharing(userId, folderId, userIds, groupIds, RestApiConstants.UNSHARE);
        }

        private SharingResponse UpdateFolderSharing(string userId, string folderId, IEnumerable<string> userIds, IEnumerable<string> groupIds, string sHARE)
        {
            var shareFolder = CreateRestRequest(URI_RESOURCE + folderId, Method.Post, ContentType.APPLICATION_JSON);

            var sharingDetails = new JObject();
            if (userIds?.Any() == true)
            {
                sharingDetails[RestApiConstants.USERS] = new JArray(userIds);
            }
            if (groupIds?.Any() == true)
            {
                sharingDetails[RestApiConstants.GROUPS] = new JArray(groupIds);
            }

#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.ACTION] = sHARE;
            parameters[RestApiConstants.PARAMS] = sharingDetails;
            shareFolder.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = Execute<SharingResponse>(GetOAuthInfo(userId), shareFolder);
            return UnwrapResponse(result, new StatusCodeResponseHandler<SharingResponse>(new List<HttpStatusCode>() { HttpStatusCode.NoContent, HttpStatusCode.OK }));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element