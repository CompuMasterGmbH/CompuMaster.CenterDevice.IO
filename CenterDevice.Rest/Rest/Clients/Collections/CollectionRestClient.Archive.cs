using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Sets an explicit archive expiry rule and action.</summary>
        /// <inheritdoc cref="SetArchiveExpiryAsync"/>
        public void SetArchiveExpiry(string userId, string collectionId, CollectionArchiveExpiryRequest expiry, CancellationToken cancellationToken = default(CancellationToken))
            => SetArchiveExpiryAsync(userId, collectionId, expiry, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Sets an explicit archive expiry rule using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected archived collection identifier.</param>
        /// <param name="expiry">The copied explicit date or positive period and caller-selected expiry action.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server requires ownership, an internal/admin role and archiving permission. It computes expiry in Europe/Berlin; no date/calendar conversion occurs locally. Delete permanently deletes expired documents. Empty/null requests do not implicitly remove expiry; use RemoveArchiveExpiry explicitly. Frozen configuration and unarchiving permissions remain server-authoritative; no grant, current rule or default is inferred.</remarks>
        /// <exception cref="ArgumentNullException">The expiry request is null.</exception>
        /// <exception cref="ArgumentException">The collection or complete expiry selection is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no rule changed.</exception>
        public Task SetArchiveExpiryAsync(string userId, string collectionId, CollectionArchiveExpiryRequest expiry, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); if (expiry == null) throw new ArgumentNullException(nameof(expiry)); return SendCollectionActionAsync(userId, collectionId, "set-expiry", expiry.SnapshotForSend(), cancellationToken); }
        /// <summary>Removes an explicitly selected archive's expiry rule and action.</summary>
        /// <inheritdoc cref="RemoveArchiveExpiryAsync"/>
        public void RemoveArchiveExpiry(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveArchiveExpiryAsync(userId, collectionId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes archive expiry using an explicit empty parameter object.</summary>
        /// <inheritdoc cref="ArchiveCollectionAsync"/>
        /// <remarks>The server unsets date/period and action. Ownership, internal/admin role, archiving and frozen/unarchiving restrictions apply. No archive content is deleted and no permissions or existing state are changed locally.</remarks>
        public Task RemoveArchiveExpiryAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => SendCollectionActionAsync(userId, collectionId, "set-expiry", new JObject(), cancellationToken);

        /// <summary>Sets an explicit extended-metadata source for archived document dates.</summary>
        /// <inheritdoc cref="SetArchivedDateSourceAsync"/>
        public void SetArchivedDateSource(string userId, string collectionId, string key, CancellationToken cancellationToken = default(CancellationToken))
            => SetArchivedDateSourceAsync(userId, collectionId, key, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Sets an explicit archived-date source using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected archived collection identifier.</param>
        /// <param name="key">The original namespace.section.field-name key; the reference supports user.dates fields.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server validates date-field support and archiving/owner/frozen permissions. When an added document lacks the selected date field, the server uses its current date/time. The client does not fetch metadata or infer a date, normalize keys or alter unarchiving grants. Use RemoveArchivedDateSource explicitly to unset it.</remarks>
        /// <exception cref="ArgumentException">The collection or source key is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested and cannot establish that no source changed.</exception>
        public Task SetArchivedDateSourceAsync(string userId, string collectionId, string key, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("An explicit archived-date source key is required.", nameof(key)); return SendCollectionActionAsync(userId, collectionId, "set-archived-date-source", new JObject { ["key"] = key }, cancellationToken); }
        /// <summary>Removes the explicitly selected archive's custom date source.</summary>
        /// <inheritdoc cref="RemoveArchivedDateSourceAsync"/>
        public void RemoveArchivedDateSource(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => RemoveArchivedDateSourceAsync(userId, collectionId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Removes the custom archive date source using explicit empty parameters.</summary>
        /// <inheritdoc cref="ArchiveCollectionAsync"/>
        /// <remarks>The server unsets the custom date source. Ownership, internal/admin role, archiving and frozen/unarchiving restrictions apply. No metadata, document dates or permission grants are inferred or changed locally.</remarks>
        public Task RemoveArchivedDateSourceAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => SendCollectionActionAsync(userId, collectionId, "set-archived-date-source", new JObject(), cancellationToken);

        /// <summary>Sets the explicitly selected archive's configuration freeze state.</summary>
        /// <inheritdoc cref="SetArchiveFrozenAsync"/>
        public void SetArchiveFrozen(string userId, string collectionId, bool freeze, CancellationToken cancellationToken = default(CancellationToken))
            => SetArchiveFrozenAsync(userId, collectionId, freeze, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Sets archive configuration freezing using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected archived collection identifier.</param>
        /// <param name="freeze">The explicit freeze choice; false requires the owner to be in unarchiving mode.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server enforces ownership, internal/admin role and archiving permission. Once frozen, subsequent changes by callers outside unarchiving mode are rejected, including another freeze request. The client does not enable unarchiving or change grants and does not automatically replay this state-dependent operation. Cancellation cannot establish that the configuration remained changeable.</remarks>
        /// <exception cref="ArgumentException">The selected collection is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task SetArchiveFrozenAsync(string userId, string collectionId, bool freeze, CancellationToken cancellationToken = default(CancellationToken))
            => SendCollectionActionAsync(userId, collectionId, "freeze-archive", new JObject { ["freeze"] = freeze }, cancellationToken);
    }
}
