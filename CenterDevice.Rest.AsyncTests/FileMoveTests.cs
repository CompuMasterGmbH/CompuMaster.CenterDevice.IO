using CenterDevice.IO;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.Folders;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Directory = CenterDevice.IO.DirectoryInfo;
using File = CenterDevice.IO.FileInfo;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class FileMoveTests
    {
        [Test, Combinatorial]
        public async Task MoveRetainsIdentityInvalidatesBothCachesAndPublishesParentOnlyOnSuccess(
            [Values(false, true)] bool sync, [Values(false, true)] bool sourceFolder, [Values(false, true)] bool targetFolder, [Values(false, true)] bool fail)
        {
            var io = new FakeIo();
            var sourceCollection = new Directory(io, io.RootDirectory, new Collection { Id = "source-collection" });
            var targetCollection = new Directory(io, io.RootDirectory, new Collection { Id = "target-collection" });
            var source = sourceFolder ? new Directory(io, sourceCollection, new Folder { Id = "source-folder" }) : sourceCollection;
            var target = targetFolder ? new Directory(io, targetCollection, new Folder { Id = "target-folder" }) : targetCollection;
            await source.GetFilesAsync();
            await target.GetFilesAsync();
            var file = new FakeFile(io, source) { Failure = fail ? new IOException("isolated uncertain move") : null };
            if (fail)
            {
                var error = sync ? Assert.Throws<IOException>((Action)(() => file.Move(target)))
                    : Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await file.MoveAsync(target); }));
                Assert.That(error, Is.SameAs(file.Failure));
            }
            else if (sync) file.Move(target);
            else await file.MoveAsync(target);
            Assert.That(file.ParentDirectory, Is.SameAs(fail ? source : target));
            Assert.That(file.ID, Is.EqualTo("original-document"));
            Assert.That(file.SourceCollection, Is.EqualTo("source-collection"));
            Assert.That(file.TargetCollection, Is.EqualTo("target-collection"));
            Assert.That(file.SourceFolder, Is.EqualTo(sourceFolder ? "source-folder" : null));
            Assert.That(file.TargetFolder, Is.EqualTo(targetFolder ? "target-folder" : null));
            Assert.That(file.Calls, Is.EqualTo(1));
            await source.GetFilesAsync();
            await target.GetFilesAsync();
            Assert.That(io.FileLookups, Is.EqualTo(4));
        }

        [Test]
        public void DifferentClientTargetIsRejectedBeforeMutation()
        {
            var sourceIo = new FakeIo();
            var targetIo = new FakeIo();
            var source = new Directory(sourceIo, sourceIo.RootDirectory, new Collection { Id = "source" });
            var target = new Directory(targetIo, targetIo.RootDirectory, new Collection { Id = "target" });
            var file = new FakeFile(sourceIo, source);
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await file.MoveAsync(target); }));
            Assert.That(file.Calls, Is.Zero);
            Assert.That(file.ParentDirectory, Is.SameAs(source));
        }

        [Test]
        public void RootTargetIsRejectedBeforeMutation()
        {
            var io = new FakeIo();
            var source = new Directory(io, io.RootDirectory, new Collection { Id = "source" });
            var file = new FakeFile(io, source);
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await file.MoveAsync(io.RootDirectory); }));
            Assert.That(file.Calls, Is.Zero);
        }

        [Test]
        public void PreCancellationDoesNotEnterTheMove()
        {
            var io = new FakeIo();
            var source = new Directory(io, io.RootDirectory, new Collection { Id = "source" });
            var target = new Directory(io, io.RootDirectory, new Collection { Id = "target" });
            var file = new FakeFile(io, source);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await file.MoveAsync(target, cancellation.Token); }));
            }
            Assert.That(file.Calls, Is.Zero);
            Assert.That(file.ParentDirectory, Is.SameAs(source));
        }

        [Test]
        public async Task ActiveCancellationInvalidatesBothListingsWithoutPublishingANewParent()
        {
            var io = new FakeIo();
            var source = new Directory(io, io.RootDirectory, new Collection { Id = "source" });
            var target = new Directory(io, io.RootDirectory, new Collection { Id = "target" });
            await source.GetFilesAsync(); await target.GetFilesAsync();
            var file = new FakeFile(io, source) { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                var task = file.MoveAsync(target, cancellation.Token);
                await file.Entered.Task;
                Assert.That(file.Token, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await task; }));
            }
            Assert.That(file.ParentDirectory, Is.SameAs(source));
            Assert.That(file.ID, Is.EqualTo("original-document"));
            await source.GetFilesAsync(); await target.GetFilesAsync();
            Assert.That(io.FileLookups, Is.EqualTo(4));
            Assert.That(file.Calls, Is.EqualTo(1));
        }

        private sealed class FakeIo : IOClientBase
        {
            internal int FileLookups;
            internal FakeIo() : base(null, "fixture") { }
            protected internal override Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collection, string folder, CancellationToken token)
            { FileLookups++; return Task.FromResult(new List<DocumentFullMetadata>()); }
        }
        private sealed class FakeFile : File
        {
            internal int Calls;
            internal string SourceCollection, SourceFolder, TargetCollection, TargetFolder;
            internal IOException Failure;
            internal bool Pending;
            internal CancellationToken Token;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal FakeFile(FakeIo io, Directory parent) : base(io, parent, new DocumentFullMetadata { Id = "original-document", Filename = "original.txt" }) { }
            protected override async Task MoveCoreAsync(Directory target, CancellationToken token)
            {
                Calls++; Token = token;
                SourceCollection = ParentDirectory.AssociatedCollection.CollectionID; SourceFolder = ParentDirectory.FolderID;
                TargetCollection = target.AssociatedCollection.CollectionID; TargetFolder = target.FolderID;
                Entered.TrySetResult(true);
                if (Failure != null) throw Failure;
                if (Pending) await Task.Delay(Timeout.Infinite, token);
            }
        }
    }
}
