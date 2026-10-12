using CenterDevice.IO;
using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using CompuMaster.Scopevisio.OpenApi;
using CompuMaster.Scopevisio.OpenApi.Client;
using CompuMaster.Scopevisio.Teamwork;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Directory = CenterDevice.IO.DirectoryInfo;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    public partial class DocumentVersionBridgeTests
    {
        private static readonly string ParallelLedgerPath = Path.Combine(Path.GetTempPath(), "CenterDevice.API229.ParallelDirectories.owned-resources.json");

        [Test, Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task AddFileLinksWithSpecialCenterDeviceBehaviour_Parallelized()
        {
            RequireLease();
            client = null;
            Exception failure = null, cleanupError = null;
            ParallelLedger owned = null;
            TeamworkIOClient io = null;
            using (var transport = CenterDeviceHttpTransport.CreateHttpClient(new System.Net.Http.HttpClientHandler()))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8)))
            {
                try
                {
                    phase = "parallel fixture authorization";
                    var config = new Configuration { HttpClient = transport, Username = Credential("TEST_USERNAME", "username"),
                        ClientNumber = Credential("TEST_CUSTOMERNO", "customer no."), Password = Credential("TEST_PASSWORD", "password") };
                    var open = new OpenScopeApiClient(config);
                    await open.AuthorizeWithUserCredentialsAsync(timeout.Token);
                    client = new BridgeClient(open);
                    contextId = open.Token.Uid;
                    ownerId = (await client.User.GetAuthenticatedUserDataAsync(contextId, timeout.Token)).Id;
                    Check(!string.IsNullOrWhiteSpace(ownerId), "The actual owning CenterDevice user is required.");
                    io = new TeamworkIOClient(client);
                    phase = "parallel fixture recovery";
                    if (File.Exists(ParallelLedgerPath))
                    {
                        owned = JsonSerializer.Deserialize<ParallelLedger>(File.ReadAllText(ParallelLedgerPath));
                        await CleanupParallelResources(owned, io, timeout.Token);
                        owned = null;
                    }
                    owned = new ParallelLedger { Owner = ownerId, Filename = "parallel-" + Guid.NewGuid().ToString("N") + ".txt",
                        Scopes = new List<ParallelScope> {
                            new ParallelScope { Name = Prefix + Guid.NewGuid().ToString("N"), Paths = new List<string> { "Source", "Empty1", "Empty2" } },
                            new ParallelScope { Name = Prefix + Guid.NewGuid().ToString("N"), Paths = new List<string> { "Target1", "Target2" } } } };
                    SaveParallelLedger(owned); // Record both exact root intentions before either can be created.
                    var existing = await Collections(timeout.Token);
                    Check(!existing.Any(collection => owned.Scopes.Any(scope => scope.Name == collection.Name)), "Both fresh collection names must be absent.");
                    CheckLease();
                    phase = "parallel collection creation and root publication";
                    await Task.WhenAll(owned.Scopes.Select(scope => Task.Run(() => io.RootDirectory.CreateDirectory(scope.Name))).ToArray());
                    foreach (var scope in owned.Scopes) scope.Id = io.RootDirectory.GetDirectory(scope.Name).CollectionID;
                    Check(owned.Scopes.Select(scope => scope.Id).Distinct().Count() == 2 && owned.Scopes.All(scope => !string.IsNullOrWhiteSpace(scope.Id)),
                        "Both parallel collection creations must confirm distinct identities.");
                    SaveParallelLedger(owned);
                    var sourceCollection = io.RootDirectory.GetDirectory(owned.Scopes[0].Name);
                    var targetCollection = io.RootDirectory.GetDirectory(owned.Scopes[1].Name);
                    phase = "parallel child creation";
                    CheckLease();
                    await Task.WhenAll(new[] { Task.Run(() => sourceCollection.CreateDirectory("Source")), Task.Run(() => sourceCollection.CreateDirectory("Empty1")),
                        Task.Run(() => sourceCollection.CreateDirectory("Empty2")), Task.Run(() => targetCollection.CreateDirectory("Target1")), Task.Run(() => targetCollection.CreateDirectory("Target2")) });
                    var source = sourceCollection.GetDirectory("Source");
                    var firstTarget = targetCollection.GetDirectory("Target1");
                    var secondTarget = targetCollection.GetDirectory("Target2");
                    CheckLease();
                    phase = "owned document upload and reference changes";
                    source.UploadAndCreateNewFile(() => Payload("parallel-reference-payload"), owned.Filename);
                    var original = source.GetFile(owned.Filename);
                    owned.Documents.Add(original.ID);
                    SaveParallelLedger(owned);
                    Check((await client.Document.GetDocumentMetadataAsync(contextId, original.ID, timeout.Token)).Owner == ownerId, "The source document must be owned by the fixture user.");
                    targetCollection.AddExistingFile(original);
                    firstTarget.AddExistingFile(original);
                    secondTarget.AddExistingFile(original);
                    // A document has one location per collection; cached old locations must be explicitly reset.
                    source.ResetFilesCache(); targetCollection.ResetFilesCache(); firstTarget.ResetFilesCache(); secondTarget.ResetFilesCache();
                    Check(source.GetFile(owned.Filename).ID == original.ID && secondTarget.GetFile(owned.Filename).ID == original.ID,
                        "The original source reference and the final destination reference must share document identity.");
                    Check(!targetCollection.FileExists(owned.Filename) && !firstTarget.FileExists(owned.Filename), "The destination collection must have only its latest folder location.");
                    await CheckPayload(original.ID, null, "parallel-reference-payload", timeout.Token);
                    phase = "parallel child deletion and mixed listing";
                    var empty1 = sourceCollection.GetDirectory("Empty1");
                    var empty2 = sourceCollection.GetDirectory("Empty2");
                    CheckLease();
                    await Task.WhenAll(Task.Run(() => empty1.Delete()), Task.Run(() => empty2.Delete()));
                    var syncListing = Task.Run(() => sourceCollection.GetDirectories());
                    var asyncListing = sourceCollection.GetDirectoriesAsync(timeout.Token);
                    var listings = await Task.WhenAll(syncListing, asyncListing);
                    Check(ReferenceEquals(listings[0], listings[1]) && listings[0].Length == 1 && listings[0][0].FolderID == source.FolderID,
                        "Mixed listings after parallel deletion must share the current snapshot and preserve the source folder.");
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    if (client != null && owned != null && io != null)
                    {
                        using (var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                        {
                            try { await CleanupParallelResources(owned, io, cleanupTimeout.Token); }
                            catch (Exception error) { cleanupError = error; }
                        }
                    }
                }
                if (failure != null || cleanupError != null)
                    Assert.Fail("Phase: " + phase + "; failure: " + Diagnostic(failure) + "; cleanup phase: " + cleanupPhase + "; cleanup: " + Diagnostic(cleanupError));
                TestContext.Out.WriteLine("Parallel root/child setup, stable source/destination references, mixed listing, child/root deletion and complete owned cleanup verified.");
            }
        }

        private async Task CleanupParallelResources(ParallelLedger owned, TeamworkIOClient io, CancellationToken token)
        {
            CheckLease(false);
            cleanupPhase = "parallel ledger validation";
            Check(owned != null && owned.Owner == ownerId && !string.IsNullOrWhiteSpace(owned.Filename) && owned.Filename.StartsWith("parallel-", StringComparison.Ordinal) &&
                owned.Scopes != null && owned.Scopes.Count == 2 && owned.Scopes.Select(scope => scope.Name).Distinct().Count() == 2,
                "Recovery requires this fixture's exact two collection intentions.");
            var collections = await Collections(token);
            var confirmed = new Dictionary<ParallelScope, Collection>();
            var foldersByScope = new Dictionary<ParallelScope, List<Folder>>();
            foreach (var scope in owned.Scopes)
            {
                Check(scope.Name != null && scope.Name.StartsWith(Prefix, StringComparison.Ordinal) && Guid.TryParseExact(scope.Name.Substring(Prefix.Length), "N", out _),
                    "Recovery may only target exactly named test collections.");
                var matches = collections.Where(collection => collection.Name == scope.Name).ToList();
                Check(matches.Count <= 1, "An ambiguous test collection prevents cleanup.");
                if (matches.Count == 0) continue;
                Check(scope.Id == null || scope.Id == matches[0].Id, "The recovered collection must agree with the recorded identity.");
                scope.Id = matches[0].Id;
                var ownedCollection = await client.Collection.GetCollectionAsync(contextId, scope.Id, RestRequestFields.ALL, token);
                Check(ownedCollection.Owner == ownerId && ownedCollection.Name == scope.Name && !ownedCollection.IsShared && !ownedCollection.Archived && !ownedCollection.IsIntelligent,
                    "Every cleanup collection must be ordinary, private and owned by the fixture user.");
                confirmed.Add(scope, ownedCollection);
                var page = await client.Documents.GetAsync<DocumentFullMetadata>(contextId, scope.Id, null, null, 0, 500, token);
                Check(page?.Documents != null && page.Hits == page.Documents.Count && page.Documents.Count <= 1,
                    "Unexpected or incomplete document contents prevent cleanup.");
                foreach (var document in page.Documents)
                {
                    Check(document.Owner == ownerId && document.Filename == owned.Filename, "Only the fixture's owned document may be removed.");
                    Check(document.Collections?.Visible != null && document.Collections.Visible.All(id => owned.Scopes.Any(candidate => candidate.Id == id || candidate.Name == collections.FirstOrDefault(c => c.Id == id)?.Name)),
                        "The fixture document must not have references outside the recorded owned collections.");
                    if (!owned.Documents.Contains(document.Id)) owned.Documents.Add(document.Id);
                }
                var folders = (await client.Folders.GetFoldersAsync(contextId, scope.Id, RestApiConstants.NONE, null, null,
                    new[] { "id", "name", "parent", "collection", "users", "groups", "link" }, token)).Folders;
                Check(folders != null && folders.Count <= scope.Paths.Count && folders.Select(folder => folder.Name).Distinct().Count() == folders.Count,
                    "Every folder must be represented once in the complete bounded inventory.");
                foreach (var folder in folders)
                {
                    Check(folder.Collection == scope.Id && folder.Parent == RestApiConstants.NONE && !folder.IsShared && scope.Paths.Contains(folder.Name),
                        "Only exact private recorded top-level folders may be removed.");
                    Check(!scope.Folders.TryGetValue(folder.Name, out var id) || id == folder.Id, "Recovered folder identities must agree with the journal.");
                    scope.Folders[folder.Name] = folder.Id;
                    Check((await client.Folders.GetFoldersAsync(contextId, null, folder.Id, null, token)).Folders.Count == 0,
                        "Unexpected nested folders prevent cleanup.");
                }
                foldersByScope.Add(scope, folders);
            }
            SaveParallelLedger(owned); // All scopes are validated before a global document erase can affect either reference.
            cleanupPhase = "owned parallel folder removal";
            foreach (var scope in confirmed.Keys)
            {
                foreach (var folder in foldersByScope[scope]) await client.Folder.EraseFolderAsync(contextId, folder.Id, true, token);
                Check((await client.Folders.GetFoldersAsync(contextId, scope.Id, RestApiConstants.NONE, null, token)).Folders.Count == 0,
                    "Owned children must be absent before collection removal.");
                var page = await client.Documents.GetAsync<DocumentFullMetadata>(contextId, scope.Id, null, null, 0, 500, token);
                Check(page?.Hits == 0 && page.Documents?.Count == 0, "Owned collections must be empty before high-level deletion.");
            }
            cleanupPhase = "parallel collection deletion and root-cache absence";
            var root = io.RootDirectory;
            await root.GetDirectoriesAsync(token);
            await Task.WhenAll(confirmed.Select(pair => Task.Run(() => new Directory(io, root, pair.Value).Delete())).ToArray());
            Check(!(await root.GetDirectoriesAsync(token)).Any(directory => owned.Scopes.Any(scope => directory.CollectionID == scope.Id || directory.Name == scope.Name)),
                "The shared root cache must not retain any deleted owned collection.");
            Check(!(await Collections(token)).Any(collection => owned.Scopes.Any(scope => collection.Id == scope.Id || collection.Name == scope.Name)),
                "Owned collection deletion must be confirmed by a fresh server listing.");
            cleanupPhase = "parallel document and trash absence";
            var trash = (await client.Trash.List(contextId, token)).Where(document => owned.Documents.Contains(document.Id)).ToList();
            Check(trash.All(document => document.Filename == owned.Filename), "Trash entries must match the exact owned document identity.");
            if (trash.Count != 0) await client.Trash.Erase(contextId, trash.Select(document => document.Id).ToArray(), token);
            Check(!(await client.Trash.List(contextId, token)).Any(document => owned.Documents.Contains(document.Id)), "The fixture document must be absent from trash.");
            foreach (var id in owned.Documents)
            {
                var absent = false;
                try { await client.Document.GetDocumentMetadataAsync(contextId, id, token); }
                catch (CenterDevice.Rest.Exceptions.NotFoundException) { absent = true; }
                Check(absent, "Every owned document must be absent from active metadata.");
            }
            File.Delete(ParallelLedgerPath);
        }

        private static void SaveParallelLedger(ParallelLedger owned) => File.WriteAllText(ParallelLedgerPath, JsonSerializer.Serialize(owned), new UTF8Encoding(true));
        public sealed class ParallelLedger
        {
            public string Owner { get; set; }
            public string Filename { get; set; }
            public List<ParallelScope> Scopes { get; set; } = new List<ParallelScope>();
            public List<string> Documents { get; set; } = new List<string>();
        }
        public sealed class ParallelScope
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public List<string> Paths { get; set; } = new List<string>();
            public Dictionary<string, string> Folders { get; set; } = new Dictionary<string, string>();
        }
    }
}
