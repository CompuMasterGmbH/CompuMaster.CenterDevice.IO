using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Timeline
{
    public partial class TimelineRestClient : CenterDeviceRestClient, ITimelineRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "timeline";
            }
        }

        public int MaxRows
        {
            get
            {
                return 500;
            }
        }

        [Inject]
        public TimelineRestClient(IOAuthInfoProvider oAuthInfo, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oAuthInfo, configuration, errorHandler, apiVersionPrefix) { }

        public TimelineSearchResults GetTimelineEvents(string userId, DateTime startDate, int offset, int rows, List<string> types)
        {
            var timelineSearchRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);

            timelineSearchRequest.AddJsonBody(new { action = RestApiConstants.SEARCH, @params = new { types = types, offset = offset, rows = rows, timestamp = new { from = toIso8601(startDate) } } });

            var response = Execute<TimelineSearchResults>(GetOAuthInfo(userId), timelineSearchRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<TimelineSearchResults>(HttpStatusCode.OK));
        }

        public TimelineSearchResults Scroll(string userId, string previousId, List<string> types, int rows = 500)
        {
            var timelineSearchRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);

            var parameters = new JObject();
            if (types != null)
            {
                parameters[RestApiConstants.TYPES] = JToken.FromObject(types);
            }
            if (previousId != null)
            {
                parameters[RestApiConstants.PREVIOUS_ID] = previousId;
            }
            parameters[RestApiConstants.ROWS] = rows;

            var request = new JObject();
            request[RestApiConstants.ACTION] = RestApiConstants.SCROLL;
            request[RestApiConstants.PARAMS] = parameters;

            timelineSearchRequest.AddParameter(ContentType.APPLICATION_JSON, request.ToString(), ParameterType.RequestBody);

            var response = Execute<TimelineSearchResults>(GetOAuthInfo(userId), timelineSearchRequest);
            return UnwrapResponse(response, new StatusCodeResponseHandler<TimelineSearchResults>(HttpStatusCode.OK));
        }

        private string toIso8601(DateTime date)
        {
            return date.ToUniversalTime().ToString("s") + "Z";
        }

    }

}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element