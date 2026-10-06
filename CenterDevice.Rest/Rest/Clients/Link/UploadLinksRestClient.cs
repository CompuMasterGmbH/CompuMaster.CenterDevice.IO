using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Link
{
    public partial class UploadLinksRestClient : CenterDeviceRestClient, IUploadLinksRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "upload-links";
            }
        }

        [Inject]
        public UploadLinksRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix, string uploadLinkBaseUrl) : base(oauthInfo, configuration, errorHandler, apiVersionPrefix)
        {
            this.UploadLinkBaseUrl = uploadLinkBaseUrl;
        }

        private readonly string UploadLinkBaseUrl;

        public UploadLinks GetAllUploadLinks(string userId)
        {
            var getUploadLinksRequest = CreateRestRequest(URI_RESOURCE, Method.Get);

            var result = Execute<UploadLinks>(GetOAuthInfo(userId), getUploadLinksRequest);
            UploadLinks resultUnwrapped = UnwrapResponse(result, new StatusCodeResponseHandler<UploadLinks>(HttpStatusCode.OK));
            foreach (UploadLink item in resultUnwrapped.UploadLinksList)
            {
                item.UploadLinkBaseUrl = this.UploadLinkBaseUrl;
            }
            return resultUnwrapped;
        }

        //public LinkCreationResponse CreateDocumentLink(string userId, string id, LinkAccessControl accessControl)
        //{
        //    return CreateLink(userId, RestApiConstants.DOCUMENT_ID, id, accessControl);
        //}

        //public LinkCreationResponse CreateFolderLink(string userId, string id, LinkAccessControl accessControl)
        //{
        //    return CreateLink(userId, RestApiConstants.FOLDER, id, accessControl);
        //}

        public UploadLinkCreationResponse CreateCollectionLink(string userId, string collectionId, string name, List<string> tags, DateTime? expiryDate, int? maxDocuments, string password, bool? emailCreatorOnUpload)
        {
            if (collectionId == null)
            {
                throw new ArgumentException("Id and field need to be set.");
            }

            var createLinkRequest = CreateRestRequest(URI_RESOURCE, Method.Post);
            JObject parameters = CreateJsonBody(collectionId, name, tags, expiryDate, maxDocuments, password, emailCreatorOnUpload);

            createLinkRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = Execute<UploadLinkCreationResponse>(GetOAuthInfo(userId), createLinkRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<UploadLinkCreationResponse>(HttpStatusCode.Created));
        }

        private static JObject CreateJsonBody(string collectionId, string name, List<string> tags, DateTime? expiryDate, int? maxDocuments, string password, bool? emailCreatorOnUpload)
        {
#pragma warning disable IDE0028 // Initialisierung der Sammlung vereinfachen
            var parameters = new JObject();
#pragma warning restore IDE0028 // Initialisierung der Sammlung vereinfachen
            parameters[RestApiConstants.COLLECTION] = collectionId;
            if (name != null) parameters[RestApiConstants.NAME] = name;
            if (tags != null) parameters[RestApiConstants.TAGS] = JsonConvert.SerializeObject(tags);
            if (expiryDate.HasValue) parameters[RestApiConstants.EXPIRY_DATE] = expiryDate;
            if (maxDocuments.HasValue) parameters[RestApiConstants.MAX_DOCUMENTS] = maxDocuments;
            if (password != null) parameters[RestApiConstants.PASSWORD] = password;
            if (emailCreatorOnUpload.HasValue) parameters[RestApiConstants.EMAIL_CREATOR_ON_UPLOAD] = emailCreatorOnUpload;
            return parameters;
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element