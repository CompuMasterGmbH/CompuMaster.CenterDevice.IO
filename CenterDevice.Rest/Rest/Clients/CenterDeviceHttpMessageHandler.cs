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
        private readonly bool retryReads;

        internal CenterDeviceHttpMessageHandler(HttpMessageHandler inner)
            : this(inner, true) { }

        internal CenterDeviceHttpMessageHandler(HttpMessageHandler inner, bool retryReads)
            : this(inner, new SystemRequestClock(), GetAllowance, retryReads) { }

        internal CenterDeviceHttpMessageHandler(HttpMessageHandler inner, IRequestClock clock, Func<Uri, RequestAllowance> allowance)
            : this(inner, clock, allowance, true) { }

        internal CenterDeviceHttpMessageHandler(HttpMessageHandler inner, IRequestClock clock, Func<Uri, RequestAllowance> allowance, bool retryReads)
            : base(inner)
        {
            this.clock = clock;
            this.allowance = allowance;
            this.retryReads = retryReads;
        }

        internal static RequestAllowance GetAllowance(Uri serviceUri)
        {
            if (serviceUri == null) throw new ArgumentNullException(nameof(serviceUri));
            if (!serviceUri.IsAbsoluteUri || (serviceUri.Scheme != Uri.UriSchemeHttp && serviceUri.Scheme != Uri.UriSchemeHttps))
                throw new ArgumentException("An absolute HTTP or HTTPS service URI is required.", nameof(serviceUri));
            var origin = serviceUri.Scheme + Uri.SchemeDelimiter + serviceUri.IdnHost + ":" + serviceUri.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Allowances.GetOrAdd(origin, key => new RequestAllowance(new SystemRequestClock()));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage previous = null;
            var started = clock.UtcNow;
            var read = retryReads && (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head);
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
                TimeSpan? overloadDelay = null;
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
                        overloadDelay = RetryDelay(response, attempt);
                        requestAllowance.DeferUntil(clock.UtcNow + overloadDelay.Value);
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
                var delay = overloadDelay ?? RetryDelay(response, attempt);
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
        private readonly SemaphoreSlim concurrent;
        private readonly Queue<DateTimeOffset> starts = new Queue<DateTimeOffset>();
        private readonly IRequestClock clock;
        private int? maxRequestsPerMinute;
        private bool started;
        private DateTimeOffset resumeAt;

        internal RequestAllowance(IRequestClock clock, int maxConcurrent = 3, int? maxRequestsPerMinute = null)
        {
            if (maxConcurrent < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrent));
            if (maxRequestsPerMinute.HasValue && maxRequestsPerMinute.Value < 1) throw new ArgumentOutOfRangeException(nameof(maxRequestsPerMinute));
            this.clock = clock;
            this.maxRequestsPerMinute = maxRequestsPerMinute;
            concurrent = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        internal void ConfigureRateLimit(int? value)
        {
            if (value.HasValue && value.Value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            lock (starts)
            {
                if (started) throw new InvalidOperationException("Configure the rate limit before the first request to this origin.");
                maxRequestsPerMinute = value;
            }
        }

        internal void DeferUntil(DateTimeOffset resumeAt)
        {
            lock (starts) if (resumeAt > this.resumeAt) this.resumeAt = resumeAt;
        }

        internal async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (starts) started = true;
            await concurrent.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TimeSpan delay;
                    lock (starts)
                    {
                        var now = clock.UtcNow;
                        delay = resumeAt - now;
                        if (delay <= TimeSpan.Zero)
                        {
                            if (!maxRequestsPerMinute.HasValue) return new Slot(concurrent);
                            while (starts.Count > 0 && now - starts.Peek() >= TimeSpan.FromMinutes(1)) starts.Dequeue();
                            if (starts.Count < maxRequestsPerMinute.Value)
                            {
                                starts.Enqueue(now);
                                return new Slot(concurrent);
                            }
                            delay = starts.Peek() + TimeSpan.FromMinutes(1) - now;
                        }
                    }
                    await clock.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
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
