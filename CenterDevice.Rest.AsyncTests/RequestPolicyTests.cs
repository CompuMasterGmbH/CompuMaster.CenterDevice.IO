using CenterDevice.Rest.Clients;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class RequestPolicyTests
    {
        [Test]
        public async Task RetryAfterOnAMutationDefersOtherRequestsWithoutReplayingTheMutation()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            var transport = new FakeHandler((request, token) =>
            {
                var response = Response(request.Method == HttpMethod.Post ? (HttpStatusCode)429 : HttpStatusCode.OK);
                if (request.Method == HttpMethod.Post) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(4));
                return Task.FromResult(response);
            });
            using (var client = Client(transport, clock, gate))
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/write"))
                using (await client.SendAsync(request)) { }
                Assert.That(transport.Calls, Is.EqualTo(1));
                using (await client.GetAsync("https://example.test/read")) { }
                Assert.That(clock.Delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(4) }));
                Assert.That(transport.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task SeparateClientsShareTheOriginAllowanceUntilTheResponseBodyIsConsumed()
        {
            var origin = "https://" + Guid.NewGuid().ToString("N") + ".test/";
            var firstHandler = new FakeHandler((request, token) => Task.FromResult(Response(HttpStatusCode.OK)));
            var secondHandler = new FakeHandler((request, token) => Task.FromResult(Response(HttpStatusCode.OK)));
            using (var first = new HttpClient(new CenterDeviceHttpMessageHandler(firstHandler)))
            using (var second = new HttpClient(new CenterDeviceHttpMessageHandler(secondHandler)))
            using (var response = await first.GetAsync(origin, HttpCompletionOption.ResponseHeadersRead))
            using (var cancellation = new CancellationTokenSource())
            {
                var queued = second.GetAsync(origin + "queued", HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
                Assert.That(secondHandler.Calls, Is.Zero);
                cancellation.Cancel();
                Assert.ThrowsAsync<TaskCanceledException>((Func<Task>)(async () => { await queued; }));
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[32];
                    Assert.That(await stream.ReadAsync(buffer, 0, buffer.Length), Is.GreaterThan(0));
                    Assert.That(await stream.ReadAsync(buffer, 0, buffer.Length), Is.Zero);
                    using (await second.GetAsync(origin + "after-eof")) { }
                    Assert.That(secondHandler.Calls, Is.EqualTo(1));
                }
            }
        }

        [Test]
        public async Task DisposingAnUnreadResponseReleasesTheSharedAllowance()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            var transport = new FakeHandler((request, token) => Task.FromResult(Response(HttpStatusCode.OK)));
            using (var client = Client(transport, clock, gate))
            {
                var first = await client.GetAsync("https://example.test/first", HttpCompletionOption.ResponseHeadersRead);
                var second = client.GetAsync("https://example.test/second", HttpCompletionOption.ResponseHeadersRead);
                Assert.That(second.IsCompleted, Is.False);
                first.Dispose();
                using (await second) { }
                Assert.That(transport.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task CancellationReachesAnActiveHttpRequestAndReleasesItsAllowance()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var transport = new FakeHandler(async (request, token) =>
            {
                if (request.RequestUri.AbsolutePath == "/cancel")
                {
                    entered.SetResult(true);
                    await Task.Delay(Timeout.Infinite, token);
                }
                return Response(HttpStatusCode.OK);
            });
            using (var client = Client(transport, clock, gate))
            using (var cancellation = new CancellationTokenSource())
            {
                var active = client.GetAsync("https://example.test/cancel", cancellation.Token);
                await entered.Task;
                cancellation.Cancel();
                Assert.ThrowsAsync<TaskCanceledException>((Func<Task>)(async () => { await active; }));
                using (await client.GetAsync("https://example.test/after")) { }
                Assert.That(transport.Calls, Is.EqualTo(2));
            }
        }

        [Test]
        public async Task RateAdmissionUsesASeparateRollingWindow()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            for (var i = 0; i < 30; i++) using (await gate.EnterAsync(CancellationToken.None)) { }
            using (await gate.EnterAsync(CancellationToken.None)) { }
            Assert.That(clock.Delays, Is.EqualTo(new[] { TimeSpan.FromMinutes(1) }));
        }

        [TestCase(false), TestCase(true)]
        public async Task ReadRetriesHonorRetryAfterSecondsAndDate(bool date)
        {
            var clock = new FakeClock();
            var transport = new FakeHandler((request, token) =>
            {
                var response = Response(HttpStatusCode.ServiceUnavailable);
                response.Headers.RetryAfter = date
                    ? new RetryConditionHeaderValue(clock.UtcNow.AddSeconds(2))
                    : new RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
                return Task.FromResult(response);
            });
            using (var client = Client(transport, clock, new RequestAllowance(clock)))
            using (var response = await client.GetAsync("https://example.test/read"))
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("diagnostic"));
                Assert.That(transport.Calls, Is.EqualTo(3));
                Assert.That(clock.Delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2) }));
            }
        }

        [TestCase("POST"), TestCase("PUT"), TestCase("DELETE")]
        public async Task MutationsAreNeverReplayedAfterAnAmbiguousServerError(string method)
        {
            var clock = new FakeClock();
            var transport = new FakeHandler((request, token) => Task.FromResult(Response(HttpStatusCode.BadGateway)));
            using (var client = Client(transport, clock, new RequestAllowance(clock)))
            using (var request = new HttpRequestMessage(new HttpMethod(method), "https://example.test/write"))
            using (var response = await client.SendAsync(request))
            {
                Assert.That(transport.Calls, Is.EqualTo(1));
                Assert.That(clock.Delays, Is.Empty);
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("diagnostic"));
            }
        }

        [TestCase(401), TestCase(403), TestCase(404)]
        public async Task PermanentReadErrorsAreNotRetried(int status)
        {
            var clock = new FakeClock();
            var transport = new FakeHandler((request, token) => Task.FromResult(Response((HttpStatusCode)status)));
            using (var client = Client(transport, clock, new RequestAllowance(clock)))
            using (await client.GetAsync("https://example.test/read")) { }
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(clock.Delays, Is.Empty);
        }

        [Test]
        public async Task RetryBudgetRetainsFinalDiagnosticsWhenRateAdmissionTakesTooLong()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            for (var i = 0; i < 29; i++) using (await gate.EnterAsync(CancellationToken.None)) { }
            var transport = new FakeHandler((request, token) => Task.FromResult(Response(HttpStatusCode.ServiceUnavailable)));
            using (var client = Client(transport, clock, gate))
            using (var response = await client.GetAsync("https://example.test/read"))
            {
                Assert.That(transport.Calls, Is.EqualTo(1));
                Assert.That(await response.Content.ReadAsStringAsync(), Is.EqualTo("diagnostic"));
                Assert.That(clock.UtcNow, Is.EqualTo(clock.Initial.AddMinutes(1)));
            }
        }

        private static HttpClient Client(FakeHandler transport, FakeClock clock, RequestAllowance gate)
            => new HttpClient(new CenterDeviceHttpMessageHandler(transport, clock, uri => gate));

        private static HttpResponseMessage Response(HttpStatusCode status)
            => new HttpResponseMessage(status) { Content = new StringContent("diagnostic") };

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
            internal int Calls;
            internal FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref Calls);
                return send(request, cancellationToken);
            }
        }

        private sealed class FakeClock : IRequestClock
        {
            internal readonly DateTimeOffset Initial = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
            internal readonly List<TimeSpan> Delays = new List<TimeSpan>();
            public DateTimeOffset UtcNow { get; private set; }
            internal FakeClock() { UtcNow = Initial; }
            public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Delays.Add(delay);
                UtcNow += delay;
                return Task.CompletedTask;
            }
        }
    }
}
