using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Link
{
    public partial class LinksRestClient : CenterDeviceRestClient, ILinksRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "links";
            }
        }

        [Inject]
        public LinksRestClient(IOAuthInfoProvider oauthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfo, configuration, errorHandler, apiVersionPrefix)
        {
        }

        public LinkCreationResponse CreateDocumentLink(string userId, string id, LinkAccessControl accessControl)
        {
            return CreateLink(userId, RestApiConstants.DOCUMENT_ID, id, accessControl);
        }

        public LinkCreationResponse CreateFolderLink(string userId, string id, LinkAccessControl accessControl)
        {
            return CreateLink(userId, RestApiConstants.FOLDER, id, accessControl);
        }

        public LinkCreationResponse CreateCollectionLink(string userId, string id, LinkAccessControl accessControl)
        {
            return CreateLink(userId, RestApiConstants.COLLECTION, id, accessControl);
        }

        private LinkCreationResponse CreateLink(string userId, string field, string id, LinkAccessControl accessControl)
        {
            if (id == null || field == null)
            {
                throw new ArgumentException("Id and field need to be set.");
            }

            var createLinkRequest = CreateRestRequest(URI_RESOURCE, Method.Post);
            JObject parameters = CreateJsonBody(field, id, accessControl);

            createLinkRequest.AddStringBody(parameters.ToString(), ContentType.APPLICATION_JSON);

            var result = Execute<LinkCreationResponse>(GetOAuthInfo(userId), createLinkRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<LinkCreationResponse>(HttpStatusCode.Created));
        }

        private static JObject CreateJsonBody(string field, string id, LinkAccessControl accessControl)
        {
            var parameters = new JObject();
            parameters[field] = id;

            if (accessControl != null)
            {
                parameters[RestApiConstants.ACCESS_CONTROL] = AccessControlConverter.ToJsonObject(accessControl);
            }

            return parameters;
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element