using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using CenterDevice.Rest.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient : CenterDeviceRestClient, IDocumentsRestClient
    {
        public const int MAX_DOCUMENT_ROWS = 500;

        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "documents";
            }
        }

        private readonly IStreamWrapper streamWrapper;

        [Inject]
        public DocumentsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, IStreamWrapper streamWrapper, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) 
        { 
            this.streamWrapper = streamWrapper; 
        }

        public DocumentSearchResults<DocumentFullMetadata> Search(string userId, string query, List<string> collections)
        {
            return Search<DocumentFullMetadata>(userId, query, collections, 0, MAX_DOCUMENT_ROWS);
        }

        public DocumentSearchResults<T> Search<T>(string userId, string query, List<string> collections, int offset, int rows)
        {
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);

            searchRequest.AddJsonBody(new { action = RestApiConstants.SEARCH, @params = new { @query = new { text = query }, filter = new { collections = collections }, offset = offset, rows = rows } });

            return UnwrapResponse(Execute<DocumentSearchResults<T>>(GetOAuthInfo(userId), searchRequest), new StatusCodeResponseHandler<DocumentSearchResults<T>>(HttpStatusCode.OK));
        }

        public DocumentSearchResults<T> Get<T>(string userId, string collectionId, IEnumerable<string> documentIds, string folderId, int offset, int rows) where T : new()
        {
            return GetMetadata<T>(userId, collectionId, documentIds, folderId, FieldUtils.GetAllFields(typeof(T)), null, offset, rows);
        }

        public DocumentSearchResults<T> Get<T>(string userId, string collectionId, IEnumerable<string> documentIds, string folderId, DateTime? lastChangeTo, int offset, int rows) where T : new()
        {
            return GetMetadata<T>(userId, collectionId, documentIds, folderId, FieldUtils.GetAllFields(typeof(T)), lastChangeTo, offset, rows);
        }

        private DocumentSearchResults<T> GetMetadata<T>(string userId, string collectionId, IEnumerable<string> documentIds, string folderId, IEnumerable<string> includes, DateTime? lastChangeTo, int offset, int rows) where T : new()
        {
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Get);

            if (collectionId != null)
            {
                searchRequest.AddQueryParameter(RestApiConstants.COLLECTION, collectionId);
            }
            if (documentIds != null && documentIds.Count() > 0)
            {
                searchRequest.AddQueryParameter(RestApiConstants.IDS, string.Join(",", documentIds));
            }
            if (folderId != null)
            {
                searchRequest.AddQueryParameter(RestApiConstants.FOLDER, folderId);
            }
            if (includes != null)
            {
                searchRequest.AddQueryParameter(RestApiConstants.INCLUDES, string.Join(",", includes));
            }
            if (lastChangeTo != null)
            {
                searchRequest.AddQueryParameter(RestApiConstants.LAST_CHANGE_TO, lastChangeTo?.ToString("o"));
            }

            searchRequest.AddQueryParameter(RestApiConstants.OFFSET, offset.ToString());
            searchRequest.AddQueryParameter(RestApiConstants.ROWS, rows.ToString());

            var result = Execute<DocumentSearchResults<T>>(GetOAuthInfo(userId), searchRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<DocumentSearchResults<T>>(HttpStatusCode.OK));
        }

        public DocumentSearchResults<T> Search<T>(string userId, DocumentSearchRequest request)
        {
            JsonSerializer serializer = new JsonSerializer()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);

            JObject parameters = JObject.FromObject(request, serializer);
            var fields = new JObject();
            fields[RestApiConstants.INCLUDES] = JToken.FromObject(FieldUtils.GetAllFields(typeof(T)));
            parameters[RestApiConstants.FIELDS] = fields;

            var body = new JObject();
            body[RestApiConstants.ACTION] = RestApiConstants.SEARCH;
            body[RestApiConstants.PARAMS] = parameters;

            string v = body.ToString();
            searchRequest.AddParameter(ContentType.APPLICATION_JSON, v, ParameterType.RequestBody);

            var result = Execute<DocumentSearchResults<T>>(GetOAuthInfo(userId), searchRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<DocumentSearchResults<T>>(HttpStatusCode.OK));
        }

        public UploadDocumentResponse UploadDocument(string userId, string filename, string path, string collectionId, string parentId, CancellationToken cancellationToken)
        {
            return UploadDocumentAsync(userId, filename, path, collectionId, parentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public UploadDocumentResponse UploadDocument(string userId, string filename, System.Func<Stream> fileStreamData, string collectionId, string parentId, CancellationToken cancellationToken)
        {
            return UploadDocumentAsync(userId, filename, fileStreamData, collectionId, parentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public UploadDocumentResponse UploadDocument(string userId, string filename, string path, DateTime? documentDate, List<string> collectionIds, List<string> folderIds, CancellationToken cancellationToken)
        {
            return UploadDocumentAsync(userId, filename, path, documentDate, collectionIds, folderIds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public UploadDocumentResponse UploadDocument(string userId, string filename, System.Func<Stream> fileStreamData, DateTime? documentDate, List<string> collectionIds, List<string> folderIds, CancellationToken cancellationToken)
        {
            return UploadDocumentAsync(userId, filename, fileStreamData, documentDate, collectionIds, folderIds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public DeleteDocumentsResponse DeleteDocuments(string userId, List<string> ids)
        {
            var deleteRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);
            deleteRequest.AddJsonBody(new { action = RestApiConstants.DELETE, @params = new { documents = ids } });

            var result = Execute<DeleteDocumentsResponse>(GetOAuthInfo(userId), deleteRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<DeleteDocumentsResponse>(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent }));
        }

        public void MoveDocuments(string userId, IEnumerable<string> documentIds, string srcCollection, string srcFolder, string dstCollection, string dstFolder)
        {
            var parameters = new JObject();
            parameters[RestApiConstants.DOCUMENTS] = JArray.FromObject(documentIds);
            parameters[RestApiConstants.SOURCE_FOLDER] = srcFolder ?? RestApiConstants.NONE;
            parameters[RestApiConstants.DESTINATION_FOLDER] = dstFolder ?? RestApiConstants.NONE;
            if (!string.IsNullOrWhiteSpace(srcCollection))
            {
                parameters[RestApiConstants.SOURCE_COLLECTION] = srcCollection;
            }
            if (!string.IsNullOrWhiteSpace(dstCollection))
            {
                parameters[RestApiConstants.DESTINATION_COLLECTION] = dstCollection;
            }

            var body = new JObject();
            body[RestApiConstants.ACTION] = RestApiConstants.MOVE;
            body[RestApiConstants.PARAMS] = parameters;

            var moveRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);
            moveRequest.AddParameter(ContentType.APPLICATION_JSON, body.ToString(), ParameterType.RequestBody);

            var result = Execute(GetOAuthInfo(userId), moveRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        private string GenerateDocumentUploadJson(string filename, string fileFullpath, DateTime? documentDate, List<string> collectionIds, List<string> folderIds)
        {
            var document = new JObject();
            document[RestApiConstants.FILENAME] = filename;
            document[RestApiConstants.SIZE] = new FileInfo(fileFullpath).Length;
            if (documentDate != null)
            {
                document[RestApiConstants.DOCUMENT_DATE] = documentDate.Value;
            }

            var actions = new JObject();
            actions[RestApiConstants.ADD_TO_FOLDER] = folderIds != null ? JToken.FromObject(folderIds) : new JArray();
            actions[RestApiConstants.ADD_TO_COLLECTION] = collectionIds != null ? JToken.FromObject(collectionIds) : new JArray();

            var metadata = new JObject();
            metadata[RestApiConstants.DOCUMENT] = document;
            metadata[RestApiConstants.ACTIONS] = actions;

            var upload = new JObject();
            upload[RestApiConstants.METADATA] = metadata;

            return upload.ToString();
        }

        private string GenerateDocumentUploadJson(string filename, System.Func<Stream> fileDataStream, DateTime? documentDate, List<string> collectionIds, List<string> folderIds)
        {
            var document = new JObject();
            document[RestApiConstants.FILENAME] = filename;
            document[RestApiConstants.SIZE] = fileDataStream().Length;
            if (documentDate != null)
            {
                document[RestApiConstants.DOCUMENT_DATE] = documentDate.Value;
            }

            var actions = new JObject();
            actions[RestApiConstants.ADD_TO_FOLDER] = folderIds != null ? JToken.FromObject(folderIds) : new JArray();
            actions[RestApiConstants.ADD_TO_COLLECTION] = collectionIds != null ? JToken.FromObject(collectionIds) : new JArray();

            var metadata = new JObject();
            metadata[RestApiConstants.DOCUMENT] = document;
            metadata[RestApiConstants.ACTIONS] = actions;

            var upload = new JObject();
            upload[RestApiConstants.METADATA] = metadata;

            return upload.ToString();
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element