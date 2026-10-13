using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Link
{
    public partial class UploadLinksRestClient
    {
        /// <summary>Creates an upload link with copied explicit targets, limits and metadata.</summary>
        /// <inheritdoc cref="CreateConfiguredUploadLinkAsync"/>
        public UploadLinkDetails CreateConfiguredUploadLink(string userId, UploadLinkOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) => CreateConfiguredUploadLinkAsync(userId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an upload link using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="options">Copied explicit creation choices, or null to retain all server defaults without selecting a target.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed immutable identity and original server web URL.</returns>
        /// <remarks>API 2.29 section 5.7.1 supports exclusive optional collection/folder targets, original tags/dates/password and explicit limits/notifications. The server owns uploaded documents on behalf of the creator and enforces visibility and actual limits. No one-week/50-upload/2-GiB defaults, URL reconstruction/distribution or uncertain creation replay is added. Existing collection-only methods remain compatible.</remarks>
        /// <exception cref="ArgumentException">Known choices or target/limit combinations are invalid.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Confirmed identity or server URL metadata is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no upload link was created.</exception>
        public async Task<UploadLinkDetails> CreateConfiguredUploadLinkAsync(string userId, UploadLinkOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var copied = LinkContractJson.UploadSnapshot(options); var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json"); request.AddOrUpdateHeader("Accept", "application/json"); request.AddStringBody(copied.ToString(Formatting.None), RestSharp.ContentType.Json); var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.Created)); return LinkContractJson.Parse(response.Content, data => new UploadLinkDetails(data, response.Content, true)); }
        /// <summary>Retrieves immutable complete metadata for all own upload links.</summary>
        /// <inheritdoc cref="GetUploadLinkDetailsAsync"/>
        public UploadLinkList GetUploadLinkDetails(string userId, CancellationToken cancellationToken = default(CancellationToken)) => GetUploadLinkDetailsAsync(userId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves the original own upload-link list using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Immutable complete metadata in server order and exact raw list JSON.</returns>
        /// <remarks>API 2.29 section 5.7.3 is creator-scoped and has no paging/sort parameters. Original server URLs/passwords, unknown values and future metadata survive without URL fallback or creator/permission inference. Missing arrays are invalid rather than assumed empty.</remarks>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<UploadLinkList> GetUploadLinkDetailsAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(URI_RESOURCE, Method.Get); request.AddOrUpdateHeader("Accept", "application/json"); var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, cancellationToken).ConfigureAwait(false), request, cancellationToken).ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK)); return LinkContractJson.Parse(response.Content, data => new UploadLinkList(data, response.Content)); }
    }
}
