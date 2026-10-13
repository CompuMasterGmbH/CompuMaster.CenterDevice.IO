using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.ResponseHandler;
using RestSharp;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    public abstract partial class CenterDeviceRestClient
    {
        internal async Task<DocumentRemovalResult> ExecuteDocumentRemovalAsync(string userId, string resource, Method method,
            object body, string[] selected, bool? includeErrorInfo, bool single, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var request = CreateRestRequest(resource, method, "application/json");
            if (body != null) request.AddJsonBody(body);
            if (includeErrorInfo.HasValue) request.AddQueryParameter("include-error-info", includeErrorInfo.Value ? "true" : "false");
            var authorization = await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false);
            var response = await ExecuteAsync(authorization, request, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateResponse(response, new StatusCodeResponseHandler(new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NoContent }));
            return response.StatusCode == HttpStatusCode.NoContent
                ? new DocumentRemovalResult(new DocumentRemovalFailure[0], null)
                : DocumentRemovalJson.Read(response.Content, selected, includeErrorInfo == true, single);
        }
    }
}
