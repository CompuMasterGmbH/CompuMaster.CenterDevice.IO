using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Tenant
{
    public partial class TenantSettingsRestClient
    {
        /// <summary>Performs the get tenant settings operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="tenantId">The tenant id for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation and its result.</returns>
        public async Task<TenantSettings> GetTenantSettingsAsync(string userId, string tenantId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantRequest = CreateRestRequest(string.Format(URI_RESOURCE, tenantId), Method.Get);

            var response = (await ExecuteAsync<TenantSettings>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), tenantRequest, cancellationToken).ConfigureAwait(false));
            return UnwrapResponse(response, new StatusCodeResponseHandler<TenantSettings>(HttpStatusCode.OK));
        }

        /// <summary>Performs the update tenant settings operation using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user id for this operation.</param>
        /// <param name="tenantId">The tenant id for this operation.</param>
        /// <param name="settingName">The setting name for this operation.</param>
        /// <param name="users">The users for this operation.</param>
        /// <param name="roles">The roles for this operation.</param>
        /// <param name="cancellationToken">Cancels queue admission and active HTTP I/O.</param>
        /// <returns>A task representing the operation.</returns>
        public async Task UpdateTenantSettingsAsync(string userId, string tenantId, string settingName, IEnumerable<string> users, IEnumerable<string> roles, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tenantRequest = CreateRestRequest(string.Format(URI_RESOURCE, tenantId), Method.Put, ContentType.APPLICATION_JSON);

            JObject parameter = new JObject();
            if (users != null)
            {
                parameter[RestApiConstants.USERS] = JArray.FromObject(users);
            }
            if (roles != null)
            {
                parameter[RestApiConstants.ROLES] = JArray.FromObject(roles);
            }

            var settingsUpdate = new JObject
            {
                [settingName] = parameter
            };
            tenantRequest.AddParameter(ContentType.APPLICATION_JSON, settingsUpdate.ToString(), ParameterType.RequestBody);

            var response = (await ExecuteAsync<TenantSettings>((await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false)), tenantRequest, cancellationToken).ConfigureAwait(false));
            ValidateResponse(response, new StatusCodeResponseHandler<TenantSettings>(HttpStatusCode.NoContent));
        }
    }
}
