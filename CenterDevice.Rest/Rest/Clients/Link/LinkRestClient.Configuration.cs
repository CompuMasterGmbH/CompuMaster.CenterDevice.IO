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
    public partial class LinkRestClient
    {
        /// <summary>Retrieves immutable complete metadata for one selected link.</summary>
        /// <inheritdoc cref="GetLinkDetailsAsync"/>
        public LinkDetails GetLinkDetails(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken)) => GetLinkDetailsAsync(userId, linkId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves complete selected link metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="linkId">The original selected link identity, escaped and correlated with the response.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Immutable original URLs, target identity, controls and nullable counters without reconstruction or width narrowing.</returns>
        /// <remarks>API 2.29 section 5.6.2 leaves role/visibility and external-owner restrictions to the server. Unknown metadata is not permission evidence. Older metadata/defaults remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The identity is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Metadata is malformed or identifies another link.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<LinkDetails> GetLinkDetailsAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(URI_RESOURCE + LinkContractJson.Selection(linkId), Method.Get); var response = await SendConfiguredLinkAsync(userId, request, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false); return LinkContractJson.Parse(response.Content, data => { var detail = new LinkDetails(data, response.Content); LinkContractJson.Correlate(linkId, detail.Id); return detail; }); }
        /// <summary>Replaces one selected link's complete access-control state.</summary>
        /// <inheritdoc cref="ReplaceAccessControlAsync"/>
        public void ReplaceAccessControl(string userId, string linkId, LinkAccessControlOptions controls, CancellationToken cancellationToken = default(CancellationToken)) => ReplaceAccessControlAsync(userId, linkId, controls, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Replaces complete access controls using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="linkId">The original selected link identity.</param>
        /// <param name="controls">Copied complete new controls. An explicit empty object removes previous optional choices according to server policy.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.6.3 replaces the whole access-control state, unlike partial upload-link updates. No current-state read/merge or target change occurs. Mandatory-control/password errors remain original server errors; uncertain writes are not replayed.</remarks>
        /// <exception cref="ArgumentNullException">The controls are null.</exception>
        /// <exception cref="ArgumentException">The identity or known control kinds are invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task ReplaceAccessControlAsync(string userId, string linkId, LinkAccessControlOptions controls, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); if (controls == null) throw new ArgumentNullException(nameof(controls)); var request = CreateRestRequest(URI_RESOURCE + LinkContractJson.Selection(linkId), Method.Put, "application/json"); var copied = LinkContractJson.AccessSnapshot(controls); request.AddStringBody(new JObject { ["access-control"] = copied }.ToString(Formatting.None), RestSharp.ContentType.Json); await SendConfiguredLinkAsync(userId, request, HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false); }
        /// <summary>Removes one explicitly selected link.</summary>
        /// <inheritdoc cref="RemoveLinkAsync"/>
        public void RemoveLink(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken)) => RemoveLinkAsync(userId, linkId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes one selected link using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="linkId">The original selected link identity, escaped as one path segment.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>API 2.29 section 5.6.4 enforces role/ownership/visibility on the server. No entity, all-selection, permission inference or new write replay is added; older DeleteLink methods remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The identity is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task RemoveLinkAsync(string userId, string linkId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); await SendConfiguredLinkAsync(userId, CreateRestRequest(URI_RESOURCE + LinkContractJson.Selection(linkId), Method.Delete), HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false); }
        private async Task<RestResponse> SendConfiguredLinkAsync(string userId, RestRequest request, HttpStatusCode expected, CancellationToken token)
        { request.AddOrUpdateHeader("Accept", "application/json"); var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(expected)); return response; }
    }
}
