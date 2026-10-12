using CenterDevice.IO;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Directory = CenterDevice.IO.DirectoryInfo;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class DirectoryCopyTests
    {
        [TestCase(false), TestCase(true)]
        public async Task RecursiveCopyPlansBeforeWritingAndPreservesNamesEmptyFoldersAndRepeatedFilenames(bool sync)
        {
            var io = new FakeIo();
            var source = io.Source();
            var target = io.Target();
            if (sync) target.AddCopy(source);
            else await target.AddDirectoryCopyAsync(source);
            Assert.That(io.Events.Where(e => e.StartsWith("create:")), Is.EqualTo(new[] { "create:Source", "create:Sub", "create:Empty" }));
            Assert.That(io.Copies.SelectMany(c => c).Select(d => d.DocumentId), Is.EqualTo(new[] { "original-a", "original-b", "original-c" }));
            Assert.That(io.Copies[0].Select(d => d.Filename), Is.EqualTo(new[] { "same.txt", "same.txt" }));
            var firstCreate = io.Events.FindIndex(e => e.StartsWith("create:"));
            Assert.That(io.Events.Skip(firstCreate).Any(e => e.StartsWith("read:")), Is.False, "The complete source tree is snapshotted before writes.");
            Assert.That(io.Created.All(d => d.Users == null && d.Groups == null), Is.True);
            Assert.That(io.Documents["source"].Select(d => d.Id), Is.EqualTo(new[] { "original-a", "original-b" }));
            Assert.That(source.CollectionID, Is.EqualTo("source"));
        }

        [Test]
        public async Task CopyIntoRootCreatesANormalCollectionAndPreservesVisibleStructure()
        {
            var io = new FakeIo();
            var root = new FakeDirectory(io);
            await root.AddDirectoryCopyAsync(io.Source(), "New collection");
            Assert.That(io.Created[0].Type, Is.EqualTo(Directory.DirectoryType.Collection));
            Assert.That(io.Created.Skip(1).All(d => d.Type == Directory.DirectoryType.Folder), Is.True);
            Assert.That(io.Created[0].IsIntelligent, Is.False);
        }

        [Test]
        public async Task NonRecursiveCopyUsesOnlyImmediateDocumentsAndDoesNotReadSourceChildren()
        {
            var io = new FakeIo();
            await io.Target().AddDirectoryCopyAsync(io.Source(), "Flat", false);
            Assert.That(io.Events, Does.Not.Contain("read:folders:source"));
            Assert.That(io.Created.Count, Is.EqualTo(1));
            Assert.That(io.Copies.Single().Count, Is.EqualTo(2));
        }

        [Test]
        public void ExistingNamedDestinationFailsWithoutSourceReadsOrWrites()
        {
            var io = new FakeIo();
            io.Folders["target"] = new List<Folder> { new Folder { Id = "existing", Name = "Source" } };
            Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source()); }));
            Assert.That(io.Events.Any(e => e.StartsWith("create:") || e == "read:files:source"), Is.False);
        }

        [TestCase(""), TestCase(" "), TestCase("."), TestCase(".."), TestCase("part/child"), TestCase("part\\child")]
        public void InvalidNamesFailBeforeAnyLookup(string name)
        {
            var io = new FakeIo();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source(), name); }));
            Assert.That(io.Events, Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public void SourceOrDescendantDestinationFailsBeforeLookup(bool descendant)
        {
            var io = new FakeIo();
            var source = io.Source();
            Directory target = descendant ? new FakeDirectory(io, source, new Folder { Id = "sub", Name = "Sub" }) : source;
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await target.AddDirectoryCopyAsync(source, "copy"); }));
            Assert.That(io.Events, Is.Empty);
        }

        [Test]
        public void AliasOfSourceIdentityAlsoFailsBeforeLookup()
        {
            var io = new FakeIo();
            var source = new Directory(io, io.RootDirectory, new Collection { Id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", Name = "source" });
            var alias = new FakeDirectory(io, io.RootDirectory, new Collection { Id = "AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", Name = "alias" });
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await alias.AddDirectoryCopyAsync(source, "copy"); }));
            Assert.That(io.Events, Is.Empty);
        }

        [Test]
        public void CopyingAcrossDifferentClientsDoesNotInferATenantFromMatchingNames()
        {
            var sourceIo = new FakeIo();
            var targetIo = new FakeIo();
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await targetIo.Target().AddDirectoryCopyAsync(sourceIo.Source()); }));
            Assert.That(sourceIo.Events, Is.Empty);
            Assert.That(targetIo.Events, Is.Empty);
        }

        [Test]
        public void RootSourceIsRejectedBeforeLookup()
        {
            var io = new FakeIo();
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.RootDirectory, "copy"); }));
            Assert.That(io.Events, Is.Empty);
        }

        [Test]
        public void RepeatedSourceFolderIdentityFailsDuringPlanningWithoutWrites()
        {
            var io = new FakeIo();
            io.Folders["sub"] = new List<Folder> { new Folder { Id = "sub", Name = "cycle" } };
            Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source()); }));
            Assert.That(io.Created, Is.Empty);
            Assert.That(io.Copies, Is.Empty);
        }

        [Test]
        public void FailureDuringPlanningPreservesOriginalExceptionAndCreatesNothing()
        {
            var io = new FakeIo { PlanningFailure = new IOException("isolated listing failure") };
            var error = Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source()); }));
            Assert.That(error, Is.SameAs(io.PlanningFailure));
            Assert.That(io.Created, Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public void PartialCreationOrCopyFailureIsNotRetriedOrRolledBack(bool creation)
        {
            var io = new FakeIo { FailCreate = creation, FailCopy = !creation };
            Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source()); }));
            Assert.That(io.Events.Count(e => e.StartsWith("create:")), Is.EqualTo(1));
            Assert.That(io.CopyCalls, Is.EqualTo(creation ? 0 : 1));
            Assert.That(io.Created.Count, Is.EqualTo(creation ? 0 : 1), "A confirmed partial destination remains for reconciliation.");
        }

        [Test]
        public void ServerReusingSourceDirectoryIdIsRejectedBeforeCopyingAnyDocument()
        {
            var io = new FakeIo { ReturnedFolderId = "sub" };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source()); }));
            Assert.That(io.CopyCalls, Is.Zero);
        }

        [Test]
        public void PreCancellationDoesNotReadOrCreate()
        {
            var io = new FakeIo();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source(), cancellationToken: cancellation.Token); }));
            }
            Assert.That(io.Events, Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task CancellationDuringPlanningOrCopyReachesTheActiveOperation(bool writing)
        {
            var io = new FakeIo { BlockPlanning = !writing, BlockCopy = writing };
            using (var cancellation = new CancellationTokenSource())
            {
                var task = io.Target().AddDirectoryCopyAsync(io.Source(), cancellationToken: cancellation.Token);
                await io.Entered.Task;
                Assert.That(io.LastToken, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await task; }));
            }
            Assert.That(io.Created.Count, Is.EqualTo(writing ? 1 : 0));
        }

        [TestCase(500), TestCase(501), TestCase(1001)]
        public async Task EveryDocumentPageIsPlannedBeforeAnyDestinationIsCreated(int count)
        {
            var io = new FakeIo();
            io.Documents["source"] = Enumerable.Range(0, count).Select(i => new DocumentFullMetadata { Id = "document-" + i, Filename = "same.txt" }).ToList();
            await io.Target().AddDirectoryCopyAsync(io.Source(), "paged", false);
            Assert.That(io.PageOffsets, Is.EqualTo(Enumerable.Range(0, (count + 499) / 500).Select(i => i * 500)));
            Assert.That(io.Copies.Single().Count, Is.EqualTo(count));
            Assert.That(io.Copies.Single().Select(d => d.DocumentId).Distinct().Count(), Is.EqualTo(count));
            Assert.That(io.Events.Skip(io.Events.FindIndex(e => e.StartsWith("create:"))).Any(e => e.StartsWith("read:")), Is.False);
        }

        [TestCase("empty"), TestCase("changed-total"), TestCase("repeated-id"), TestCase("null-page"), TestCase("negative-total"), TestCase("oversized-page")]
        public void IncompleteOrUnstablePagesFailBeforeAnyWrites(string problem)
        {
            var io = new FakeIo();
            io.Documents["source"] = Enumerable.Range(0, 501).Select(i => new DocumentFullMetadata { Id = "document-" + i, Filename = "same.txt" }).ToList();
            io.PageTransform = (offset, page) =>
            {
                if (problem == "null-page") return null;
                if (problem == "negative-total") page.Hits = -1;
                if (problem == "oversized-page") page.Documents.Add(page.Documents[0]);
                if (offset != 0)
                {
                    if (problem == "empty") page.Documents.Clear();
                    if (problem == "changed-total") page.Hits++;
                    if (problem == "repeated-id") page.Documents[0].Id = "document-0";
                }
                return page;
            };
            Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await io.Target().AddDirectoryCopyAsync(io.Source(), "paged", false); }));
            Assert.That(io.Created, Is.Empty);
            Assert.That(io.Copies, Is.Empty);
        }

        private sealed class FakeIo : IOClientBase
        {
            internal readonly Dictionary<string, List<Folder>> Folders = new Dictionary<string, List<Folder>>();
            internal readonly Dictionary<string, List<DocumentFullMetadata>> Documents = new Dictionary<string, List<DocumentFullMetadata>>();
            internal readonly List<string> Events = new List<string>();
            internal readonly List<FakeDirectory> Created = new List<FakeDirectory>();
            internal readonly List<IReadOnlyList<DocumentCopyRequest>> Copies = new List<IReadOnlyList<DocumentCopyRequest>>();
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal CancellationToken LastToken;
            internal IOException PlanningFailure;
            internal bool FailCreate, FailCopy, BlockPlanning, BlockCopy;
            internal string ReturnedFolderId;
            internal int CopyCalls;
            internal readonly List<int> PageOffsets = new List<int>();
            internal Func<int, DocumentSearchResults<DocumentFullMetadata>, DocumentSearchResults<DocumentFullMetadata>> PageTransform;
            internal FakeIo() : base(null, "fixture")
            {
                Documents["source"] = new List<DocumentFullMetadata> { new DocumentFullMetadata { Id = "original-a", Filename = "same.txt" }, new DocumentFullMetadata { Id = "original-b", Filename = "same.txt" } };
                Documents["sub"] = new List<DocumentFullMetadata> { new DocumentFullMetadata { Id = "original-c", Filename = "child.txt" } };
                Folders["source"] = new List<Folder> { new Folder { Id = "sub", Name = "Sub", Collection = "source" } };
                Folders["sub"] = new List<Folder> { new Folder { Id = "empty", Name = "Empty", Collection = "source" } };
            }
            internal Directory Source() => new Directory(this, RootDirectory, new Collection { Id = "source", Name = "Source", Owner = "owner", Users = new CenterDevice.Rest.Clients.Common.Sharings { NotVisibleCount = 1 } });
            internal FakeDirectory Target() => new FakeDirectory(this, RootDirectory, new Collection { Id = "target", Name = "Target" });
            protected internal override Task<List<Collection>> LookupCollectionsAsync(CancellationToken token)
            {
                Events.Add("read:collections");
                return Task.FromResult(new List<Collection> { new Collection { Id = "source", Name = "Source" }, new Collection { Id = "target", Name = "Target" } });
            }
            protected internal override Task<List<Folder>> LookupChildFoldersAsync(string collection, string parent, CancellationToken token)
            {
                var key = parent == "none" ? collection : parent;
                Events.Add("read:folders:" + key);
                return Task.FromResult(Folders.TryGetValue(key, out var list) ? list : new List<Folder>());
            }
            protected internal override async Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collection, string parent, CancellationToken token)
            {
                var key = parent == "none" ? collection : parent;
                Events.Add("read:files:" + key);
                if (PlanningFailure != null) throw PlanningFailure;
                if (BlockPlanning) { LastToken = token; Entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, token); }
                return Documents.TryGetValue(key, out var list) ? list : new List<DocumentFullMetadata>();
            }
            protected internal override async Task<DocumentSearchResults<DocumentFullMetadata>> LookupChildDocumentPageAsync(string collection, string parent, int offset, int rows, CancellationToken token)
            {
                PageOffsets.Add(offset);
                var documents = await LookupChildDocumentsAsync(collection, parent, token);
                var page = new DocumentSearchResults<DocumentFullMetadata> { Hits = documents.Count, Documents = documents.Skip(offset).Take(rows).ToList() };
                return PageTransform == null ? page : PageTransform(offset, page);
            }
        }
        private sealed class FakeDirectory : Directory
        {
            private readonly FakeIo io;
            internal FakeDirectory(FakeIo io) : base(io) { this.io = io; }
            internal FakeDirectory(FakeIo io, Directory parent, Collection collection) : base(io, parent, collection) { this.io = io; }
            internal FakeDirectory(FakeIo io, Directory parent, Folder folder) : base(io, parent, folder) { this.io = io; }
            protected override Task<Directory> CreateCopyChildAsync(string name, CancellationToken token)
            {
                io.Events.Add("create:" + name);
                try
                {
                    if (io.FailCreate) throw new IOException("isolated uncertain creation failure");
                    var id = io.ReturnedFolderId ?? "new-" + io.Created.Count;
                    var child = IsRootDirectory ? new FakeDirectory(io, this, new Collection { Id = id, Name = name })
                        : new FakeDirectory(io, this, new Folder { Id = id, Name = name, Collection = AssociatedCollection.CollectionID });
                    io.Created.Add(child);
                    return Task.FromResult<Directory>(child);
                }
                finally { ResetDirectoriesCache(); }
            }
            protected override async Task CopyDirectoryFilesAsync(IReadOnlyList<DocumentCopyRequest> documents, CancellationToken token)
            {
                io.CopyCalls++;
                io.Copies.Add(documents);
                if (io.FailCopy) throw new IOException("isolated uncertain copy failure");
                if (io.BlockCopy) { io.LastToken = token; io.Entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, token); }
                ResetFilesCache();
            }
        }
    }
}
