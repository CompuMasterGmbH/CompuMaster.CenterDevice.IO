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
using Newtonsoft.Json;
using System.Globalization;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient : CenterDeviceRestClient, IDocumentRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "document/";
            }
        }

        private const int PREVIEW_TIMEOUT = 10 * 1000;

        private readonly string userAgent;
        private readonly IStreamWrapper streamWrapper;

        [Inject]
        public DocumentRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, IStreamWrapper streamWrapper, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix)
        {
            userAgent = configuration.UserAgent;
            this.streamWrapper = streamWrapper;
        }

        public DocumentFullMetadata GetDocumentMetadata(string userId, string id)
        {
            return GetDocumentMetadata<DocumentFullMetadata>(userId, id, null);
        }

        public T GetDocumentMetadata<T>(string userId, string id, long? version = null) where T : new()
        {
            string path = URI_RESOURCE + id;
            if (version != null && version > 0)
            {
                path += ";" + RestApiConstants.VERSION + "=" + version;
            }

            var metadataRequest = CreateRestRequest(path, Method.Get);
            metadataRequest.AddQueryParameter(RestApiConstants.INCLUDES, FieldUtils.GetFieldIncludes(typeof(T)));

            var result = Execute<T>(GetOAuthInfo(userId), metadataRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<T>(HttpStatusCode.OK));
        }

        public Stream DownloadPreview(string userId, string id, PreviewSize size, long? version)
        {
            return DownloadPreviewAsync(userId, id, size, version).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Stream DownloadDocument(string userId, string id)
        {
            return DownloadDocument(userId, id, null, null);
        }

        public Stream DownloadDocument(string userId, string id, long? version, long? range)
        {
            return DownloadDocumentAsync(userId, id, version, range).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private Uri GetDocumentDownloadUri(string id, long? version)
        {
            var path = URI_RESOURCE + id;
            if (version != null)
            {
                path += ";" + RestApiConstants.VERSION + "=" + version;
            }
            return new Uri(new Uri(CustomOptionBaseAddress), path);
        }

        public NewVersionUploadResponse UploadNewVersion(string userId, string id, string filename, string filepath)
        {
            return UploadNewVersion(userId, id, filename, filepath, new CancellationToken());
        }

        public NewVersionUploadResponse UploadNewVersion(string userId, string id, string filename, System.Func<Stream> fileDataStream)
        {
            return UploadNewVersion(userId, id, filename, fileDataStream, new CancellationToken());
        }

        public NewVersionUploadResponse UploadNewVersion(string userId, string id, string filename, string filepath, CancellationToken token)
        {
            return UploadNewVersionAsync(userId, id, filename, filepath, token).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        public NewVersionUploadResponse UploadNewVersion(string userId, string id, string filename, System.Func<Stream> fileDataStream, CancellationToken token)
        {
            return UploadNewVersionAsync(userId, id, filename, fileDataStream, token).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private string GetMetadata(string filename, System.Func<Stream> fileDataStream)
        {
            return VersionMetadata(filename, fileDataStream().Length);
        }
        private string GetMetadata(string filename, string fileFullpath)
        {
            return VersionMetadata(filename, GetFileSize(fileFullpath));
        }

        private static string VersionMetadata(string filename, long length)
        {
            return JsonConvert.SerializeObject(new { metadata = new { document = new { filename, size = length.ToString(CultureInfo.InvariantCulture) } } });
        }

        public NewVersionUploadResponse RenameDocument(string userId, string id, string filename)
        {
            RestRequest renameRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            renameRequest.AddJsonBody(new { action = RestApiConstants.RENAME, @params = new { filename = filename } });

            var result = Execute<NewVersionUploadResponse>(GetOAuthInfo(userId), renameRequest);
            return UnwrapResponse(result, new RenameDocumentResponseHandler());
        }

        public void AddLock(string userId, string id)
        {
            RestRequest renameRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            renameRequest.AddJsonBody(new { action = RestApiConstants.ADD_LOCK, @params = new { locks = new string[] { RestApiConstants.CREATE_NEW_VERSION } } });

            var result = Execute<NewVersionUploadResponse>(GetOAuthInfo(userId), renameRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        public void RemoveLock(string userId, string id)
        {
            RestRequest renameRequest = CreateRestRequest(URI_RESOURCE + id, Method.Post, ContentType.APPLICATION_JSON);
            renameRequest.AddJsonBody(new { action = RestApiConstants.REMOVE_LOCK, @params = new { locks = new string[] { RestApiConstants.CREATE_NEW_VERSION } } });

            var result = Execute<NewVersionUploadResponse>(GetOAuthInfo(userId), renameRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }

        private string GetFileName(string filepath)
        {
            return Path.GetFileName(filepath);
        }

        private long GetFileSize(string filepath)
        {
            return new FileInfo(filepath).Length;
        }

        public DeleteDocumentsResponse DeleteDocument(string userId, string documentId)
        {
            RestRequest delete = CreateRestRequest(URI_RESOURCE + documentId, Method.Delete, ContentType.APPLICATION_JSON);

            var result = Execute<DeleteDocumentsResponse>(GetOAuthInfo(userId), delete);
            return UnwrapResponse(result, new StatusCodeResponseHandler<DeleteDocumentsResponse>(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent }));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
