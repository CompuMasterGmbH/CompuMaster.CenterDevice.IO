using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Linq;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionsRestClient : CenterDeviceRestClient, ICollectionsRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "collections";
            }
        }

        public CollectionsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        public CollectionsResults GetCollections(string userId)
        {
            return GetCollections(userId, null, false);
        }

        public CollectionsResults GetCollections(string userId, IEnumerable<string> ids, bool includeHasFolders)
        {
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Get);
            if (ids != null && ids.Any())
            {
                searchRequest.AddQueryParameter(RestApiConstants.IDS, string.Join(",", ids));
            }

            if (includeHasFolders)
            {
                // has folders is the only on default field
                searchRequest.AddQueryParameter(RestApiConstants.FIELDS, Utils.FieldUtils.GetFieldIncludes(typeof(Collection)));
            }

            //following lines might fail with HttpStatusCode.BadRequest when the user is missing an assigned license
            var result = Execute<CollectionsResults>(GetOAuthInfo(userId), searchRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<CollectionsResults>((new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent })));
        }

        public CreateCollectionResponse CreateCollection(string userId, string collectionName)
        {
            return CreateCollection(GetOAuthInfo(userId), collectionName);
        }

        public CreateCollectionResponse CreateCollection(OAuthInfo oAuthInfo, string collectionName)
        {
            var createCollectionsRequest = CreateRestRequest(URI_RESOURCE, Method.Post);

            createCollectionsRequest.AddJsonBody(new { name = collectionName });

            var result = Execute<CreateCollectionResponse>(oAuthInfo, createCollectionsRequest);
            return UnwrapResponse(result, new StatusCodeResponseHandler<CreateCollectionResponse>(HttpStatusCode.Created));
        }

        public IEnumerable<string> GetCollectionIds(string userId, bool includePublic)
        {
            var searchRequest = CreateRestRequest(URI_RESOURCE, Method.Get);
            searchRequest.AddQueryParameter(RestApiConstants.INCLUDE_PUBLIC, includePublic.ToString());
            searchRequest.AddQueryParameter(RestApiConstants.FIELDS, RestApiConstants.ID);

            var result = Execute(GetOAuthInfo(userId), searchRequest);
            ValidateResponse(result, new StatusCodeResponseHandler(HttpStatusCode.OK, HttpStatusCode.NoContent));
            if (string.IsNullOrWhiteSpace(result.Content))
            {
                return new string[] { };
            }
            return JObject.Parse(result.Content)[RestApiConstants.COLLECTIONS]?.Select(i => i[RestApiConstants.ID]).Values<string>();
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element