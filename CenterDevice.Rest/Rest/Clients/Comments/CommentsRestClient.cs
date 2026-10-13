using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Comments
{
    /// <summary>Provides explicit comment workflows for selected visible documents.</summary>
    /// <remarks>API 2.29 sections 5.5.1–5.5.5 enforce document visibility, creator-only edit/removal and guest latest-version restrictions on the server. Creation is non-idempotent; uncertain writes are not replayed. Original comment text is not trimmed, escaped as HTML or sanitized by the SDK.</remarks>
    public class CommentsRestClient : CenterDeviceRestClient
    {
        /// <summary>Initializes a comment client with the existing authorization and transport configuration.</summary>
        /// <param name="oauthInfoProvider">The existing authorization provider.</param>
        /// <param name="configuration">The selected REST origin and user agent.</param>
        /// <param name="errorHandler">The existing error/expired-token handler, or null.</param>
        /// <param name="apiVersionPrefix">The existing API resource prefix.</param>
        public CommentsRestClient(IOAuthInfoProvider oauthInfoProvider, IRestClientConfiguration configuration, IRestClientErrorHandler errorHandler, string apiVersionPrefix)
            : base(oauthInfoProvider, configuration, errorHandler, apiVersionPrefix) { }
        /// <summary>Creates a comment for one selected visible document.</summary>
        /// <inheritdoc cref="AddCommentAsync"/>
        public DocumentComment AddComment(string userId, string documentId, string text, CancellationToken cancellationToken = default(CancellationToken))
            => AddCommentAsync(userId, documentId, text, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Creates one explicit comment using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The original selected document identifier, escaped as one path segment.</param>
        /// <param name="text">The nonnull original text, including whitespace, empty text and permitted HTML formatting for server validation.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The confirmed new comment identifier with no inferred author/dates/version.</returns>
        /// <exception cref="ArgumentException">The document selection is unusable.</exception>
        /// <exception cref="ArgumentNullException">The text is null.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">The successful response has invalid metadata.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested; a sent write may already have been applied.</exception>
        public async Task<DocumentComment> AddCommentAsync(string userId, string documentId, string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(CollectionPath(documentId), Method.Post, "application/json"); AddText(request, text);
            var response = await Send(userId, request, HttpStatusCode.Created, cancellationToken).ConfigureAwait(false); return CommentJson.Parse(response.Content, data => new DocumentComment(data, false));
        }
        /// <summary>Replaces the text of one explicitly selected comment.</summary>
        /// <inheritdoc cref="UpdateCommentAsync"/>
        public void UpdateComment(string userId, string documentId, string commentId, string text, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateCommentAsync(userId, documentId, commentId, text, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Replaces explicit comment text using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The original selected document identifier.</param>
        /// <param name="commentId">The original selected comment identifier; it is not inferred from list order.</param>
        /// <param name="text">The nonnull original replacement text, sent unchanged.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server enforces creator rights and document visibility. No comment merge, formatting normalization, permission inference or uncertain write replay occurs.</remarks>
        /// <exception cref="ArgumentException">An explicit selection is unusable.</exception>
        /// <exception cref="ArgumentNullException">The text is null.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task UpdateCommentAsync(string userId, string documentId, string commentId, string text, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var request = CreateRestRequest(SelectedPath(documentId, commentId), Method.Put, "application/json"); AddText(request, text); await Send(userId, request, HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false); }
        /// <summary>Removes one explicitly selected comment.</summary>
        /// <inheritdoc cref="RemoveCommentAsync"/>
        public void RemoveComment(string userId, string documentId, string commentId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveCommentAsync(userId, documentId, commentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes one selected comment using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The original selected document identifier.</param>
        /// <param name="commentId">The original selected comment identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>Creator rights and repeated-removal errors remain server-authoritative. The request has no entity; it never removes all comments implicitly.</remarks>
        /// <exception cref="ArgumentException">An explicit selection is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task RemoveCommentAsync(string userId, string documentId, string commentId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); await Send(userId, CreateRestRequest(SelectedPath(documentId, commentId), Method.Delete), HttpStatusCode.NoContent, cancellationToken).ConfigureAwait(false); }
        /// <summary>Retrieves one explicitly selected comment's complete metadata.</summary>
        /// <inheritdoc cref="GetCommentAsync"/>
        public DocumentComment GetComment(string userId, string documentId, string commentId, CancellationToken cancellationToken = default(CancellationToken))
            => GetCommentAsync(userId, documentId, commentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves selected comment metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The original selected document identifier.</param>
        /// <param name="commentId">The original selected comment identifier, correlated with the confirmed response.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>Immutable original author/date/text/future metadata.</returns>
        /// <remarks>Guest visibility is limited to the latest version's comments. No version association is inferred from fields the response does not contain.</remarks>
        /// <exception cref="ArgumentException">An explicit selection is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">Successful metadata is malformed or identifies another comment.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentComment> GetCommentAsync(string userId, string documentId, string commentId, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested(); var selected = commentId; var request = CreateRestRequest(SelectedPath(documentId, selected), Method.Get);
            var response = await Send(userId, request, HttpStatusCode.OK, cancellationToken).ConfigureAwait(false);
            return CommentJson.Parse(response.Content, data => { var comment = new DocumentComment(data, true); if (CommentJson.Identity(comment.Id) != CommentJson.Identity(selected)) throw new System.IO.InvalidDataException("The response identifies another comment."); return comment; });
        }
        /// <summary>Retrieves all visible comments for one selected document.</summary>
        /// <inheritdoc cref="GetCommentsAsync"/>
        public DocumentCommentList GetComments(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
            => GetCommentsAsync(userId, documentId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Retrieves the original visible comment list using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentId">The original selected visible document identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>The immutable list in original server order and exact raw response JSON.</returns>
        /// <remarks>The manufacturer exposes no list paging/sort options. A missing array is invalid rather than assumed empty. Guest latest-version restrictions remain server-enforced.</remarks>
        /// <exception cref="ArgumentException">The document selection is unusable.</exception>
        /// <exception cref="CenterDevice.Rest.Exceptions.InvalidResponseDataException">The successful list has invalid metadata.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public async Task<DocumentCommentList> GetCommentsAsync(string userId, string documentId, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); var response = await Send(userId, CreateRestRequest(CollectionPath(documentId), Method.Get), HttpStatusCode.OK, cancellationToken).ConfigureAwait(false); return CommentJson.Parse(response.Content, data => new DocumentCommentList(data, response.Content)); }
        private async Task<RestResponse> Send(string userId, RestRequest request, HttpStatusCode expected, CancellationToken token)
        { request.AddOrUpdateHeader("Accept", "application/json"); var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false); token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(expected)); return response; }
        private static void AddText(RestRequest request, string text) { if (text == null) throw new ArgumentNullException(nameof(text)); request.AddStringBody(new JObject { ["text"] = text }.ToString(Formatting.None), RestSharp.ContentType.Json); }
        private string CollectionPath(string documentId) => ApiVersionPrefix + "document/" + Selection(documentId) + "/comments";
        private string SelectedPath(string documentId, string commentId) => ApiVersionPrefix + "document/" + Selection(documentId) + "/comment/" + Selection(commentId);
        private static string Selection(string value) { if (string.IsNullOrWhiteSpace(value) || value == "." || value == "..") throw new ArgumentException("An original explicit document/comment identifier is required."); return Uri.EscapeDataString(value); }
    }
}
