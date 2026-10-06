using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Collections.Generic;
using System;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.User
{
    public partial class UsersRestClient : CenterDeviceRestClient, IUsersRestClient
    {
        private string URI_RESOURCE
        {
            get
            {
                return this.ApiVersionPrefix + "users/";
            }
        }

        public UsersRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix) : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }

        /// <summary>
        /// Get a list of all users
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="userStatuses">A list of user statuses as defined at UserData.UserStatus</param>
        /// <returns></returns>
        public UserList<BaseUserData> GetAllUsers(string userId, string[] userStatuses)
        {
            if (userStatuses == null) throw new ArgumentNullException(nameof(userStatuses));
            if (userStatuses.Length == 0) throw new ArgumentException("At least one user status required", nameof(userStatuses));
            var request = CreateRestRequest(URI_RESOURCE + "?status=" + string.Join(",", userStatuses), Method.Get);

            var result = Execute<UserList<BaseUserData>>(GetOAuthInfo(userId), request);
            if (result.StatusCode == HttpStatusCode.NoContent)
                return new UserList<BaseUserData>();
            else
                return UnwrapResponse(result, new StatusCodeResponseHandler<UserList<BaseUserData>>(HttpStatusCode.OK));
        }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element