using CenterDevice.Rest.ResponseHandler;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Collections
{
    public partial class CollectionRestClient
    {
        /// <summary>Updates explicitly selected collection custom metadata.</summary>
        /// <inheritdoc cref="UpdateExtendedMetadataAsync"/>
        public void UpdateExtendedMetadata(string userId, string collectionId, CollectionMetadataUpdateRequest update, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateExtendedMetadataAsync(userId, collectionId, update, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates selected collection custom metadata using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="update">The original copied string/string-array set values and unset names; unspecified fields remain unchanged.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server permits updates to visible collections and silently ignores unset keys that do not exist. Both set and unset remain explicit; no precedence, permission or existing-value inference is performed. No speculative replay is added; cancellation cannot establish that no metadata changed.</remarks>
        /// <exception cref="ArgumentNullException">The update is null.</exception>
        /// <exception cref="ArgumentException">The selected collection or known update structure is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task UpdateExtendedMetadataAsync(string userId, string collectionId, CollectionMetadataUpdateRequest update, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); if (update == null) throw new ArgumentNullException(nameof(update)); return SendCollectionActionAsync(userId, collectionId, "update-extended-metadata", update.SnapshotForSend(), cancellationToken); }

        /// <summary>Enables irreversible auditing for an explicitly selected collection.</summary>
        /// <inheritdoc cref="EnableAuditingAsync"/>
        public void EnableAuditing(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => EnableAuditingAsync(userId, collectionId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Enables collection auditing using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>Auditing cannot be disabled afterward. The server requires ownership, a non-public collection without shared folders, tenant feature availability and auditing permission. Notification settings start disabled. No state or permission is inferred and no speculative replay is added; cancellation cannot establish that auditing remained disabled.</remarks>
        /// <exception cref="ArgumentException">The selected collection is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task EnableAuditingAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => SendCollectionActionAsync(userId, collectionId, "enable-auditing", null, cancellationToken);

        /// <summary>Opts the caller into an explicitly selected audited collection.</summary>
        /// <inheritdoc cref="OptInAsync"/>
        public void OptIn(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => OptInAsync(userId, collectionId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Opts the caller into an audited collection using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="EnableAuditingAsync"/>
        /// <remarks>The server requires an audited collection shared with the caller. Opting in cannot subsequently be undone by opting out or resetting the opted-in state. No participant, audit state or permission is inferred; cancellation cannot establish that no opt-in occurred.</remarks>
        public Task OptInAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => SendCollectionActionAsync(userId, collectionId, "opt-in", null, cancellationToken);
        /// <summary>Opts the caller out of an explicitly selected audited collection.</summary>
        /// <inheritdoc cref="OptOutAsync"/>
        public void OptOut(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => OptOutAsync(userId, collectionId, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Opts the caller out of an audited collection using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="EnableAuditingAsync"/>
        /// <remarks>The server permits opting out only before the caller has opted in and requires the audited collection to be shared with the caller. No prior-state lookup, reset or speculative replay is added.</remarks>
        public Task OptOutAsync(string userId, string collectionId, CancellationToken cancellationToken = default(CancellationToken))
            => SendCollectionActionAsync(userId, collectionId, "opt-out", null, cancellationToken);

        /// <summary>Resets explicitly selected opted-out collection audit states.</summary>
        /// <inheritdoc cref="ResetAuditingAsync"/>
        public void ResetAuditing(string userId, string collectionId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
            => ResetAuditingAsync(userId, collectionId, users, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Resets selected opted-out audit states using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="users">The nonempty explicit user selection copied before authorization.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server requires collection ownership and permits resetting opted-out states only. Opted-in states cannot be reset. No existing state is fetched or inferred; no speculative replay is added.</remarks>
        /// <exception cref="ArgumentNullException">The user selection is null.</exception>
        /// <exception cref="ArgumentException">The selected collection or user selection is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task ResetAuditingAsync(string userId, string collectionId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendCollectionActionAsync(userId, collectionId, "reset-auditing", CollectionAuditUsers(users), cancellationToken); }
        /// <summary>Resends auditing emails to explicitly selected pending users.</summary>
        /// <inheritdoc cref="ResendAuditingEmailAsync"/>
        public void ResendAuditingEmail(string userId, string collectionId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
            => ResendAuditingEmailAsync(userId, collectionId, users, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Resends auditing emails to selected pending users using asynchronous HTTP I/O.</summary>
        /// <inheritdoc cref="ResetAuditingAsync"/>
        /// <remarks>The server requires ownership and sends email only to selected users who have not responded. Each invocation can send another email; no automatic follow-up or speculative replay is added. Cancellation cannot establish that no email was sent.</remarks>
        public Task ResendAuditingEmailAsync(string userId, string collectionId, IEnumerable<string> users, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); return SendCollectionActionAsync(userId, collectionId, "resend-auditing-email", CollectionAuditUsers(users), cancellationToken); }

        /// <summary>Updates explicit owner auditing notification settings.</summary>
        /// <inheritdoc cref="UpdateAuditingEmailSettingsAsync"/>
        public void UpdateAuditingEmailSettings(string userId, string collectionId, CollectionAuditingEmailSettings settings, CancellationToken cancellationToken = default(CancellationToken))
            => UpdateAuditingEmailSettingsAsync(userId, collectionId, settings, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        /// <summary>Updates owner auditing notification settings using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The authentication-context user identifier.</param>
        /// <param name="collectionId">The explicitly selected collection identifier.</param>
        /// <param name="settings">The copied explicit opted-in/out notifications and pending-response period, retaining omitted values.</param>
        /// <param name="cancellationToken">Cancels authorization, admission and HTTP I/O.</param>
        /// <returns>A task completing after HTTP 204 confirmation.</returns>
        /// <remarks>The server requires ownership of an audited collection. No defaults, current-setting lookups or speculative replay are added.</remarks>
        /// <exception cref="ArgumentNullException">The settings are null.</exception>
        /// <exception cref="ArgumentException">The collection or known setting structure is unusable.</exception>
        /// <exception cref="OperationCanceledException">Cancellation is requested.</exception>
        public Task UpdateAuditingEmailSettingsAsync(string userId, string collectionId, CollectionAuditingEmailSettings settings, CancellationToken cancellationToken = default(CancellationToken))
        { cancellationToken.ThrowIfCancellationRequested(); if (settings == null) throw new ArgumentNullException(nameof(settings)); return SendCollectionActionAsync(userId, collectionId, "update-auditing-email-settings", settings.SnapshotForSend(), cancellationToken); }

        private static JObject CollectionAuditUsers(IEnumerable<string> users)
        {
            if (users == null) throw new ArgumentNullException(nameof(users)); var selected = users.ToArray();
            if (selected.Length == 0 || selected.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Explicit nonblank audit user identifiers are required.", nameof(users));
            return new JObject { ["users"] = new JArray(selected) };
        }
        private async Task SendCollectionActionAsync(string userId, string collectionId, string action, JObject parameters, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(collectionId) || collectionId == "." || collectionId == "..") throw new ArgumentException("A selected collection identifier is required.", nameof(collectionId));
            var body = new JObject { ["action"] = action }; if (parameters != null) body["params"] = parameters.DeepClone();
            var request = CreateRestRequest(ApiVersionPrefix + "collection/" + Uri.EscapeDataString(collectionId), Method.Post, "application/json");
            request.AddStringBody(body.ToString(Newtonsoft.Json.Formatting.None), RestSharp.ContentType.Json);
            var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token).ConfigureAwait(false), request, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
        }
    }
}
