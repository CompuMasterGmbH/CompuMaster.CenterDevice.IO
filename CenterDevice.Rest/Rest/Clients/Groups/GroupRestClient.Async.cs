using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Groups
{
    public partial class GroupRestClient
    {
        /// <summary>Performs the get group operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="groupId">The group id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<Group> GetGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(URI_RESOURCE + groupId, Method.Get);

            var result = (await ExecuteAsync<Group>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), request, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(result, new StatusCodeResponseHandler<Group>(HttpStatusCode.OK));
        }
    }
}
