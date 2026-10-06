using CenterDevice.IO;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class AsyncDirectoryTests
    {
        [Test]
        public async Task ConcurrentListingsReuseTheCompletedCacheAndParentReferences()
        {
            var entered = Signal();
            var release = Signal();
            var io = new FakeIo { Collections = async token => { entered.SetResult(true); await release.Task; return Collections(); } };
            var root = io.RootDirectory;
            var first = root.GetDirectoriesAsync();
            await entered.Task;
            var second = root.GetDirectoriesAsync();
            Assert.That(second.IsCompleted, Is.False);
            release.SetResult(true);
            var values = await Task.WhenAll(first, second);
            Assert.That(io.CollectionCalls, Is.EqualTo(1));
            Assert.That(values[0], Is.SameAs(values[1]));
            Assert.That(values[0][0].ParentDirectory, Is.SameAs(root));
            Assert.That(values[0][0].CollectionID, Is.EqualTo("collection-id"));
        }

        [Test]
        public async Task CanceledWaiterDoesNotCancelTheListingOwner()
        {
            var entered = Signal();
            var release = Signal();
            var io = new FakeIo { Collections = async token => { entered.SetResult(true); await release.Task; return Collections(); } };
            var root = io.RootDirectory;
            using (var cancellation = new CancellationTokenSource())
            {
                var owner = root.GetDirectoriesAsync();
                await entered.Task;
                var waiter = root.GetDirectoriesAsync(cancellation.Token);
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await waiter; }));
                Assert.That(owner.IsCompleted, Is.False);
                release.SetResult(true);
                await owner;
                Assert.That(io.CollectionCalls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task CancelingTheOwnerDoesNotCacheOrBlockAFollowingListing()
        {
            var entered = Signal();
            var io = new FakeIo { Collections = async token => { if (entered.TrySetResult(true)) await Task.Delay(Timeout.Infinite, token); return Collections(); } };
            var root = io.RootDirectory;
            using (var cancellation = new CancellationTokenSource())
            {
                var owner = root.GetDirectoriesAsync(cancellation.Token);
                await entered.Task;
                var next = root.GetDirectoriesAsync();
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await owner; }));
                Assert.That((await next).Length, Is.EqualTo(1));
                Assert.That(io.CollectionCalls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task FailedLookupDoesNotPoisonTheCache()
        {
            var first = true;
            var io = new FakeIo { Collections = token => { if (first) { first = false; throw new InvalidOperationException("fixture failure"); } return Task.FromResult(Collections()); } };
            Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => { await io.RootDirectory.GetDirectoriesAsync(); }));
            Assert.That((await io.RootDirectory.GetDirectoriesAsync()).Length, Is.EqualTo(1));
            Assert.That(io.CollectionCalls, Is.EqualTo(2));
        }

        [TestCase(false), TestCase(true)]
        public async Task ResetDuringAPendingListingPreventsPublishingItsCache(bool files)
        {
            var entered = Signal();
            var release = Signal();
            var io = new FakeIo();
            var directory = files ? new DirectoryInfo(io, io.RootDirectory, Collections()[0]) : io.RootDirectory;
            io.Collections = async token => { entered.TrySetResult(true); await release.Task; return Collections(); };
            io.Documents = async token => { entered.TrySetResult(true); await release.Task; return Documents(); };
            Task first = files ? (Task)directory.GetFilesAsync() : directory.GetDirectoriesAsync();
            await entered.Task;
            if (files) directory.ResetFilesCache(); else directory.ResetDirectoriesCache();
            release.SetResult(true);
            await first;
            if (files) await directory.GetFilesAsync(); else await directory.GetDirectoriesAsync();
            Assert.That(files ? io.DocumentCalls : io.CollectionCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task TraversalKeepsParentMarkersAndSeparatelyNamedReservedCharacters()
        {
            var io = new FakeIo();
            var directory = await io.RootDirectory.OpenDirectoryPathAsync(new[] { "collection", "nested/name", "." });
            Assert.That(directory.FolderID, Is.EqualTo("folder-id"));
            Assert.That(io.FolderCollection, Is.EqualTo("collection-id"));
            Assert.That(io.FolderParent, Is.EqualTo(CenterDevice.Rest.RestApiConstants.NONE));
            Assert.That(await directory.OpenDirectoryPathAsync(new[] { ".." }), Is.SameAs(directory.ParentDirectory));
            Assert.That(await directory.OpenDirectoryPathAsync(new[] { "/" }), Is.SameAs(io.RootDirectory));
        }

        [Test]
        public async Task FileLookupPreservesLargeSizeAndUsesNoRequestAtRoot()
        {
            var io = new FakeIo();
            Assert.That(await io.RootDirectory.GetFilesAsync(), Is.Empty);
            Assert.That(io.DocumentCalls, Is.Zero);
            var collection = await io.RootDirectory.GetDirectoryAsync("collection");
            var file = await collection.GetFileAsync("fixture.bin");
            Assert.That(file.ID, Is.EqualTo("document-id"));
            Assert.That(file.Size, Is.EqualTo(5L * 1024 * 1024 * 1024));
            Assert.That(file.ParentDirectory, Is.SameAs(collection));
            Assert.That(await collection.TryGetFileAsync("absent"), Is.Null);
            Assert.ThrowsAsync<CenterDevice.Model.Exceptions.FileNotFoundException>((Func<Task>)(async () => { await collection.GetFileAsync("absent"); }));
            Assert.That(io.DocumentCalls, Is.EqualTo(1));
            Assert.That(io.DocumentCollection, Is.EqualTo("collection-id"));
            Assert.That(io.DocumentParent, Is.EqualTo(CenterDevice.Rest.RestApiConstants.NONE));
        }

        private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private static List<Collection> Collections() => new List<Collection> { new Collection { Id = "collection-id", Name = "collection" } };
        private static List<DocumentFullMetadata> Documents() => new List<DocumentFullMetadata> { new DocumentFullMetadata { Id = "document-id", Filename = "fixture.bin", Size = 5L * 1024 * 1024 * 1024 } };
        private sealed class FakeIo : IOClientBase
        {
            internal FakeIo() : base(null, "fixture-user") { }
            internal Func<CancellationToken, Task<List<Collection>>> Collections = token => Task.FromResult(AsyncDirectoryTests.Collections());
            internal Func<CancellationToken, Task<List<DocumentFullMetadata>>> Documents = token => Task.FromResult(AsyncDirectoryTests.Documents());
            internal int CollectionCalls;
            internal int DocumentCalls;
            internal string FolderCollection;
            internal string FolderParent;
            internal string DocumentCollection;
            internal string DocumentParent;
            protected internal override Task<List<Collection>> LookupCollectionsAsync(CancellationToken token) { CollectionCalls++; return Collections(token); }
            protected internal override Task<List<Folder>> LookupChildFoldersAsync(string collectionId, string parentId, CancellationToken token)
            {
                FolderCollection = collectionId;
                FolderParent = parentId;
                return Task.FromResult(new List<Folder> { new Folder { Id = "folder-id", Name = "nested/name", Parent = parentId, Collection = collectionId } });
            }
            protected internal override Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collectionId, string parentId, CancellationToken token)
            {
                DocumentCollection = collectionId;
                DocumentParent = parentId;
                DocumentCalls++;
                return Documents(token);
            }
        }
    }
}
