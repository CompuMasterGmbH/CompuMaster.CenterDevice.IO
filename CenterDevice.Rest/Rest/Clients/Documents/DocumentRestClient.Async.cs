using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using CenterDevice.Rest.Utils;
using Ninject;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Performs the get document metadata operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DocumentFullMetadata> GetDocumentMetadataAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (await GetDocumentMetadataAsync<DocumentFullMetadata>(userId, id, null, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>Performs the get document metadata operation using asynchronous HTTP I/O.</summary>
        /// <typeparam name="T">The response metadata type.</typeparam>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="version">The version for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<T> GetDocumentMetadataAsync<T>(string userId, string id, long? version = null, CancellationToken cancellationToken = default(CancellationToken)) where T : new()
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = URI_RESOURCE + id;
            if (version != null && version > 0)
            {
                path += ";" + RestApiConstants.VERSION + "=" + version;
            }

            var metadataRequest = CreateRestRequest(path, Method.Get);
            metadataRequest.AddQueryParameter(RestApiConstants.INCLUDES, FieldUtils.GetFieldIncludes(typeof(T)));

            var result = (await ExecuteAsync<T>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), metadataRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<T>(HttpStatusCode.OK));
        }

        /// <summary>Performs the upload new version operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="filepath">The filepath for this operation.</param>
        /// <param name="token">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<NewVersionUploadResponse> UploadNewVersionAsync(string userId, string id, string filename, string filepath, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            RestRequest newVersionRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.MULTIPART_FORM_DATA);
            newVersionRequest.AlwaysMultipartFormData = true;
            newVersionRequest.AddParameter(new BodyParameter(RestApiConstants.METADATA, GetMetadata(filename, filepath), "application/json"));
            DocumentStreamUtils.AddFileToUpload(newVersionRequest, "document", filepath, streamWrapper, token);
            newVersionRequest.Timeout = new TimeSpan(0, 0, 0, 0, int.MaxValue);

            var result = (await ExecuteAsync<NewVersionUploadResponse>((await GetOAuthInfoAsync(userId, token).ConfigureAwait(false)), newVersionRequest, token).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<NewVersionUploadResponse>(HttpStatusCode.Created));
        }

        /// <summary>Performs the upload new version operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="fileDataStream">The file data stream for this operation.</param>
        /// <param name="token">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<NewVersionUploadResponse> UploadNewVersionAsync(string userId, string id, string filename, System.Func<Stream> fileDataStream, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var upload = new UploadStreamSource(fileDataStream))
            {
                RestRequest newVersionRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.MULTIPART_FORM_DATA);
                newVersionRequest.AlwaysMultipartFormData = true;
                newVersionRequest.AddParameter(new BodyParameter(RestApiConstants.METADATA, GetMetadata(filename, () => upload.MetadataStream), "application/json"));
                DocumentStreamUtils.AddFileToUpload(newVersionRequest, "document", upload.Open, streamWrapper, token);
                newVersionRequest.Timeout = new TimeSpan(0, 0, 0, 0, int.MaxValue);

                var result = (await ExecuteAsync<NewVersionUploadResponse>((await GetOAuthInfoAsync(userId, token).ConfigureAwait(false)), newVersionRequest, token).ConfigureAwait(false));
                return UnwrapResponse(result, new StatusCodeResponseHandler<NewVersionUploadResponse>(HttpStatusCode.Created));
            }
        }

        /// <summary>Performs the rename document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="filename">The filename for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<NewVersionUploadResponse> RenameDocumentAsync(string userId, string id, string filename, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestRequest renameRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            renameRequest.AddJsonBody(new { action = RestApiConstants.RENAME, @params = new { filename = filename } });

            var result = (await ExecuteAsync<NewVersionUploadResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), renameRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new RenameDocumentResponseHandler());
        }

        /// <summary>Performs the add lock operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task AddLockAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestRequest renameRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            renameRequest.AddJsonBody(new { action = RestApiConstants.ADD_LOCK, @params = new { locks = new string[] { RestApiConstants.CREATE_NEW_VERSION } } });

            var result = (await ExecuteAsync<NewVersionUploadResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), renameRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the remove lock operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="id">The id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task RemoveLockAsync(string userId, string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestRequest renameRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            renameRequest.AddJsonBody(new { action = RestApiConstants.REMOVE_LOCK, @params = new { locks = new string[] { RestApiConstants.CREATE_NEW_VERSION } } });

            var result = (await ExecuteAsync<NewVersionUploadResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), renameRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        /// <summary>Performs the delete document operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="documentId">The document id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<DeleteDocumentsResponse> DeleteDocumentAsync(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RestRequest delete = CreateRestRequest(URI_RESOURCE + documentId, Method.Delete, ContentType.APPLICATION_JSON);

            var result = (await ExecuteAsync<DeleteDocumentsResponse>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), delete, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<DeleteDocumentsResponse>(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent }));
        }
    }
}
