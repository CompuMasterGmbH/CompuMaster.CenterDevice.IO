using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupsRestClient
    {
        /// <summary>Performs the get all groups operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="filter">The filter for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<GroupList> GetAllGroupsAsync(string userId, CenterDevice.Model.Groups.GroupsFilter filter, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string filterExpression;
            switch (filter)
            {
                case CenterDevice.Model.Groups.GroupsFilter.MembershipsOfCurrentUser:
                    filterExpression = "?all=false";
                    break;
                case CenterDevice.Model.Groups.GroupsFilter.AllVisibleGroupsForCurrentUser:
                    filterExpression = "?all=true";
                    break;
                default:
                    throw new ArgumentException("Invalid filter", nameof(filter));
            }
            var request = CreateRestRequest(URI_RESOURCE + filterExpression, Method.Get);

            var result = (await ExecuteAsync<GroupList>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false));
            if (result.StatusCode == HttpStatusCode.NoContent)
                return new GroupList();
            else
                return UnwrapResponse(result, new StatusCodeResponseHandler<GroupList>(HttpStatusCode.OK));
        }
    }
}
