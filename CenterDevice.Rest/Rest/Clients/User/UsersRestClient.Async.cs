using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UsersRestClient
    {
        /// <summary>Performs the get all users operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="userStatuses">The user statuses for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UserList<BaseUserData>> GetAllUsersAsync(string userId, string[] userStatuses, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (userStatuses == null) throw new ArgumentNullException(nameof(userStatuses));
            if (userStatuses.Length == 0) throw new ArgumentException("At least one user status required", nameof(userStatuses));
            var request = CreateRestRequest(URI_RESOURCE + "?status=" + string.Join(",", userStatuses), Method.Get);

            var result = (await ExecuteAsync<UserList<BaseUserData>>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false));
            if (result.StatusCode == HttpStatusCode.NoContent)
                return new UserList<BaseUserData>();
            else
                return UnwrapResponse(result, new StatusCodeResponseHandler<UserList<BaseUserData>>(HttpStatusCode.OK));
        }
    }
}
