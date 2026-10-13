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
    public partial class UploadLinkRestClient
    {
        /// <summary>Retrieves immutable complete metadata for one selected link.</summary>
        /// <inheritdoc cref="GetUploadLinkDetailsAsync"/>
        public UploadLinkDetails GetUploadLinkDetails(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken)) => GetUploadLinkDetailsAsync(userId, linkId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves complete selected link metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="linkId">The original selected link identity, escaped and correlated with the response.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Immutable original URLs, target identity, controls and nullable counters without reconstruction or width narrowing.</returns>
        /// <remarks>API 2.29 section 5.7.2 allows retrieval only by the upload-link creator. Unknown metadata is not permission evidence. Older metadata/defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The identity is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Metadata is malformed or identifies another link.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<UploadLinkDetails> GetUploadLinkDetailsAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(URI_RESOURCE + LinkContractJson.Selection(linkId), Method.Get); var response = await SendConfiguredUploadLinkAsync(userId, request, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false); return LinkContractJson.Parse(response.Content, data => { var detail = new UploadLinkDetails(data, response.Content); LinkContractJson.Correlate(linkId, detail.Id); return detail; }); }
        /// <summary>Updates only explicit choices of one selected upload link.</summary>
        /// <inheritdoc cref="UpdateConfiguredUploadLinkAsync"/>
        public void UpdateConfiguredUploadLink(string userId, string linkId, UploadLinkOptions options, CancellationToken cancellationToken = default(CancellationToken)) => UpdateConfiguredUploadLinkAsync(userId, linkId, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates explicit upload-link choices using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="linkId">The original selected upload-link identity.</param>
        /// <param name="options">Copied partial choices; omitted fields retain current values. An explicit new target replaces the previous target type.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.7.4 requires creator rights and visible targets. Collection/folder choices are exclusive; explicit password null or empty clears protection. No current-state read/merge, limit defaults, implicit clearing of omitted fields, URL distribution or uncertain write replay occurs.</remarks>
        /// <exception cref="ArgumentNullException">The options are null.</exception>
        /// <exception cref="ArgumentException">The identity or known choices are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task UpdateConfiguredUploadLinkAsync(string userId, string linkId, UploadLinkOptions options, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); if (options == null) throw new ArgumentNullException(nameof(options)); var request = CreateRestRequest(URI_RESOURCE + LinkContractJson.Selection(linkId), Method.Put, "application/json"); var copied = LinkContractJson.UploadSnapshot(options); request.AddStringBody(copied.ToString(Formatting.None), RestSharp.ContentType.Json); await SendConfiguredUploadLinkAsync(userId, request, HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false); }
        /// <summary>Removes one explicitly selected link.</summary>
        /// <inheritdoc cref="RemoveUploadLinkAsync"/>
        public void RemoveUploadLink(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken)) => RemoveUploadLinkAsync(userId, linkId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes one selected link using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="linkId">The original selected link identity, escaped as one path segment.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.7.5 permits removal only by the upload-link creator. No entity, all-selection, permission inference or new write replay is added; older DeleteLink methods remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The identity is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task RemoveUploadLinkAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); await SendConfiguredUploadLinkAsync(userId, CreateRestRequest(URI_RESOURCE + LinkContractJson.Selection(linkId), Method.Delete), HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false); }
        private async Task<RestResponse> SendConfiguredUploadLinkAsync(string userId, RestRequest request, HttpStatusCode expected, CancellationToken token)
        { request.AddOrUpdateHeader("Accept", "application/json"); var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(expected)); return response; }
    }
}
