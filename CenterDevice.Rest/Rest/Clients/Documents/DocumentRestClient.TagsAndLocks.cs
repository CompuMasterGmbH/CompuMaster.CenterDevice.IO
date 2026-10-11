using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    public partial class DocumentRestClient
    {
        /// <summary>Adds tags to the selected document.</summary>
        /// <inheritdoc cref="AddTagsAsync"/>
        public DocumentCommandResult AddTags(string userId, string id, IEnumerable<string> tags, CancellationToken cancellationToken = default(CancellationToken))
            => AddTagsAsync(userId, id, tags, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Adds tags to the selected document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="tags">The nonempty tag sequence, preserving spelling and whitespace for server-side normalization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or the exact server-reported failed document IDs.</returns>
        /// <remarks>Admin, internal or external roles and visible documents are required. The server trims and ignores empty tags and compares existing tags case-insensitively. HTTP 200 can carry partial failures; HTTP 204 indicates complete success. No uncertain write or partial failure is automatically retried.</remarks>
        /// <exception cref="ArgumentNullException">A required sequence is null.</exception>
        /// <exception cref="ArgumentException">The document selection or value sequence is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentCommandResult> AddTagsAsync(string userId, string id, IEnumerable<string> tags,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (id == "." || id == "..") throw new ArgumentException("A document ID must not be a relative path segment.", nameof(id));
            var selected = new[] { id };
            var values = SnapshotDocumentCommandValues(tags, nameof(tags), false);
            return ExecuteDocumentListCommandAsync(userId, URI_RESOURCE + Uri.EscapeDataString(id), "add-tag",
                new { tags = values }, selected, cancellationToken);
        }

        /// <summary>Removes tags from the selected document.</summary>
        /// <inheritdoc cref="RemoveTagsAsync"/>
        public DocumentCommandResult RemoveTags(string userId, string id, IEnumerable<string> tags, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveTagsAsync(userId, id, tags, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes tags from the selected document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="tags">The nonempty tag sequence, preserving spelling and whitespace for server-side normalization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or the exact server-reported failed document IDs.</returns>
        /// <remarks>Admin, internal or external roles and visible documents are required. The server trims and ignores empty tags and compares existing tags case-insensitively. HTTP 200 can carry partial failures; HTTP 204 indicates complete success. No uncertain write or partial failure is automatically retried.</remarks>
        /// <exception cref="ArgumentNullException">A required sequence is null.</exception>
        /// <exception cref="ArgumentException">The document selection or value sequence is invalid.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentCommandResult> RemoveTagsAsync(string userId, string id, IEnumerable<string> tags,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (id == "." || id == "..") throw new ArgumentException("A document ID must not be a relative path segment.", nameof(id));
            var selected = new[] { id };
            var values = SnapshotDocumentCommandValues(tags, nameof(tags), false);
            return ExecuteDocumentListCommandAsync(userId, URI_RESOURCE + Uri.EscapeDataString(id), "remove-tag",
                new { tags = values }, selected, cancellationToken);
        }

        /// <summary>Adds locks to the selected document.</summary>
        /// <inheritdoc cref="AddLocksAsync"/>
        public DocumentCommandResult AddLocks(string userId, string id, IEnumerable<string> locks, CancellationToken cancellationToken = default(CancellationToken))
            => AddLocksAsync(userId, id, locks, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Adds locks to the selected document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="locks">The nonempty supported lock names; API 2.29 supports create-new-version only.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or the exact server-reported failed document IDs.</returns>
        /// <remarks>Admin, internal or external roles and a visible document are required; another user's existing lock prevents locking. HTTP 200 can carry partial failures; HTTP 204 indicates complete success. No uncertain write or partial failure is automatically retried. Existing AddLock/RemoveLock overloads and their defaults are unchanged.</remarks>
        /// <exception cref="ArgumentNullException">A required sequence is null.</exception>
        /// <exception cref="ArgumentException">The document selection or value sequence is invalid.</exception>
        /// <exception cref="NotSupportedException">A requested lock is unsupported by API 2.29.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentCommandResult> AddLocksAsync(string userId, string id, IEnumerable<string> locks,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (id == "." || id == "..") throw new ArgumentException("A document ID must not be a relative path segment.", nameof(id));
            var selected = new[] { id };
            var values = SnapshotDocumentCommandValues(locks, nameof(locks), true);
            return ExecuteDocumentListCommandAsync(userId, URI_RESOURCE + Uri.EscapeDataString(id), "add-lock",
                new { locks = values }, selected, cancellationToken);
        }

        /// <summary>Removes locks from the selected document.</summary>
        /// <inheritdoc cref="RemoveLocksAsync"/>
        public DocumentCommandResult RemoveLocks(string userId, string id, IEnumerable<string> locks, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveLocksAsync(userId, id, locks, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>Removes locks from the selected document using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="id">The selected document identifier.</param>
        /// <param name="locks">The nonempty supported lock names; API 2.29 supports create-new-version only.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and active HTTP I/O.</param>
        /// <returns>Complete success or the exact server-reported failed document IDs.</returns>
        /// <remarks>The lock owner or document owner can unlock; admins can unlock any visible document. HTTP 200 can carry partial failures; HTTP 204 indicates complete success. No uncertain write or partial failure is automatically retried. Existing AddLock/RemoveLock overloads and their defaults are unchanged.</remarks>
        /// <exception cref="ArgumentNullException">A required sequence is null.</exception>
        /// <exception cref="ArgumentException">The document selection or value sequence is invalid.</exception>
        /// <exception cref="NotSupportedException">A requested lock is unsupported by API 2.29.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task<DocumentCommandResult> RemoveLocksAsync(string userId, string id, IEnumerable<string> locks,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVersionDocumentId(id);
            if (id == "." || id == "..") throw new ArgumentException("A document ID must not be a relative path segment.", nameof(id));
            var selected = new[] { id };
            var values = SnapshotDocumentCommandValues(locks, nameof(locks), true);
            return ExecuteDocumentListCommandAsync(userId, URI_RESOURCE + Uri.EscapeDataString(id), "remove-lock",
                new { locks = values }, selected, cancellationToken);
        }

    }
}

