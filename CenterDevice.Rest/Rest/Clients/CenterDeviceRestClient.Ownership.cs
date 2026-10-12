using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal static void ValidateNewOwner(string newOwnerId)
        {
            if (string.IsNullOrWhiteSpace(newOwnerId))
                throw new ArgumentException("An explicit new owner user ID is required.", nameof(newOwnerId));
        }

        internal async Task ExecuteOwnershipTransferAsync(string userId, string resource, JObject parameters, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var request = CreateRestRequest(resource, Method.Post, "application/json");
            request.AddStringBody(new JObject { ["action"] = "transfer-ownership", ["params"] = parameters }.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
