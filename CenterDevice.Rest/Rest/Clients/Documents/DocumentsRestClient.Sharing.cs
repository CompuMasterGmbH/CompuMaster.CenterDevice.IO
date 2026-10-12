using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentsRestClient
    {
        /// <summary>Shares selected documents.</summary>
        /// <inheritdoc cref="ShareAsync"/>
        public DocumentSharingResult Share(string userId, IEnumerable<string> documentIds, DocumentSharingRecipients recipients, string comment = null, CancellationToken cancellationToken = default(CancellationToken))
            => ShareAsync(userId, documentIds, recipients, comment, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Shares selected documents using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The nonempty selected document IDs, snapshotted before authorization.</param>
        /// <param name="recipients">The explicit nonempty recipient selection, snapshotted before authorization.</param>
        /// <param name="comment">The optional notification comment; null is omitted and empty text is preserved.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Invitations and exact recipient/document failures; HTTP 204 reports complete success.</returns>
        /// <remarks>Admins, internal and external users may operate on visible documents. Sharing sends notifications and may invite unknown e-mail addresses into the tenant. Roles and state are enforced by the server. Partial failures and uncertain writes are not replayed automatically. Cancellation does not prove that no server changes occurred.</remarks>
        /// <exception cref="System.ArgumentException">The selected document or recipient values are invalid.</exception>
        /// <exception cref="System.OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentSharingResult> ShareAsync(string userId, IEnumerable<string> documentIds, DocumentSharingRecipients recipients, string comment = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = SnapshotDocumentIds(documentIds, nameof(documentIds));
            var resource = URI_RESOURCE;
            var parameters = SnapshotSharingRecipients(recipients);
            parameters["documents"] = new Newtonsoft.Json.Linq.JArray(selected);
            if (comment != null) parameters["comment"] = comment;
            return ExecuteDocumentSharingAsync(userId, resource, true, parameters, selected, cancellationToken);
        }

        /// <summary>Removes selected direct sharings from selected documents.</summary>
        /// <inheritdoc cref="UnshareAsync"/>
        public DocumentSharingResult Unshare(string userId, IEnumerable<string> documentIds, DocumentSharingRecipients recipients, CancellationToken cancellationToken = default(CancellationToken))
            => UnshareAsync(userId, documentIds, recipients, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes selected direct sharings from selected documents using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The nonempty selected document IDs, snapshotted before authorization.</param>
        /// <param name="recipients">The explicit nonempty recipient selection, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Invitations and exact recipient/document failures; HTTP 204 reports complete success.</returns>
        /// <remarks>Admins, internal and external users may operate on visible documents. Unsharing never invites unknown e-mail addresses and silently retains owner access; inherited access may remain. Roles and state are enforced by the server. Partial failures and uncertain writes are not replayed automatically. Cancellation does not prove that no server changes occurred.</remarks>
        /// <exception cref="System.ArgumentException">The selected document or recipient values are invalid.</exception>
        /// <exception cref="System.OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentSharingResult> UnshareAsync(string userId, IEnumerable<string> documentIds, DocumentSharingRecipients recipients, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = SnapshotDocumentIds(documentIds, nameof(documentIds));
            var resource = URI_RESOURCE;
            var parameters = SnapshotSharingRecipients(recipients);
            parameters["documents"] = new Newtonsoft.Json.Linq.JArray(selected);
            return ExecuteDocumentSharingAsync(userId, resource, false, parameters, selected, cancellationToken);
        }

        /// <summary>Removes all removable direct sharings from selected documents.</summary>
        /// <inheritdoc cref="UnshareAllAsync"/>
        public DocumentSharingResult UnshareAll(string userId, IEnumerable<string> documentIds, CancellationToken cancellationToken = default(CancellationToken))
            => UnshareAllAsync(userId, documentIds, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes all removable direct sharings from selected documents using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="documentIds">The nonempty selected document IDs, snapshotted before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Invitations and exact recipient/document failures; HTTP 204 reports complete success.</returns>
        /// <remarks>Admins, internal and external users may operate on visible documents. Unsharing never invites unknown e-mail addresses and silently retains owner access; inherited access may remain. This explicitly omits all recipient lists to remove all removable direct sharings. Roles and state are enforced by the server. Partial failures and uncertain writes are not replayed automatically. Cancellation does not prove that no server changes occurred.</remarks>
        /// <exception cref="System.ArgumentException">The selected document or recipient values are invalid.</exception>
        /// <exception cref="System.OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentSharingResult> UnshareAllAsync(string userId, IEnumerable<string> documentIds, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = SnapshotDocumentIds(documentIds, nameof(documentIds));
            var resource = URI_RESOURCE;
            var parameters = new Newtonsoft.Json.Linq.JObject();
            parameters["documents"] = new Newtonsoft.Json.Linq.JArray(selected);
            return ExecuteDocumentSharingAsync(userId, resource, false, parameters, selected, cancellationToken);
        }

    }
}
