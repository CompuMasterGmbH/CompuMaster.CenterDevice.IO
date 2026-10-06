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
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Performs the search operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="query">The query for this operation.</param>
        /// <param name="collections">The collections for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSearchResults<DocumentFullMetadata>> SearchAsync(string userId, string query, List<string> collections, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await SearchAsync<DocumentFullMetadata>(userId, query, collections, 0, MAX_DOCUMENT_ROWS, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the search operation using asynchronous HTTP I/O.</summary>
        /// <typeparam name="T">The response metadata type.</typeparam>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="query">The query for this operation.</param>
        /// <param name="collections">The collections for this operation.</param>
        /// <param name="offset">The offset for this operation.</param>
        /// <param name="rows">The rows for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSearchResults<T>> SearchAsync<T>(string userId, string query, List<string> collections, int offset, int rows, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);

            searchRequest.AddJsonBody(new { action = RestApiConstants.SEARCH, @params = new { @query = new { text = query }, filter = new { collections = collections }, offset = offset, rows = rows } });

            return UnwrapResponse((await ExecuteAsync<DocumentSearchResults<T>>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), searchRequest, cancellationToken).ConfigureAwait(false)), new StatusCodeResponseHandler<DocumentSearchResults<T>>(HttpStatusCode.OK));
        }

        /// <summary>Performs the get operation using asynchronous HTTP I/O.</summary>
        /// <typeparam name="T">The response metadata type.</typeparam>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="documentIds">The document ids for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="offset">The offset for this operation.</param>
        /// <param name="rows">The rows for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSearchResults<T>> GetAsync<T>(string userId, string collectionId, IEnumerable<string> documentIds, string folderId, int offset, int rows, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetMetadataAsync<T>(userId, collectionId, documentIds, folderId, FieldUtils.GetAllFields(typeof(T)), null, offset, rows, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get operation using asynchronous HTTP I/O.</summary>
        /// <typeparam name="T">The response metadata type.</typeparam>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="documentIds">The document ids for this operation.</param>
        /// <param name="folderId">The folder id for this operation.</param>
        /// <param name="lastChangeTo">The last change to for this operation.</param>
        /// <param name="offset">The offset for this operation.</param>
        /// <param name="rows">The rows for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSearchResults<T>> GetAsync<T>(string userId, string collectionId, IEnumerable<string> documentIds, string folderId, DateTime? lastChangeTo, int offset, int rows, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetMetadataAsync<T>(userId, collectionId, documentIds, folderId, FieldUtils.GetAllFields(typeof(T)), lastChangeTo, offset, rows, cancellationToken).ConfigureAwait(false));
        }

        private async Task<DocumentSearchResults<T>> GetMetadataAsync<T>(string userId, string collectionId, IEnumerable<string> documentIds, string folderId, IEnumerable<string> includes, DateTime? lastChangeTo, int offset, int rows, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            var result = (await ExecuteAsync<DocumentSearchResults<T>>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), searchRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<DocumentSearchResults<T>>(HttpStatusCode.OK));
        }

        /// <summary>Performs the search operation using asynchronous HTTP I/O.</summary>
        /// <typeparam name="T">The response metadata type.</typeparam>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="request">The request for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentSearchResults<T>> SearchAsync<T>(string userId, DocumentSearchRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            var result = (await ExecuteAsync<DocumentSearchResults<T>>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), searchRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<DocumentSearchResults<T>>(HttpStatusCode.OK));
        }

        /// <summary>Performs the upload document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="path">The path for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="parentId">The parent id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadDocumentResponse> UploadDocumentAsync(string userId, string filename, string path, string collectionId, string parentId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await UploadDocumentAsync(userId, filename, path, null, new List<string>() { collectionId }, new List<string>() { parentId }, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the upload document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="fileStreamData">The file stream data for this operation.</param>
        /// <param name="collectionId">The collection id for this operation.</param>
        /// <param name="parentId">The parent id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadDocumentResponse> UploadDocumentAsync(string userId, string filename, System.Func<Stream> fileStreamData, string collectionId, string parentId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var upload = new UploadStreamSource(fileStreamData))
            {
                return (await UploadDocumentAsync(userId, filename, fileStreamData, null, new List<string>() { collectionId }, new List<string>() { parentId }, cancellationToken).ConfigureAwait(false));
            }
        }

        /// <summary>Performs the upload document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="path">The path for this operation.</param>
        /// <param name="documentDate">The document date for this operation.</param>
        /// <param name="collectionIds">The collection ids for this operation.</param>
        /// <param name="folderIds">The folder ids for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadDocumentResponse> UploadDocumentAsync(string userId, string filename, string path, DateTime? documentDate, List<string> collectionIds, List<string> folderIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestRequest uploadRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.MULTIPART_FORM_DATA);
            uploadRequest.AlwaysMultipartFormData = true;
            uploadRequest.AddParameter(new BodyParameter(RestApiConstants.METADATA, GenerateDocumentUploadJson(filename, path, documentDate, collectionIds, folderIds), "application/json"));
            DocumentStreamUtils.AddFileToUpload(uploadRequest, RestApiConstants.DOCUMENT, path, streamWrapper, cancellationToken);
            uploadRequest.Timeout = new TimeSpan(0, 0, 0, 0, int.MaxValue);

            var result = (await ExecuteAsync<UploadDocumentResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), uploadRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<UploadDocumentResponse>(HttpStatusCode.Created));
        }

        /// <summary>Performs the upload document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="fileStreamData">The file stream data for this operation.</param>
        /// <param name="documentDate">The document date for this operation.</param>
        /// <param name="collectionIds">The collection ids for this operation.</param>
        /// <param name="folderIds">The folder ids for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UploadDocumentResponse> UploadDocumentAsync(string userId, string filename, System.Func<Stream> fileStreamData, DateTime? documentDate, List<string> collectionIds, List<string> folderIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var upload = new UploadStreamSource(fileStreamData))
            {
                RestRequest uploadRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.MULTIPART_FORM_DATA);
                uploadRequest.AlwaysMultipartFormData = true;
                uploadRequest.AddParameter(new BodyParameter(RestApiConstants.METADATA, GenerateDocumentUploadJson(filename, () => upload.MetadataStream, documentDate, collectionIds, folderIds), "application/json"));
                DocumentStreamUtils.AddFileToUpload(uploadRequest, RestApiConstants.DOCUMENT, upload.Open, streamWrapper, cancellationToken);
                uploadRequest.Timeout = new TimeSpan(0, 0, 0, 0, int.MaxValue);

                var result = (await ExecuteAsync<UploadDocumentResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), uploadRequest, cancellationToken).ConfigureAwait(false));
                return UnwrapResponse(result, new StatusCodeResponseHandler<UploadDocumentResponse>(HttpStatusCode.Created));
            }
        }

        /// <summary>Performs the delete documents operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="ids">The ids for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DeleteDocumentsResponse> DeleteDocumentsAsync(string userId, List<string> ids, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deleteRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);
            deleteRequest.AddJsonBody(new { action = RestApiConstants.DELETE, @params = new { documents = ids } });

            var result = (await ExecuteAsync<DeleteDocumentsResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), deleteRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<DeleteDocumentsResponse>(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent }));
        }

        /// <summary>Performs the move documents operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="documentIds">The document ids for this operation.</param>
        /// <param name="srcCollection">The src collection for this operation.</param>
        /// <param name="srcFolder">The src folder for this operation.</param>
        /// <param name="dstCollection">The dst collection for this operation.</param>
        /// <param name="dstFolder">The dst folder for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task MoveDocumentsAsync(string userId, IEnumerable<string> documentIds, string srcCollection, string srcFolder, string dstCollection, string dstFolder, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            var result = (await ExecuteAsync((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), moveRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
