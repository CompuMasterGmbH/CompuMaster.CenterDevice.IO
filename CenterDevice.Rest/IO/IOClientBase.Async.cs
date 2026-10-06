using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public abstract partial class IOClientBase
    {
        private readonly SemaphoreSlim asynchronousPrincipalLookup = new SemaphoreSlim(1, 1);

        /// <summary>Retrieves and caches a group's name using asynchronous HTTP I/O.</summary>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The provider's group name, which may be empty for unnamed groups.</returns>
        public async Task<string> GroupNameAsync(string groupId, CancellationToken cancellationToken = default(CancellationToken))
        {
            await asynchronousPrincipalLookup.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (CachedKnownGroupNames.TryGetValue(groupId, out var name)) return name;
                name = (await ApiClient.Group.GetGroupAsync(CurrentAuthenticationContextUserID, groupId, cancellationToken).ConfigureAwait(false)).Name;
                CachedKnownGroupNames.TryAdd(groupId, name);
                return name;
            }
            finally { asynchronousPrincipalLookup.Release(); }
        }

        /// <summary>Retrieves and caches a user's SDK display name using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The SDK display name, which may be empty when the SDK does not map server name fields.</returns>
        public async Task<string> UserNameAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            await asynchronousPrincipalLookup.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (CachedKnownUserNames.TryGetValue(userId, out var name)) return name;
                var user = await ApiClient.User.GetUserDataAsync(CurrentAuthenticationContextUserID, userId, cancellationToken).ConfigureAwait(false);
                name = user.GetFullName();
                CachedKnownUserNames.TryAdd(userId, name);
                CachedKnownUserEMailAddresses.TryAdd(userId, user.Email);
                return name;
            }
            finally { asynchronousPrincipalLookup.Release(); }
        }

        /// <summary>Retrieves and caches a user's email address using asynchronous HTTP I/O.</summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The user's email address.</returns>
        public async Task<string> UserEMailAddressAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
        {
            await asynchronousPrincipalLookup.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (CachedKnownUserEMailAddresses.TryGetValue(userId, out var email)) return email;
                var user = await ApiClient.User.GetUserDataAsync(CurrentAuthenticationContextUserID, userId, cancellationToken).ConfigureAwait(false);
                email = user.Email;
                CachedKnownUserNames.TryAdd(userId, user.GetFullName());
                CachedKnownUserEMailAddresses.TryAdd(userId, email);
                return email;
            }
            finally { asynchronousPrincipalLookup.Release(); }
        }

        /// <summary>Retrieves the authenticated context user identifier using asynchronous HTTP I/O.</summary>
        /// <param name="cancellationToken">Cancels waiting, authorization, or active HTTP I/O.</param>
        /// <returns>The server's user identifier used for sharing and other context operations.</returns>
        public async Task<string> CurrentContextUserIdAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await asynchronousPrincipalLookup.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_CachedCurrentContextUserIdForAuthUserId == CurrentAuthenticationContextUserID) return _CachedCurrentContextUserIdResult;
                var user = await ApiClient.User.GetAuthenticatedUserDataAsync(CurrentAuthenticationContextUserID, cancellationToken).ConfigureAwait(false);
                _CachedCurrentContextUserIdResult = user.Id;
                _CachedCurrentContextUserIdForAuthUserId = CurrentAuthenticationContextUserID;
                return user.Id;
            }
            finally { asynchronousPrincipalLookup.Release(); }
        }

        /// <summary>Retrieves visible root collections through the native asynchronous REST client.</summary>
        /// <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        /// <returns>The visible collections with optional child-folder metadata.</returns>
        protected internal virtual async Task<List<Collection>> LookupCollectionsAsync(CancellationToken cancellationToken)
        {
            return (await ApiClient.Collections.GetCollectionsAsync(CurrentAuthenticationContextUserID, null, true, cancellationToken).ConfigureAwait(false))?.Collections;
        }

        /// <summary>Retrieves immediate child folders through the native asynchronous REST client.</summary>
        /// <param name="collectionId">The collection identifier, or null for a nested folder.</param>
        /// <param name="parentFolderId">The parent folder identifier, or the root-folder marker within a collection.</param>
        /// <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        /// <returns>The immediate child folders with their existing metadata fields.</returns>
        protected internal virtual async Task<List<Folder>> LookupChildFoldersAsync(string collectionId, string parentFolderId, CancellationToken cancellationToken)
        {
            return (await ApiClient.Folders.GetFoldersAsync(CurrentAuthenticationContextUserID, collectionId, parentFolderId, null, null,
                new[] { "collection", "id", "name", "parent", "users", "groups", "link" }, cancellationToken).ConfigureAwait(false)).Folders;
        }

        /// <summary>Retrieves immediate documents through the native asynchronous REST client.</summary>
        /// <param name="collectionId">The collection identifier, or null for a nested folder.</param>
        /// <param name="parentFolderId">The parent folder identifier, or the root-folder marker within a collection.</param>
        /// <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        /// <returns>The immediate document metadata.</returns>
        protected internal virtual async Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collectionId, string parentFolderId, CancellationToken cancellationToken)
        {
            return (await ApiClient.Documents.GetAsync<DocumentFullMetadata>(CurrentAuthenticationContextUserID, collectionId, null,
                parentFolderId, 0, int.MaxValue, cancellationToken).ConfigureAwait(false)).Documents;
        }

        /// <summary>Retrieves link details using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="linkId">The link identifier.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>The link details.</returns>
        public Task<Rest.Clients.Link.Link> GetLinkAsync(string linkId, CancellationToken cancellationToken = default(CancellationToken)) =>
            ApiClient.Link.GetLinkAsync(CurrentAuthenticationContextUserID, linkId, cancellationToken);

        /// <summary>Retrieves upload-link details using cancellable asynchronous HTTP I/O.</summary>
        /// <param name="linkId">The upload-link identifier.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>The upload-link details.</returns>
        public Task<Rest.Clients.Link.UploadLink> GetUploadLinkAsync(string linkId, CancellationToken cancellationToken = default(CancellationToken)) =>
            ApiClient.UploadLink.GetLinkAsync(CurrentAuthenticationContextUserID, linkId, cancellationToken);
    }
}
