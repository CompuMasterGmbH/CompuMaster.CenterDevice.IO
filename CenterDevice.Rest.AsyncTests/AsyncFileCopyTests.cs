using CenterDevice.IO;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RemoteFile = CenterDevice.IO.FileInfo;
using RemoteDirectory = CenterDevice.IO.DirectoryInfo;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class AsyncFileCopyTests
    {
        [TestCase(false), TestCase(true)]
        public async Task StagedCopyClosesTheDownloadBeforeUploadAndDeletesStagingAfterSuccessOrFailure(bool failUpload)
        {
            var io = new FakeIo();
            var source = new FakeFile(io, new AsyncReadStream());
            var target = new FakeDirectory(io, source.Stream) { FailUpload = failUpload };
            if (failUpload)
                Assert.ThrowsAsync<IOException>((Func<Task>)(async () => { await target.AddCopyAsync(source, "copy.bin"); }));
            else
                await target.AddCopyAsync(source, "copy.bin");
            Assert.That(source.Stream.Disposed, Is.True);
            // Modern runtimes round the requested copy buffer up to an ArrayPool bucket.
            Assert.That(source.Stream.MaxRequestedRead, Is.LessThanOrEqualTo(128 * 1024));
            Assert.That(target.SourceWasDisposedBeforeUpload, Is.True);
            Assert.That(target.TargetName, Is.EqualTo("copy.bin"));
            Assert.That(target.UploadLength, Is.EqualTo(200000));
            Assert.That(System.IO.File.Exists(target.StagingPath), Is.False);
        }

        [Test]
        public async Task CancellationDuringStagingClosesTheSourceAndDoesNotStartUpload()
        {
            var io = new FakeIo();
            var stream = new AsyncReadStream { Block = true };
            var source = new FakeFile(io, stream);
            var target = new FakeDirectory(io, stream);
            using (var cancellation = new CancellationTokenSource())
            {
                var operation = target.AddCopyAsync(source, null, cancellation.Token);
                await stream.Entered.Task;
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await operation; }));
                Assert.That(stream.Disposed, Is.True);
                Assert.That(target.StagingPath, Is.Null);
            }
        }

        private sealed class FakeIo : IOClientBase
        {
            internal FakeIo() : base(null, "fixture-user") { }
            protected internal override Task<List<DocumentFullMetadata>> LookupChildDocumentsAsync(string collectionId, string folderId, CancellationToken token) =>
                Task.FromResult(new List<DocumentFullMetadata>());
        }
        private sealed class FakeFile : RemoteFile
        {
            internal readonly AsyncReadStream Stream;
            internal FakeFile(IOClientBase io, AsyncReadStream stream) : base(io, null, new DocumentFullMetadata { Id = "fixture-document", Filename = "source.bin", Size = 200000 }) { Stream = stream; }
            public override Task<Stream> DownloadAsync(long version = 0, CancellationToken cancellationToken = default(CancellationToken)) => Task.FromResult<Stream>(Stream);
        }
        private sealed class FakeDirectory : RemoteDirectory
        {
            private readonly AsyncReadStream source;
            internal bool FailUpload;
            internal bool SourceWasDisposedBeforeUpload;
            internal string StagingPath;
            internal string TargetName;
            internal long UploadLength;
            internal FakeDirectory(IOClientBase io, AsyncReadStream source) : base(io, io.RootDirectory, new Collection { Id = "target-id", Name = "target" }) { this.source = source; }
            public override Task UploadAndCreateNewFileAsync(Func<Stream> factory, string fileName, CancellationToken token = default(CancellationToken))
            {
                SourceWasDisposedBeforeUpload = source.Disposed;
                TargetName = fileName;
                using (var staged = factory())
                {
                    StagingPath = ((FileStream)staged).Name;
                    UploadLength = staged.Length;
                }
                if (FailUpload) throw new IOException("fixture upload failure");
                return Task.CompletedTask;
            }
        }
        private sealed class AsyncReadStream : Stream
        {
            private long position;
            internal bool Block;
            internal bool Disposed;
            internal int MaxRequestedRead;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public override bool CanRead => !Disposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 200000;
            public override long Position { get => position; set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous read is forbidden in this fixture.");
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                MaxRequestedRead = Math.Max(MaxRequestedRead, count);
                Entered.TrySetResult(true);
                if (Block) await Task.Delay(Timeout.Infinite, token);
                token.ThrowIfCancellationRequested();
                var read = (int)Math.Min(count, Length - position);
                Array.Clear(buffer, offset, read);
                position += read;
                return read;
            }
            protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
