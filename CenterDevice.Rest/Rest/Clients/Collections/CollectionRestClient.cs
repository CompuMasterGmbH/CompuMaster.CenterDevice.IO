using CenterDevice.Rest.Clients.Common;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using CenterDevice.Rest.Utils;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient : CenterDeviceRestClient, ICollectionRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "collection";
            }
        }

        public CollectionRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfo, configuration, errorHandler, apiVersionPrefix) { }

        public DocumentSharingResponse RemoveDocumentFromCollection(string userId, string documentId, string collectionId)
        {
            List<string> documents = new List<string>();
            documents.Add(documentId);

            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);
            request.AddJsonBody(new { action = RestApiConstants.REMOVE_DOCUMENTS, @params = new { documents = documents } });

            return UnwrapResponse(Execute<DocumentSharingResponse>(GetOAuthInfo(userId), request), new StatusCodeResponseHandler<DocumentSharingResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        public DocumentSharingResponse AddDocumentToCollection(string userId, string documentId, string collectionId)
        {
            List<string> documents = new List<string>();
            documents.Add(documentId);

            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);
            request.AddJsonBody(new { action = RestApiConstants.ADD_DOCUMENTS, @params = new { documents = documents } });

            return UnwrapResponse(Execute<DocumentSharingResponse>(GetOAuthInfo(userId), request), new StatusCodeResponseHandler<DocumentSharingResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        public CollectionEraseResponse EraseCollection(string userId, string collectionId, bool onlyOwnedDocuments)
        {
            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject();
            parameters[RestApiConstants.ACTION] = RestApiConstants.ERASE;
            parameters[RestApiConstants.PARAMS] = CreateEraseParameters(onlyOwnedDocuments);
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            return UnwrapResponse(Execute<CollectionEraseResponse>(GetOAuthInfo(userId), request), new StatusCodeResponseHandler<CollectionEraseResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        /// <summary>
        /// Share a collection to additional users or groups
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="collectionId"></param>
        /// <param name="users"></param>
        /// <param name="groups"></param>
        /// <returns>Null on success or data on failed elements</returns>
        public SharingResponse ShareCollection(string userId, string collectionId, IEnumerable<string> users, IEnumerable<string> groups)
        {
            return UpdateCollectionSharing(userId, collectionId, users, groups, RestApiConstants.SHARE);
        }

        /// <summary>
        /// Remove sharings from a collection
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="collectionId"></param>
        /// <param name="users"></param>
        /// <param name="groups"></param>
        /// <returns>Null on success or data on failed elements</returns>
        public SharingResponse UnshareCollection(string userId, string collectionId, IEnumerable<string> users, IEnumerable<string> groups)
        {
            return UpdateCollectionSharing(userId, collectionId, users, groups, RestApiConstants.UNSHARE);
        }

        private SharingResponse UpdateCollectionSharing(string userId, string collectionId, IEnumerable<string> users, IEnumerable<string> groups, string sHARE)
        {
            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject();
            parameters[RestApiConstants.ACTION] = sHARE;
            parameters[RestApiConstants.PARAMS] = CreateSharingParams(users, groups);
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            return UnwrapResponse(Execute<SharingResponse>(GetOAuthInfo(userId), request), new StatusCodeResponseHandler<SharingResponse>(HttpStatusCode.NoContent, HttpStatusCode.OK));
        }

        private JToken CreateSharingParams(IEnumerable<string> users, IEnumerable<string> groups)
        {
            var parameters = new JObject();
            if (users != null)
            {
                parameters[RestApiConstants.USERS] = new JArray(users);
            }
            if (groups != null)
            {
                parameters[RestApiConstants.GROUPS] = new JArray(groups);
            }
            return parameters;
        }

        private static JObject CreateEraseParameters(bool onlyOwnedDocuments)
        {
            var eraseParameters = new JObject();
            eraseParameters[RestApiConstants.ONLY_OWNED_DOCUMENTS] = onlyOwnedDocuments;
            return eraseParameters;
        }

        public void RenameCollection(string userId, string collectionId, string newName)
        {
            var collectionRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Put, ContentType.APPLICATION_JSON);
            collectionRequest.AddJsonBody(new { name = newName });

            var result = Execute(GetOAuthInfo(userId), collectionRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void RenameCollection(OAuthInfo oAuthInfo, string collectionId, string newName)
        {
            var collectionRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Put, ContentType.APPLICATION_JSON);
            collectionRequest.AddJsonBody(new { name = newName });

            var result = Execute(oAuthInfo, collectionRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public Collection GetCollection(string userId, string collectionId)
        {
            return GetCollection(userId, collectionId, RestRequestFields.DEFAULT);
        }

        public Collection GetCollection(string userId, string collectionId, RestRequestFields fields)
        {
            var collectionRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Get);

            if (fields == RestRequestFields.ALL)
            {
                collectionRequest.AddQueryParameter(RestApiConstants.FIELDS, FieldUtils.GetFieldIncludes(typeof(Collection)));
            }
            else if (fields == RestRequestFields.ID)
            {
                collectionRequest.AddQueryParameter(RestApiConstants.FIELDS, RestApiConstants.ID);
            }

            var result = Execute<Collection>(GetOAuthInfo(userId), collectionRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<Collection>(HttpStatusCode.OK));
        }

        public void DeleteCollection(string userId, string collectionId)
        {
            var createCollectionsRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Delete);

            var result = Execute<CreateCollectionResponse>(GetOAuthInfo(userId), createCollectionsRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void DeleteCollection(OAuthInfo oAuthInfo, string collectionId)
        {
            var createCollectionsRequest = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Delete);

            var result = Execute<CreateCollectionResponse>(oAuthInfo, createCollectionsRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void ArchiveCollection(string userId, string collectionId)
        {
            var request = CreateRestRequest(URI_RESOURCE + "/" + collectionId, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject
            {
                [RestApiConstants.ACTION] = RestApiConstants.ARCHIVE
            };
            request.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            RestResponse result = Execute(GetOAuthInfo(userId), request);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element