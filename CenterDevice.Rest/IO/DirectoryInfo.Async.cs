using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.IO
{
    public partial class DirectoryInfo
    {
        private readonly SemaphoreSlim asynchronousListing = new SemaphoreSlim(1, 1);
        private readonly object asynchronousCachePublication = new object();
        private int directoriesGeneration;
        private int filesGeneration;

        /// <summary>Retrieves and caches immediate child directories using native asynchronous I/O.</summary>
        /// <param name="cancellationToken">Cancels waiting or the active listing request.</param>
        /// <returns>The immediate child directories, retaining their metadata and parent references.</returns>
        /// <remarks>Concurrent asynchronous callers reuse a successful cached listing. A failed or canceled listing is not cached. Do not mix concurrent synchronous mutation/listing calls on the same directory.</remarks>
        public async Task<DirectoryInfo[]> GetDirectoriesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await asynchronousListing.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (getDirectories != null) return getDirectories;
                var generation = Volatile.Read(ref directoriesGeneration);
                var directories = new List<DirectoryInfo>();
                List<Rest.Clients.Folders.Folder> fetchedFolders = null;
                if (IsRootDirectory)
                {
                    var collections = await ioClient.LookupCollectionsAsync(cancellationToken).ConfigureAwait(false);
                    if (collections != null)
                        foreach (var collection in collections) directories.Add(new DirectoryInfo(ioClient, this, collection));
                }
                else
                {
                    fetchedFolders = await ioClient.LookupChildFoldersAsync(restCollection?.Id,
                        restCollection != null ? Rest.RestApiConstants.NONE : restFolder.Id, cancellationToken).ConfigureAwait(false);
                    if (fetchedFolders != null)
                        foreach (var folder in fetchedFolders) directories.Add(new DirectoryInfo(ioClient, this, folder));
                }
                cancellationToken.ThrowIfCancellationRequested();
                var result = directories.ToArray();
                lock (asynchronousCachePublication)
                    if (generation == directoriesGeneration)
                    {
                        getDirectories = result;
                        if (restCollection != null) restCollection.SubFolders = fetchedFolders;
                        if (restFolder != null) restFolder.SubFolders = fetchedFolders;
                    }
                return result;
            }
            finally { asynchronousListing.Release(); }
        }

        /// <summary>Retrieves and caches immediate files using native asynchronous I/O.</summary>
        /// <param name="cancellationToken">Cancels waiting or the active listing request.</param>
        /// <returns>The immediate files, retaining their metadata and parent references.</returns>
        /// <remarks>Root directories contain no files. Failed or canceled listings are not cached. Do not mix concurrent synchronous mutation/listing calls on the same directory.</remarks>
        public async Task<FileInfo[]> GetFilesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await asynchronousListing.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (getFiles != null) return getFiles;
                var generation = Volatile.Read(ref filesGeneration);
                var files = new List<FileInfo>();
                List<Rest.Clients.Documents.Metadata.DocumentFullMetadata> fetchedDocuments = null;
                if (!IsRootDirectory)
                {
                    fetchedDocuments = await ioClient.LookupChildDocumentsAsync(restCollection?.Id,
                        restCollection != null ? Rest.RestApiConstants.NONE : restFolder.Id, cancellationToken).ConfigureAwait(false);
                    if (fetchedDocuments != null)
                        foreach (var document in fetchedDocuments) files.Add(new FileInfo(ioClient, this, document));
                }
                cancellationToken.ThrowIfCancellationRequested();
                var result = files.ToArray();
                lock (asynchronousCachePublication)
                    if (generation == filesGeneration)
                    {
                        getFiles = result;
                        if (restCollection != null) restCollection.Documents = fetchedDocuments;
                        if (restFolder != null) restFolder.Documents = fetchedDocuments;
                    }
                return result;
            }
            finally { asynchronousListing.Release(); }
        }

        /// <summary>Retrieves a named immediate child directory asynchronously.</summary>
        /// <param name="directoryName">The child name, an empty name for this directory, or a slash for its root.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The requested directory.</returns>
        /// <exception cref="Model.Exceptions.DirectoryNotFoundException">The named child does not exist.</exception>
        public async Task<DirectoryInfo> GetDirectoryAsync(string directoryName, CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(directoryName)) return this;
            if (directoryName == "/") return RootDirectory;
            foreach (var directory in await GetDirectoriesAsync(cancellationToken).ConfigureAwait(false))
                if (directory.Name == directoryName) return directory;
            throw new Model.Exceptions.DirectoryNotFoundException(directoryName);
        }

        /// <summary>Opens a directory path using asynchronous child lookup.</summary>
        /// <param name="path">The path split using the existing path transformation rules.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The requested directory.</returns>
        /// <remarks>Use the name-array overload when names contain reserved path separators.</remarks>
        public Task<DirectoryInfo> OpenDirectoryPathAsync(string path, CancellationToken cancellationToken = default(CancellationToken)) =>
            OpenDirectoryPathAsync(ioClient.Paths.SplitPath(path), cancellationToken);

        /// <summary>Opens a directory path represented by individual names asynchronously.</summary>
        /// <param name="directoryNames">The names in order, including existing current, parent, and root markers.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The requested directory.</returns>
        public async Task<DirectoryInfo> OpenDirectoryPathAsync(string[] directoryNames, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (directoryNames == null) throw new ArgumentNullException(nameof(directoryNames));
            var current = this;
            foreach (var name in directoryNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(name) || name == PathTransformations.DIRECTORY_NAME_CURRENT) continue;
                if (name == ioClient.Paths.DirectorySeparatorChar.ToString() || name == ioClient.Paths.AltDirectorySeparatorChar.ToString())
                    current = current.RootDirectory;
                else if (name == PathTransformations.DIRECTORY_NAME_PARENT)
                    current = current.parentDirectory ?? throw new System.IO.DirectoryNotFoundException("There is no parent directory for " + current.FullName);
                else
                    current = await current.GetDirectoryAsync(name, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return current;
        }

        /// <summary>Retrieves a named immediate file asynchronously, returning null when absent.</summary>
        /// <param name="fileName">The exact file name.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The first matching file, or null.</returns>
        public async Task<FileInfo> TryGetFileAsync(string fileName, CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (var file in await GetFilesAsync(cancellationToken).ConfigureAwait(false))
                if (file.FileName == fileName) return file;
            return null;
        }

        /// <summary>Retrieves a named immediate file asynchronously.</summary>
        /// <param name="fileName">The exact file name.</param>
        /// <param name="cancellationToken">Cancels waiting or active HTTP I/O.</param>
        /// <returns>The first matching file.</returns>
        /// <exception cref="Model.Exceptions.FileNotFoundException">The file does not exist.</exception>
        public async Task<FileInfo> GetFileAsync(string fileName, CancellationToken cancellationToken = default(CancellationToken)) =>
            await TryGetFileAsync(fileName, cancellationToken).ConfigureAwait(false) ?? throw new Model.Exceptions.FileNotFoundException(ioClient.Paths.CombinePath(Path, fileName));
    }
}
