using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using CompuMaster.Scopevisio.Teamwork;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Directory = CenterDevice.IO.DirectoryInfo;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    public partial class DocumentVersionBridgeTests
    {
        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task CopyAndMoveThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false, copyAndMove: true);

        private async Task VerifyCopyAndMove(bool sync, CancellationToken token)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var nameA = "copy-a-" + suffix + ".txt";
            var nameB = "copy-b-" + suffix + ".txt";
            var singleName = "single-" + suffix + ".txt";
            var bulkNames = new[] { "bulk-one-" + suffix + ".txt", "bulk-two-" + suffix + ".txt" };
            var duplicateNames = new[] { "duplicate-one-" + suffix + ".txt", "duplicate-two-" + suffix + ".txt" };
            ledger.AllowedFilenames.AddRange(new[] { nameA, nameB, singleName }.Concat(bulkNames).Concat(duplicateNames));
            ledger.AllowedFolderPaths.AddRange(new[] { "Source", "Source/Child", "Source/Empty", "Target", "Target/Recursive", "Target/Recursive/Child", "Target/Recursive/Empty", "Target/Flat" });
            SaveLedger(); // Names and paths permit recovery even after an ambiguous creation/copy.
            var io = new TeamworkIOClient(client);
            var collection = new Directory(io, io.RootDirectory, await client.Collection.GetCollectionAsync(contextId, ledger.Id, RestRequestFields.ALL, token));
            var source = await CreateOwnedFolder(io, collection, "Source", token);
            var child = await CreateOwnedFolder(io, source, "Child", token);
            await CreateOwnedFolder(io, source, "Empty", token);
            var target = await CreateOwnedFolder(io, collection, "Target", token);
            var originalA = await UploadOwnedCopyDocument(source, nameA, "first", token);
            var originalB = await UploadOwnedCopyDocument(child, nameB, "child-payload", token);
            CheckLease();
            var version = (await client.Document.UploadNewVersionAsync(contextId, originalA, nameA, () => Payload("latest"), token)).Version;
            await client.Document.AddTagsAsync(contextId, originalA, new[] { "source-copy-tag" }, token);

            phase = "single document server copy";
            CheckLease();
            var single = sync ? client.Document.CopyDocument(contextId, originalA, singleName, folderId: target.FolderID, cancellationToken: token)
                : await client.Document.CopyDocumentAsync(contextId, originalA, singleName, folderId: target.FolderID, cancellationToken: token);
            RecordCopyIds(new[] { single });
            await VerifyOwnedCopy(single, originalA, singleName, "latest", token);

            phase = "bulk document server copy";
            CheckLease();
            var selections = new[] { new DocumentCopyRequest(originalA, bulkNames[0]), new DocumentCopyRequest(originalB, bulkNames[1]) };
            var bulk = sync ? client.Documents.CopyDocuments(contextId, selections, folderId: target.FolderID, cancellationToken: token)
                : await client.Documents.CopyDocumentsAsync(contextId, selections, folderId: target.FolderID, cancellationToken: token);
            RecordCopyIds(bulk);
            TestContext.Out.WriteLine("Bulk response count: " + bulk.Count + ".");
            Check(bulk.Count == 2 && bulk.Distinct().Count() == 2, "Bulk selections must create distinct documents.");
            var actualBulkNames = new List<string>();
            foreach (var id in bulk)
            {
                var metadata = await client.Document.GetDocumentMetadataAsync(contextId, id, token);
                Check(bulkNames.Contains(metadata.Filename), "Bulk results must match an intended name without assuming result mapping.");
                actualBulkNames.Add(metadata.Filename);
                await VerifyOwnedCopy(id, metadata.Filename == bulkNames[0] ? originalA : originalB, metadata.Filename,
                    metadata.Filename == bulkNames[0] ? "latest" : "child-payload", token);
            }
            Check(actualBulkNames.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(bulkNames.OrderBy(name => name, StringComparer.Ordinal)),
                "Bulk results must contain exactly one document for each intended filename.");

            phase = "repeated-source document server copy";
            CheckLease();
            selections = duplicateNames.Select(name => new DocumentCopyRequest(originalA, name)).ToArray();
            var duplicates = sync ? client.Documents.CopyDocuments(contextId, selections, folderId: target.FolderID, cancellationToken: token)
                : await client.Documents.CopyDocumentsAsync(contextId, selections, folderId: target.FolderID, cancellationToken: token);
            RecordCopyIds(duplicates);
            Check(duplicates.Count == 2 && duplicates.Distinct().Count() == 2, "Repeated source selections must create distinct documents through separate batches.");
            var actualDuplicateNames = new List<string>();
            foreach (var id in duplicates)
            {
                var metadata = await client.Document.GetDocumentMetadataAsync(contextId, id, token);
                Check(duplicateNames.Contains(metadata.Filename), "Repeated-source copies must retain both intended names.");
                actualDuplicateNames.Add(metadata.Filename);
                await VerifyOwnedCopy(id, originalA, metadata.Filename, "latest", token);
            }
            Check(actualDuplicateNames.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(duplicateNames.OrderBy(name => name, StringComparer.Ordinal)),
                "Repeated-source results must contain exactly one document for each intended filename.");

            phase = "recursive directory server copy";
            CheckLease();
            if (sync) target.AddCopy(source, "Recursive");
            else await target.AddDirectoryCopyAsync(source, "Recursive", cancellationToken: token);
            var recursive = await target.GetDirectoryAsync("Recursive", token);
            await VerifyCopyDirectory(recursive, nameA, originalA, "latest", token);
            var copiedChild = await recursive.GetDirectoryAsync("Child", token);
            await VerifyCopyDirectory(copiedChild, nameB, originalB, "child-payload", token);
            var empty = await recursive.GetDirectoryAsync("Empty", token);
            Check((await empty.GetFilesAsync(token)).Length == 0 && (await empty.GetDirectoriesAsync(token)).Length == 0, "Empty source folders must be preserved.");
            phase = "flat directory server copy";
            CheckLease();
            await target.AddDirectoryCopyAsync(source, "Flat", false, token);
            var flat = await target.GetDirectoryAsync("Flat", token);
            await VerifyCopyDirectory(flat, nameA, originalA, "latest", token);
            Check((await flat.GetDirectoriesAsync(token)).Length == 0, "A flat copy must exclude child folders.");

            phase = "file move between folder and collection destinations";
            var moving = await child.GetFileAsync(nameB, token);
            Check(moving != null && moving.ID == originalB, "The source file must retain its original identity.");
            foreach (var destination in new[] { target, collection, child })
            {
                CheckLease();
                var previous = moving.ParentDirectory;
                await previous.GetFilesAsync(token);
                await destination.GetFilesAsync(token); // Exercise cache invalidation, not just REST state.
                if (sync) moving.Move(destination);
                else await moving.MoveAsync(destination, token);
                Check(moving.ID == originalB && ReferenceEquals(moving.ParentDirectory, destination), "Move must preserve ID and publish the confirmed parent.");
                Check(!(await previous.GetFilesAsync(token)).Any(file => file.ID == originalB), "The old immediate location must no longer contain the moved document.");
                Check((await destination.GetFilesAsync(token)).Any(file => file.ID == originalB), "The destination must contain the unchanged document identity.");
                await CheckPayload(originalB, null, "child-payload", token);
            }
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                await ExpectCanceled(() => target.AddDirectoryCopyAsync(source, "Canceled", cancellationToken: canceled.Token));
                await ExpectCanceled(() => moving.MoveAsync(target, canceled.Token));
                await ExpectCanceled(async () => { await client.Document.CopyDocumentAsync(contextId, originalA, "canceled", folderId: target.FolderID, cancellationToken: canceled.Token); });
            }
            Check(!(await target.GetDirectoriesAsync(token)).Any(folder => folder.Name == "Canceled"), "Pre-cancellation must not create a directory.");
            Check((await source.GetFilesAsync(token)).Single().ID == originalA && (await child.GetFilesAsync(token)).Single().ID == originalB,
                "Copies and the round-trip move must preserve original references.");
            var sourceMetadata = await client.Document.GetDocumentMetadataAsync<CopyMetadata>(contextId, originalA, cancellationToken: token);
            Check(sourceMetadata.Version == version && sourceMetadata.Tags.Contains("source-copy-tag"), "Copy must preserve source version and tags.");
            Check((await client.Document.GetDocumentVersionHistoryAsync(contextId, originalA, token)).VersionDetails.Count == 2, "Source version history must remain intact.");
            await CheckPayload(originalA, null, "latest", token);
        }

        private async Task<Directory> CreateOwnedFolder(TeamworkIOClient io, Directory parent, string name, CancellationToken token)
        {
            CheckLease();
            var result = await client.Folders.CreateFolderAsync(contextId, name, ledger.Id, parent.FolderID, token);
            Check(!string.IsNullOrWhiteSpace(result.Id), "Folder creation must confirm an identity.");
            parent.ResetDirectoriesCache();
            return new Directory(io, parent, new Folder { Id = result.Id, Name = name, Collection = ledger.Id, Parent = parent.FolderID ?? RestApiConstants.NONE });
        }

        private async Task<string> UploadOwnedCopyDocument(Directory destination, string name, string payload, CancellationToken token)
        {
            CheckLease();
            var result = await client.Documents.UploadDocumentAsync(contextId, name, () => Payload(payload), null, new List<string> { ledger.Id }, new List<string> { destination.FolderID }, token);
            RecordCopyIds(new[] { result.Id });
            destination.ResetFilesCache();
            return result.Id;
        }

        private void RecordCopyIds(IEnumerable<string> ids)
        {
            foreach (var id in ids) if (!ledger.Documents.Contains(id)) ledger.Documents.Add(id);
            SaveLedger();
        }

        private async Task VerifyCopyDirectory(Directory directory, string name, string sourceId, string payload, CancellationToken token)
        {
            var files = await directory.GetFilesAsync(token);
            Check(files.Length == 1 && files[0].FileName == name, "The copied directory must contain exactly its intended immediate document.");
            RecordCopyIds(files.Select(file => file.ID));
            await VerifyOwnedCopy(files[0].ID, sourceId, name, payload, token);
        }

        private async Task VerifyOwnedCopy(string id, string sourceId, string name, string payload, CancellationToken token)
        {
            var metadata = await client.Document.GetDocumentMetadataAsync<CopyMetadata>(contextId, id, cancellationToken: token);
            Check(id != sourceId && metadata.Id == id && metadata.Owner == ownerId && metadata.Filename == name, "Copies must have new, owned identities and the intended filename.");
            Check(metadata.Tags == null || metadata.Tags.Count == 0, "Copies must not inherit source tags.");
            Check(!metadata.IsShared, "Copies in the private test scope must remain unshared.");
            Check((await client.Document.GetDocumentVersionHistoryAsync(contextId, id, token)).VersionDetails.Count == 1, "Copies must not inherit source history.");
            await CheckPayload(id, null, payload, token);
        }

        public sealed class CopyMetadata : DocumentFullMetadata
        {
            public List<string> Tags { get; set; }
        }

        private bool OwnedFilename(string filename)
            => filename == ledger.Filename || ledger.AllowedFilenames.Contains(filename);

        private async Task CleanupOwnedFolders(CancellationToken token)
        {
            cleanupPhase = "folder lookup";
            var folders = new List<Folder>();
            var parents = new Queue<string>();
            parents.Enqueue(RestApiConstants.NONE);
            while (parents.Count != 0)
            {
                var parentId = parents.Dequeue();
                var children = (await client.Folders.GetFoldersAsync(contextId, parentId == RestApiConstants.NONE ? ledger.Id : null, parentId, null, null,
                    new[] { "id", "name", "parent", "collection", "users", "groups", "link" }, token)).Folders;
                Check(children != null, "The owned collection's folder listing is required before cleanup.");
                foreach (var child in children)
                {
                    Check(child.Parent == parentId && !folders.Any(folder => folder.Id == child.Id) && folders.Count < ledger.AllowedFolderPaths.Count,
                        "The owned folder inventory must be complete, unique and bounded by recorded intent.");
                    folders.Add(child);
                    parents.Enqueue(child.Id);
                }
            }
            var byId = folders.ToDictionary(folder => folder.Id);
            var paths = new Dictionary<string, string>();
            foreach (var folder in folders)
            {
                Check(folder.Collection == ledger.Id && !folder.IsShared, "Cleanup folders must belong to the private owned collection.");
                var names = new List<string> { folder.Name };
                var seen = new HashSet<string> { folder.Id };
                var parent = folder.Parent;
                while (parent != RestApiConstants.NONE)
                {
                    Check(parent != null && byId.TryGetValue(parent, out _) && seen.Add(parent), "The owned folder tree must have complete, non-cyclic parent identities.");
                    names.Insert(0, byId[parent].Name);
                    parent = byId[parent].Parent;
                }
                var path = string.Join("/", names);
                Check(ledger.AllowedFolderPaths.Contains(path) && !paths.ContainsKey(path), "Only exact recorded folder paths may be removed.");
                Check(!ledger.FolderIds.TryGetValue(path, out var knownId) || knownId == folder.Id, "Folder recovery must preserve confirmed identities.");
                paths.Add(path, folder.Id);
                ledger.FolderIds[path] = folder.Id;
            }
            SaveLedger();
            foreach (var pair in paths.OrderByDescending(pair => pair.Key.Count(c => c == '/')))
            {
                cleanupPhase = "owned folder erase";
                await client.Folder.EraseFolderAsync(contextId, pair.Value, true, token);
            }
            cleanupPhase = "folder absence check";
            Check((await client.Folders.GetFoldersAsync(contextId, ledger.Id, RestApiConstants.NONE, null, token)).Folders.Count == 0, "Every owned folder must be absent before removing its collection.");
        }
    }
}
