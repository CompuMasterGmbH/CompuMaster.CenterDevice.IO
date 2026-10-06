using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Collections.Generic;
using System.Collections;
using System;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupsRestClient : CenterDeviceRestClient, IGroupsRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "groups/";
            }
        }

        public GroupsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        public GroupList GetAllGroups(string userId, CenterDevice.Model.Groups.GroupsFilter filter)
        {
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

            var result = Execute<GroupList>(GetOAuthInfo(userId), request);
            if (result.StatusCode == HttpStatusCode.NoContent)
                return new GroupList();
            else
                return UnwrapResponse(result, new StatusCodeResponseHandler<GroupList>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element