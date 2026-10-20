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
        public async Task DefaultAdmissionDoesNotImposeAMinuteQuota()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            for (var i = 0; i < 65; i++) using (await gate.EnterAsync(CancellationToken.None)) { }
            Assert.That(clock.Delays, Is.Empty);
        }

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
            using (var secondResponse = await first.GetAsync(origin + "second", HttpCompletionOption.ResponseHeadersRead))
            using (var thirdResponse = await first.GetAsync(origin + "third", HttpCompletionOption.ResponseHeadersRead))
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
            var gate = new RequestAllowance(clock, maxConcurrent: 1);
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
            var gate = new RequestAllowance(clock, maxRequestsPerMinute: 30);
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
            var gate = new RequestAllowance(clock, maxRequestsPerMinute: 30);
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

        [TestCase(429, false), TestCase(503, true)]
        public async Task QueuedRequestsRecheckSharedCooldownAfterAWriteResponse(int status, bool date)
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            var transport = new FakeHandler((request, token) =>
            {
                var response = Response((HttpStatusCode)status);
                response.Headers.RetryAfter = date ? new RetryConditionHeaderValue(clock.UtcNow.AddSeconds(7)) : new RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
                return Task.FromResult(response);
            });
            using (var one = await gate.EnterAsync(CancellationToken.None))
            using (var two = await gate.EnterAsync(CancellationToken.None))
            using (var client = Client(transport, clock, gate))
            using (var write = new HttpRequestMessage(HttpMethod.Post, "https://example.test/write") { Content = new StringContent("data") })
            using (var response = await client.SendAsync(write, HttpCompletionOption.ResponseHeadersRead))
            {
                var queued = gate.EnterAsync(CancellationToken.None);
                Assert.That(queued.IsCompleted, Is.False);
                response.Dispose();
                using (await queued) { }
                Assert.That(clock.Delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(7) }));
                Assert.That(transport.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(429), TestCase(503)]
        public async Task OverloadWithoutRetryAfterStillDefersOtherRequests(int status)
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            var transport = new FakeHandler((request, token) => Task.FromResult(Response((HttpStatusCode)status)));
            using (var client = Client(transport, clock, gate))
            {
                using (await client.PostAsync("https://example.test/write", new StringContent("data"))) { }
                using (await gate.EnterAsync(CancellationToken.None)) { }
                Assert.That(clock.Delays.Count, Is.EqualTo(1));
                Assert.That(clock.Delays[0], Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250)));
                Assert.That(transport.Calls, Is.EqualTo(1));
            }
        }

        [Test]
        public async Task PublicRateConfigurationIsSharedAndCannotChangeAfterRequestsStart()
        {
            var origin = new Uri("https://" + Guid.NewGuid().ToString("N") + ".test/path");
            CenterDeviceHttpTransport.ConfigureRequestRateLimit(origin, 1);
            var gate = CenterDeviceHttpMessageHandler.GetAllowance(new Uri(origin, "/different-path"));
            var transport = new FakeHandler((request, token) => Task.FromResult(Response(HttpStatusCode.OK)));
            using (var first = CenterDeviceHttpTransport.CreateHttpClient(transport))
            using (await first.GetAsync(origin)) { }
            Assert.Throws<InvalidOperationException>((Action)(() => CenterDeviceHttpTransport.ConfigureRequestRateLimit(origin, null)));
            using (var cancellation = new CancellationTokenSource())
            {
                var queued = gate.EnterAsync(cancellation.Token);
                Assert.That(queued.IsCompleted, Is.False);
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { using (await queued) { } }));
            }
        }

        [TestCase(0), TestCase(-1)]
        public void RejectsInvalidPublicRateLimit(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>((Action)(() => CenterDeviceHttpTransport.ConfigureRequestRateLimit(new Uri("https://invalid-limit.test"), value)));
        }

        [Test]
        public async Task ExplicitlyDisabledRateLimitDoesNotWaitAndInvalidOriginsAreRejected()
        {
            Assert.Throws<ArgumentNullException>((Action)(() => CenterDeviceHttpTransport.ConfigureRequestRateLimit(null, 1)));
            Assert.Throws<ArgumentException>((Action)(() => CenterDeviceHttpTransport.ConfigureRequestRateLimit(new Uri("relative", UriKind.Relative), 1)));
            Assert.Throws<ArgumentException>((Action)(() => CenterDeviceHttpTransport.ConfigureRequestRateLimit(new Uri("ftp://example.test"), 1)));
            var gate = new RequestAllowance(new FakeClock());
            gate.ConfigureRateLimit(1);
            gate.ConfigureRateLimit(null);
            for (var i = 0; i < 65; i++) using (await gate.EnterAsync(CancellationToken.None)) { }
        }

        [Test]
        public async Task CancellationWhileWaitingForServerCooldownReleasesAdmission()
        {
            var clock = new BlockingClock();
            var gate = new RequestAllowance(clock);
            gate.DeferUntil(clock.UtcNow.AddMinutes(1));
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = gate.EnterAsync(cancellation.Token);
                await clock.Waiting.Task;
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { using (await pending) { } }));
            }
            clock.UtcNow += TimeSpan.FromMinutes(1);
            using (var one = await gate.EnterAsync(CancellationToken.None))
            using (var two = await gate.EnterAsync(CancellationToken.None))
            using (var three = await gate.EnterAsync(CancellationToken.None)) { }
        }

        private sealed class BlockingClock : IRequestClock
        {
            internal readonly TaskCompletionSource<bool> Waiting = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
            public Task DelayAsync(TimeSpan delay, CancellationToken token)
            {
                Waiting.TrySetResult(true);
                return Task.Delay(Timeout.Infinite, token);
            }
        }

        [Test]
        public void OriginAdmissionIgnoresUserInfoPathAndDefaultPortButSeparatesOtherPorts()
        {
            var host = Guid.NewGuid().ToString("N") + ".test";
            var gate = CenterDeviceHttpMessageHandler.GetAllowance(new Uri("https://" + host + "/first"));
            Assert.That(CenterDeviceHttpMessageHandler.GetAllowance(new Uri("https://fixture-user@" + host.ToUpperInvariant() + ":443/second")), Is.SameAs(gate));
            Assert.That(CenterDeviceHttpMessageHandler.GetAllowance(new Uri("https://" + host + ":8443/")), Is.Not.SameAs(gate));
            Assert.That(CenterDeviceHttpMessageHandler.GetAllowance(new Uri("http://" + host + "/")), Is.Not.SameAs(gate));
        }

        [Test]
        public async Task ShorterCooldownDoesNotOverrideAnExistingLongerCooldown()
        {
            var clock = new FakeClock();
            var gate = new RequestAllowance(clock);
            gate.DeferUntil(clock.UtcNow.AddSeconds(7));
            gate.DeferUntil(clock.UtcNow.AddSeconds(2));
            using (await gate.EnterAsync(CancellationToken.None)) { }
            Assert.That(clock.Delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(7) }));
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
