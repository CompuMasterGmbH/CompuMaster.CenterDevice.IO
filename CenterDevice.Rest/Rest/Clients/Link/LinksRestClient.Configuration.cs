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
    public partial class LinksRestClient
    {
        /// <summary>Creates a link for one explicitly selected document with copied controls.</summary>
        /// <inheritdoc cref="CreateConfiguredDocumentLinkAsync"/>
        public LinkDetails CreateConfiguredDocumentLink(string userId, string id, LinkAccessControlOptions controls = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredDocumentLinkAsync(userId, id, controls, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an explicitly controlled document link using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The original selected document identifier.</param>
        /// <param name="controls">Copied optional complete controls, or null to omit them without defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed immutable identity and original server URLs.</returns>
        /// <remarks>API 2.29 section 5.6.1 enforces role/visibility/ownership and mandatory controls on the server. Existing-link errors remain errors. No guessed defaults, permission lookup, external URL distribution or uncertain creation replay occurs. Older methods/interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The explicit identity or known control kinds are invalid.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful creation metadata is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no link was created.</exception>
        public Task<LinkDetails> CreateConfiguredDocumentLinkAsync(string userId, string id, LinkAccessControlOptions controls = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredLinkDetailsAsync(userId, "document-id", id, controls, cancellationToken);
        /// <summary>Creates a link for one explicitly selected folder with copied controls.</summary>
        /// <inheritdoc cref="CreateConfiguredFolderLinkAsync"/>
        public LinkDetails CreateConfiguredFolderLink(string userId, string id, LinkAccessControlOptions controls = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredFolderLinkAsync(userId, id, controls, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an explicitly controlled folder link using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The original selected folder identifier.</param>
        /// <param name="controls">Copied optional complete controls, or null to omit them without defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed immutable identity and original server URLs.</returns>
        /// <remarks>API 2.29 section 5.6.1 enforces role/visibility/ownership and mandatory controls on the server. Existing-link errors remain errors. No guessed defaults, permission lookup, external URL distribution or uncertain creation replay occurs. Older methods/interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The explicit identity or known control kinds are invalid.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful creation metadata is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no link was created.</exception>
        public Task<LinkDetails> CreateConfiguredFolderLinkAsync(string userId, string id, LinkAccessControlOptions controls = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredLinkDetailsAsync(userId, "folder", id, controls, cancellationToken);
        /// <summary>Creates a link for one explicitly selected collection with copied controls.</summary>
        /// <inheritdoc cref="CreateConfiguredCollectionLinkAsync"/>
        public LinkDetails CreateConfiguredCollectionLink(string userId, string id, LinkAccessControlOptions controls = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredCollectionLinkAsync(userId, id, controls, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates an explicitly controlled collection link using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The original selected collection identifier.</param>
        /// <param name="controls">Copied optional complete controls, or null to omit them without defaults.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed immutable identity and original server URLs.</returns>
        /// <remarks>API 2.29 section 5.6.1 enforces role/visibility/ownership and mandatory controls on the server. Existing-link errors remain errors. No guessed defaults, permission lookup, external URL distribution or uncertain creation replay occurs. Older methods/interfaces remain unchanged.</remarks>
        /// <exception cref="ArgumentException">The explicit identity or known control kinds are invalid.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful creation metadata is malformed.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no link was created.</exception>
        public Task<LinkDetails> CreateConfiguredCollectionLinkAsync(string userId, string id, LinkAccessControlOptions controls = null, CancellationToken cancellationToken = default(CancellationToken))
            => CreateConfiguredLinkDetailsAsync(userId, "collection", id, controls, cancellationToken);
        private async Task<LinkDetails> CreateConfiguredLinkDetailsAsync(string userId, string field, string id, LinkAccessControlOptions controls, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); LinkContractJson.Selection(id); var copied = LinkContractJson.AccessSnapshot(controls); var body = new JObject { [field] = id }; if (copied != null) body["access-control"] = copied;
            var request = CreateRestRequest(URI_RESOURCE, Method.Post, "application/json"); request.AddOrUpdateHeader("Accept", "application/json"); request.AddStringBody(body.ToString(Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.Created)); return LinkContractJson.Parse(response.Content, data => new LinkDetails(data, response.Content, true));
        }
    }
}
