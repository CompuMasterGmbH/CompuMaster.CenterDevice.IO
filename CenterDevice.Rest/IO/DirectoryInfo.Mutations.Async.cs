using System;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public partial class DirectoryInfo
    {
        /// <summary>Creates an immediate child collection or folder using asynchronous HTTP I/O.</summary>
        /// <param name="directoryName">A child name without configured path separators.</param>
        /// <param name="cancellationToken">Cancels admission or active HTTP I/O.</param>
        /// <returns>A task representing creation and cache invalidation.</returns>
        public Task CreateDirectoryAsync(string directoryName, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (directoryName == null) throw new ArgumentNullException(nameof(directoryName));
            if (directoryName.Contains(ioClient.Paths.DirectorySeparatorChar.ToString()) || directoryName.Contains(ioClient.Paths.AltDirectorySeparatorChar.ToString()))
                throw new ArgumentException("A directory name must not contain a configured path separator.", nameof(directoryName));
            return CreateDirectoryAsync(directoryName, IsRootDirectory ? DirectoryType.Collection : DirectoryType.Folder, cancellationToken);
        }

        /// <summary>Creates a child using an explicit directory kind and asynchronous HTTP I/O.</summary>
        /// <param name="directoryName">The child name, following the existing explicit-kind overload's server-side validation contract.</param>
        /// <param name="style">The existing collection or folder kind.</param>
        /// <param name="cancellationToken">Cancels admission or active HTTP I/O.</param>
        /// <returns>A task representing creation and cache invalidation, including uncertain failures.</returns>
        public async Task CreateDirectoryAsync(string directoryName, DirectoryType style, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { await CreateDirectoryCoreAsync(directoryName, style, cancellationToken).ConfigureAwait(false); }
            finally { ResetDirectoriesCache(); }
        }

        /// <summary>Creates the requested child through the native asynchronous REST client.</summary>
        /// <param name="directoryName">The child name.</param>
        /// <param name="style">The collection or folder kind.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the remote mutation before the outer method invalidates caches.</returns>
        protected virtual async Task CreateDirectoryCoreAsync(string directoryName, DirectoryType style, CancellationToken cancellationToken)
        {
            if (style == DirectoryType.Collection)
                await ioClient.ApiClient.Collections.CreateCollectionAsync(ioClient.CurrentAuthenticationContextUserID, directoryName, cancellationToken).ConfigureAwait(false);
            else
                await ioClient.ApiClient.Folders.CreateFolderAsync(ioClient.CurrentAuthenticationContextUserID, directoryName, restCollection?.Id, restFolder?.Id, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes this collection or folder using asynchronous HTTP I/O.</summary>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing deletion and parent-cache invalidation.</returns>
        /// <exception cref="NotSupportedException">This directory is the root.</exception>
        public async Task DeleteAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory) throw new NotSupportedException("Removing root directory is not supported");
            try { await DeleteCoreAsync(cancellationToken).ConfigureAwait(false); }
            finally { ParentDirectory?.ResetDirectoriesCache(); }
        }

        /// <summary>Deletes this directory through the native asynchronous REST client.</summary>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the remote mutation before parent-cache invalidation.</returns>
        protected virtual Task DeleteCoreAsync(CancellationToken cancellationToken) => restCollection != null
            ? ioClient.ApiClient.Collection.DeleteCollectionAsync(ioClient.CurrentAuthenticationContextUserID, restCollection.Id, cancellationToken)
            : ioClient.ApiClient.Folder.DeleteFolderAsync(ioClient.CurrentAuthenticationContextUserID, restFolder.Id, cancellationToken);

        /// <summary>Renames this collection or folder using asynchronous HTTP I/O.</summary>
        /// <param name="targetName">The replacement name.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the rename and local metadata/cache update.</returns>
        /// <remarks>Local names change only after success. Uncertain failures invalidate the parent cache for reconciliation.</remarks>
        /// <exception cref="NotSupportedException">This directory is the root.</exception>
        public async Task RenameAsync(string targetName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory) throw new NotSupportedException("Renaming root directory is not supported");
            try { await RenameCoreAsync(targetName, cancellationToken).ConfigureAwait(false); }
            finally { ParentDirectory?.ResetDirectoriesCache(); }
            name = targetName;
        }

        /// <summary>Renames this directory through the native asynchronous REST client.</summary>
        /// <param name="targetName">The replacement name.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the remote mutation before local metadata/cache updates.</returns>
        protected virtual Task RenameCoreAsync(string targetName, CancellationToken cancellationToken) => restCollection != null
            ? ioClient.ApiClient.Collection.RenameCollectionAsync(ioClient.CurrentAuthenticationContextUserID, restCollection.Id, targetName, cancellationToken)
            : ioClient.ApiClient.Folder.RenameFolderAsync(ioClient.CurrentAuthenticationContextUserID, restFolder.Id, targetName, cancellationToken);

        /// <summary>Moves this folder into a collection or folder using asynchronous HTTP I/O.</summary>
        /// <param name="targetDirectory">The target collection or folder.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the move and local metadata/cache update.</returns>
        /// <remarks>Parent and collection metadata change only after success. Both parent listings are invalidated after uncertain failures.</remarks>
        /// <exception cref="NotSupportedException">The source is a root or collection, or the target is a root.</exception>
        public async Task MoveAsync(DirectoryInfo targetDirectory, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (targetDirectory == null) throw new ArgumentNullException(nameof(targetDirectory));
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory || restCollection != null || targetDirectory.IsRootDirectory)
                throw new NotSupportedException("Only folders can move into a collection or folder.");
            var collectionId = targetDirectory.AssociatedCollection.CollectionID;
            try { await MoveCoreAsync(targetDirectory.FolderID, collectionId, cancellationToken).ConfigureAwait(false); }
            finally
            {
                ParentDirectory?.ResetDirectoriesCache();
                targetDirectory.ResetDirectoriesCache();
            }
            parentDirectory = targetDirectory;
            restFolder.Parent = targetDirectory.FolderID;
            restFolder.Collection = collectionId;
        }

        /// <summary>Moves this folder through the native asynchronous REST client.</summary>
        /// <param name="targetFolderId">The target folder identifier, or null for the collection root.</param>
        /// <param name="targetCollectionId">The target collection identifier.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the remote mutation before local metadata/cache updates.</returns>
        protected virtual Task MoveCoreAsync(string targetFolderId, string targetCollectionId, CancellationToken cancellationToken) =>
            ioClient.ApiClient.Folder.MoveFolderAsync(ioClient.CurrentAuthenticationContextUserID, restFolder.Id, targetFolderId, targetCollectionId, cancellationToken);

        /// <summary>Adds directory user/group sharings using asynchronous HTTP I/O.</summary>
        /// <param name="userIDs">The user identifiers, with the existing null/empty semantics.</param>
        /// <param name="groupIDs">The group identifiers, with the existing null/empty semantics.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the sharing mutation and parent-cache invalidation.</returns>
        public Task AddSharingAsync(string[] userIDs, string[] groupIDs, CancellationToken cancellationToken = default(CancellationToken)) =>
            ChangeSharingAsync(userIDs, groupIDs, false, cancellationToken);

        /// <summary>Removes directory user/group sharings using asynchronous HTTP I/O.</summary>
        /// <param name="userIDs">The user identifiers, with the existing null/empty semantics.</param>
        /// <param name="groupIDs">The group identifiers, with the existing null/empty semantics.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the sharing mutation and parent-cache invalidation.</returns>
        public Task RemoveSharingAsync(string[] userIDs, string[] groupIDs, CancellationToken cancellationToken = default(CancellationToken)) =>
            ChangeSharingAsync(userIDs, groupIDs, true, cancellationToken);

        private async Task ChangeSharingAsync(string[] users, string[] groups, bool remove, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsRootDirectory) throw new NotSupportedException("Sharing of root directory is not supported");
            try { await ChangeSharingCoreAsync(users, groups, remove, cancellationToken).ConfigureAwait(false); }
            finally { ParentDirectory?.ResetDirectoriesCache(); }
        }

        /// <summary>Changes directory sharings through the native asynchronous REST client.</summary>
        /// <param name="users">The user identifiers.</param>
        /// <param name="groups">The group identifiers.</param>
        /// <param name="remove">Whether the identifiers are removed rather than added.</param>
        /// <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        /// <returns>A task representing the remote mutation before parent-cache invalidation.</returns>
        protected virtual async Task ChangeSharingCoreAsync(string[] users, string[] groups, bool remove, CancellationToken cancellationToken)
        {
            if (restCollection != null)
            {
                if (remove) await ioClient.ApiClient.Collection.UnshareCollectionAsync(ioClient.CurrentAuthenticationContextUserID, restCollection.Id, users, groups, cancellationToken).ConfigureAwait(false);
                else await ioClient.ApiClient.Collection.ShareCollectionAsync(ioClient.CurrentAuthenticationContextUserID, restCollection.Id, users, groups, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (remove) await ioClient.ApiClient.Folder.UnshareFolderAsync(ioClient.CurrentAuthenticationContextUserID, restFolder.Id, users, groups, cancellationToken).ConfigureAwait(false);
                else await ioClient.ApiClient.Folder.ShareFolderAsync(ioClient.CurrentAuthenticationContextUserID, restFolder.Id, users, groups, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
