using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.User
{
    public partial class UserSettingsRestClient
    {
        /// <summary>Performs the get user settings operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<UserSettings> GetUserSettingsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantRequest = CreateRestRequest(string.Format(URI_RESOURCE, userId), Method.Get);

            var response = (await ExecuteAsync<UserSettings>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), tenantRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<UserSettings>(HttpStatusCode.OK));
        }
    }
}
