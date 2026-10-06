using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients.Documents
{
    // Reuses the stream opened for length inspection as the first multipart stream.
    // An explicit token refresh may ask the factory for another stream; uncertain writes are not replayed.
    internal sealed class UploadStreamSource : IDisposable
    {
        private readonly Func<Stream> factory;
        private readonly List<OwnedUploadStream> streams = new List<OwnedUploadStream>();
        private OwnedUploadStream first;
        internal UploadStreamSource(Func<Stream> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            first = Track(factory());
        }
        internal Stream MetadataStream => first;
        internal Stream Open() => Interlocked.Exchange(ref first, null) ?? Track(factory());
        private OwnedUploadStream Track(Stream stream)
        {
            if (stream == null) throw new InvalidOperationException("The upload stream factory returned null.");
            var owned = new OwnedUploadStream(stream);
            lock (streams) streams.Add(owned);
            return owned;
        }
        public void Dispose()
        {
            lock (streams) foreach (var stream in streams) stream.Dispose();
        }

        private sealed class OwnedUploadStream : Stream
        {
            private Stream inner;
            internal OwnedUploadStream(Stream inner) { this.inner = inner; }
            public override bool CanRead => inner?.CanRead ?? false;
            public override bool CanSeek => inner?.CanSeek ?? false;
            public override bool CanWrite => inner?.CanWrite ?? false;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => inner.ReadAsync(buffer, offset, count, token);
            public override void Flush() => inner.Flush();
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) => inner.WriteAsync(buffer, offset, count, token);
            protected override void Dispose(bool disposing)
            {
                if (disposing) Interlocked.Exchange(ref inner, null)?.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
