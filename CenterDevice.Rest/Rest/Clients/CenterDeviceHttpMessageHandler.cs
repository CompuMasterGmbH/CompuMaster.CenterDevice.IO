using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.Clients
{
    // One transport allowance per origin, including calls from separate REST client instances.
    internal sealed class CenterDeviceHttpMessageHandler : DelegatingHandler
    {
        private static readonly ConcurrentDictionary<string, RequestAllowance> Allowances =
            new ConcurrentDictionary<string, RequestAllowance>(StringComparer.OrdinalIgnoreCase);
        private readonly IRequestClock clock;
        private readonly Func<Uri, RequestAllowance> allowance;

        internal CenterDeviceHttpMessageHandler(HttpMessageHandler inner)
            : this(inner, new SystemRequestClock(), uri => Allowances.GetOrAdd(
                uri.GetLeftPart(UriPartial.Authority), key => new RequestAllowance(new SystemRequestClock()))) { }

        internal CenterDeviceHttpMessageHandler(HttpMessageHandler inner, IRequestClock clock, Func<Uri, RequestAllowance> allowance)
            : base(inner)
        {
            this.clock = clock;
            this.allowance = allowance;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage previous = null;
            var started = clock.UtcNow;
            var read = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
            for (var attempt = 0; ; attempt++)
            {
                var requestAllowance = allowance(request.RequestUri);
                IDisposable slot;
                try { slot = await requestAllowance.EnterAsync(cancellationToken).ConfigureAwait(false); }
                catch { previous?.Dispose(); throw; }
                if (attempt > 0 && clock.UtcNow - started >= TimeSpan.FromSeconds(30))
                {
                    slot.Dispose();
                    return previous;
                }
                HttpResponseMessage response;
                HttpRequestMessage retryRequest = null;
                try
                {
                    if (attempt > 0)
                    {
                        retryRequest = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
                        foreach (var header in request.Headers) retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    var sending = retryRequest ?? request;
                    var originalContent = sending.Content;
                    using (var ordered = MetadataFirstMultipartContent.CreateIfNeeded(sending))
                    {
                        if (ordered != null) sending.Content = ordered;
                        try { response = await base.SendAsync(sending, cancellationToken).ConfigureAwait(false); }
                        finally { if (ordered != null) sending.Content = originalContent; }
                    }
                    if ((int)response.StatusCode == 429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        var after = response.Headers.RetryAfter;
                        if (after?.Delta != null) requestAllowance.DeferUntil(clock.UtcNow + after.Delta.Value);
                        else if (after?.Date != null) requestAllowance.DeferUntil(after.Date.Value);
                    }
                    if (response.Content == null) slot.Dispose();
                    else response.Content = new LeasedContent(response.Content, slot);
                    slot = null;
                }
                catch
                {
                    slot?.Dispose();
                    previous?.Dispose();
                    throw;
                }
                finally { retryRequest?.Dispose(); }

                previous?.Dispose();
                previous = null;

                if (!read || attempt >= 2 || !IsTransient(response.StatusCode)) return response;
                var delay = RetryDelay(response, attempt);
                if (clock.UtcNow - started + delay >= TimeSpan.FromSeconds(30)) return response;
                // Release the request allowance before waiting. Keep the final response for diagnostics
                // if rate admission consumes the remaining retry budget.
                previous = response;
                try
                {
                    if (response.Content != null) await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                    await clock.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
                }
                catch { response.Dispose(); throw; }
            }
        }

        private static bool IsTransient(HttpStatusCode status)
        {
            var value = (int)status;
            return value == 408 || value == 429 || value == 500 || value == 502 || value == 503 || value == 504;
        }

        private TimeSpan RetryDelay(HttpResponseMessage response, int attempt)
        {
            var after = response.Headers.RetryAfter;
            if (after?.Delta != null) return after.Delta.Value < TimeSpan.Zero ? TimeSpan.Zero : after.Delta.Value;
            if (after?.Date != null)
            {
                var delay = after.Date.Value - clock.UtcNow;
                return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
            }
            // Bounded jitter prevents synchronized read retries across unrelated processes.
            return TimeSpan.FromMilliseconds((1 << attempt) * 250 + (Guid.NewGuid().GetHashCode() & 127));
        }

        private sealed class LeasedContent : HttpContent
        {
            private readonly HttpContent inner;
            private IDisposable slot;

            internal LeasedContent(HttpContent inner, IDisposable slot)
            {
                this.inner = inner;
                this.slot = slot;
                foreach (var header in inner.Headers) Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                try { await inner.CopyToAsync(stream).ConfigureAwait(false); }
                finally { Release(); }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = inner.Headers.ContentLength ?? 0;
                return inner.Headers.ContentLength.HasValue;
            }

            protected override async Task<Stream> CreateContentReadStreamAsync()
            {
                try { return new AllowanceStream(await inner.ReadAsStreamAsync().ConfigureAwait(false), Release); }
                catch { Release(); throw; }
            }

            private void Release() { Interlocked.Exchange(ref slot, null)?.Dispose(); }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { inner.Dispose(); }
                    finally { Release(); }
                }
                base.Dispose(disposing);
            }
        }
    }

    internal interface IRequestClock
    {
        DateTimeOffset UtcNow { get; }
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    internal sealed class SystemRequestClock : IRequestClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.Delay(delay, cancellationToken);
    }

    internal sealed class RequestAllowance
    {
        private readonly SemaphoreSlim concurrent = new SemaphoreSlim(1, 1);
        private readonly Queue<DateTimeOffset> starts = new Queue<DateTimeOffset>();
        private readonly IRequestClock clock;
        private long resumeAtTicks;

        internal RequestAllowance(IRequestClock clock) { this.clock = clock; }

        internal void DeferUntil(DateTimeOffset resumeAt)
        {
            var next = resumeAt.UtcTicks;
            long previous;
            do
            {
                previous = Interlocked.Read(ref resumeAtTicks);
                if (next <= previous) return;
            } while (Interlocked.CompareExchange(ref resumeAtTicks, next, previous) != previous);
        }

        internal async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
        {
            await concurrent.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var now = clock.UtcNow;
                    var cooldown = new DateTimeOffset(Interlocked.Read(ref resumeAtTicks), TimeSpan.Zero);
                    if (cooldown > now)
                    {
                        await clock.DelayAsync(cooldown - now, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    while (starts.Count > 0 && now - starts.Peek() >= TimeSpan.FromMinutes(1)) starts.Dequeue();
                    if (starts.Count < 30)
                    {
                        starts.Enqueue(now);
                        return new Slot(concurrent);
                    }
                    await clock.DelayAsync(starts.Peek() + TimeSpan.FromMinutes(1) - now, cancellationToken).ConfigureAwait(false);
                }
            }
            catch { concurrent.Release(); throw; }
        }

        private sealed class Slot : IDisposable
        {
            private SemaphoreSlim semaphore;
            internal Slot(SemaphoreSlim semaphore) { this.semaphore = semaphore; }
            public void Dispose() { Interlocked.Exchange(ref semaphore, null)?.Release(); }
        }
    }

    internal sealed class AllowanceStream : Stream
    {
        private readonly Stream inner;
        private Action release;
        internal AllowanceStream(Stream inner, Action release) { this.inner = inner; this.release = release; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count)
        {
            try { var read = inner.Read(buffer, offset, count); if (read == 0 && count > 0) Release(); return read; }
            catch { Release(); throw; }
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                var read = await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (read == 0 && count > 0) Release();
                return read;
            }
            catch { Release(); throw; }
        }
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.WriteAsync(buffer, offset, count, cancellationToken);
        private void Release() { Interlocked.Exchange(ref release, null)?.Invoke(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { inner.Dispose(); }
                finally { Release(); }
            }
            base.Dispose(disposing);
        }
    }
}
