using CenterDevice.IO;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Folders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class AsyncDirectoryMutationTests
    {
        [TestCase(false), TestCase(true)]
        public async Task RenameChangesTheNameOnlyOnSuccessAndInvalidatesTheParentAfterEitherOutcome(bool fail)
        {
            var io = new FakeIo();
            var root = io.RootDirectory;
            await root.GetDirectoriesAsync();
            var directory = new FakeDirectory(io, root, new Collection { Id = "source", Name = "old" }) { Fail = fail };
            using (var cancellation = new CancellationTokenSource())
            {
                if (fail) Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => { await directory.RenameAsync("new", cancellation.Token); }));
                else await directory.RenameAsync("new", cancellation.Token);
                Assert.That(directory.LastToken, Is.EqualTo(cancellation.Token));
            }
            Assert.That(directory.Name, Is.EqualTo(fail ? "old" : "new"));
            await root.GetDirectoriesAsync();
            Assert.That(io.RootCalls, Is.EqualTo(2));
        }

        [TestCase(false), TestCase(true)]
        public async Task FolderMoveUpdatesMetadataOnlyOnSuccessAndInvalidatesBothParents(bool fail)
        {
            var io = new FakeIo();
            var sourceParent = new DirectoryInfo(io, io.RootDirectory, new Collection { Id = "source", Name = "source" });
            var targetParent = new DirectoryInfo(io, io.RootDirectory, new Collection { Id = "target", Name = "target" });
            await sourceParent.GetDirectoriesAsync();
            await targetParent.GetDirectoriesAsync();
            var metadata = new Folder { Id = "folder", Name = "child", Collection = "source", Parent = "old-parent" };
            var directory = new FakeDirectory(io, sourceParent, metadata) { Fail = fail };
            if (fail) Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => { await directory.MoveAsync(targetParent); }));
            else await directory.MoveAsync(targetParent);
            Assert.That(directory.ParentDirectory, Is.SameAs(fail ? sourceParent : targetParent));
            Assert.That(metadata.Collection, Is.EqualTo(fail ? "source" : "target"));
            Assert.That(metadata.Parent, Is.EqualTo(fail ? "old-parent" : null));
            Assert.That(directory.TargetCollection, Is.EqualTo("target"));
            Assert.That(directory.TargetFolder, Is.Null);
            await sourceParent.GetDirectoriesAsync();
            await targetParent.GetDirectoriesAsync();
            Assert.That(io.FolderCalls, Is.EqualTo(4));
        }

        [TestCase(false), TestCase(true)]
        public async Task ChildCreationInvalidatesTheCurrentListingAfterSuccessOrUncertainFailure(bool fail)
        {
            var io = new FakeIo();
            var directory = new FakeDirectory(io, io.RootDirectory, new Collection { Id = "collection", Name = "collection" }) { Fail = fail };
            await directory.GetDirectoriesAsync();
            if (fail) Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => { await directory.CreateDirectoryAsync("new-folder"); }));
            else await directory.CreateDirectoryAsync("new-folder");
            Assert.That(directory.CreatedStyle, Is.EqualTo(DirectoryInfo.DirectoryType.Folder));
            await directory.GetDirectoriesAsync();
            Assert.That(io.FolderCalls, Is.EqualTo(2));
        }

        [TestCase(false), TestCase(true)]
        public async Task SharingPreservesNullAndExplicitIdentifiersAndInvalidatesTheParent(bool remove)
        {
            var io = new FakeIo();
            await io.RootDirectory.GetDirectoriesAsync();
            var directory = new FakeDirectory(io, io.RootDirectory, new Collection { Id = "collection", Name = "collection" });
            var groups = new[] { "explicit-group" };
            if (remove) await directory.RemoveSharingAsync(null, groups);
            else await directory.AddSharingAsync(null, groups);
            Assert.That(directory.SharedUsers, Is.Null);
            Assert.That(directory.SharedGroups, Is.SameAs(groups));
            Assert.That(directory.RemovedSharing, Is.EqualTo(remove));
            await io.RootDirectory.GetDirectoriesAsync();
            Assert.That(io.RootCalls, Is.EqualTo(2));
        }

        [Test]
        public void UnsupportedRootMutationsRejectBeforeAnyRemoteCallback()
        {
            var io = new FakeIo();
            var root = new FakeDirectory(io);
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await root.DeleteAsync(); }));
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await root.RenameAsync("x"); }));
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await root.AddSharingAsync(null, null); }));
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await root.MoveAsync(root); }));
            Assert.That(root.CoreCalls, Is.Zero);
        }

        private sealed class FakeIo : IOClientBase
        {
            internal FakeIo() : base(null, "fixture") { }
            internal int RootCalls;
            internal int FolderCalls;
            protected internal override Task<List<Collection>> LookupCollectionsAsync(CancellationToken token) { RootCalls++; return Task.FromResult(new List<Collection>()); }
            protected internal override Task<List<Folder>> LookupChildFoldersAsync(string collectionId, string parentId, CancellationToken token) { FolderCalls++; return Task.FromResult(new List<Folder>()); }
        }
        private sealed class FakeDirectory : DirectoryInfo
        {
            internal FakeDirectory(IOClientBase io) : base(io) { }
            internal FakeDirectory(IOClientBase io, DirectoryInfo parent, Collection collection) : base(io, parent, collection) { }
            internal FakeDirectory(IOClientBase io, DirectoryInfo parent, Folder folder) : base(io, parent, folder) { }
            internal bool Fail;
            internal int CoreCalls;
            internal CancellationToken LastToken;
            internal DirectoryType CreatedStyle;
            internal string TargetFolder;
            internal string TargetCollection;
            internal string[] SharedUsers;
            internal string[] SharedGroups;
            internal bool RemovedSharing;
            private Task Complete(CancellationToken token) { CoreCalls++; LastToken = token; if (Fail) throw new InvalidOperationException("Uncertain fixture mutation failure."); return Task.CompletedTask; }
            protected override Task CreateDirectoryCoreAsync(string name, DirectoryType style, CancellationToken token) { CreatedStyle = style; return Complete(token); }
            protected override Task DeleteCoreAsync(CancellationToken token) => Complete(token);
            protected override Task RenameCoreAsync(string name, CancellationToken token) => Complete(token);
            protected override Task MoveCoreAsync(string folder, string collection, CancellationToken token) { TargetFolder = folder; TargetCollection = collection; return Complete(token); }
            protected override Task ChangeSharingCoreAsync(string[] users, string[] groups, bool remove, CancellationToken token) { SharedUsers = users; SharedGroups = groups; RemovedSharing = remove; return Complete(token); }
        }
    }
}
