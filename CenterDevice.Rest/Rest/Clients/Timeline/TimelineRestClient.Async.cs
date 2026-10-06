using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using Ninject;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Timeline
{
    public partial class TimelineRestClient
    {
        /// <summary>Performs the get timeline events operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="startDate">The start date for this operation.</param>
        /// <param name="offset">The offset for this operation.</param>
        /// <param name="rows">The rows for this operation.</param>
        /// <param name="types">The types for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<TimelineSearchResults> GetTimelineEventsAsync(string userId, DateTime startDate, int offset, int rows, List<string> types, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timelineSearchRequest = CreateRestRequest(URI_RESOURCE, Method.Post, ContentType.APPLICATION_JSON);

            timelineSearchRequest.AddJsonBody(new { action = RestApiConstants.SEARCH, @params = new { types = types, offset = offset, rows = rows, timestamp = new { from = toIso8601(startDate) } } });

            var response = (await ExecuteAsync<TimelineSearchResults>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), timelineSearchRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<TimelineSearchResults>(HttpStatusCode.OK));
        }

        /// <summary>Performs the scroll operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="previousId">The previous id for this operation.</param>
        /// <param name="types">The types for this operation.</param>
        /// <param name="rows">The rows for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<TimelineSearchResults> ScrollAsync(string userId, string previousId, List<string> types, int rows = 500, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            var response = (await ExecuteAsync<TimelineSearchResults>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), timelineSearchRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<TimelineSearchResults>(HttpStatusCode.OK));
        }
    }
}
