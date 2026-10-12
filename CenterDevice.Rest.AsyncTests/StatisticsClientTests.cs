using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Statistics;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class StatisticsClientTests
    {
        [TestCase(false), TestCase(true)]
        public async Task SelectedBytesPreserve64BitCountsAndSnapshotBeforeAuthorization(bool sync)
        {
            var selected = new List<string> { StatisticsRestClient.DocumentsBytesUsed };
            var state = new State { Json = "{\"documents-bytes-used\":5368709120}", BeforeAuthorize = () => selected.Clear() };
            var client = new Client(state);
            var result = sync ? client.GetStatistics("context", selected) : await client.GetStatisticsAsync("context", selected);
            Assert.That(result.DocumentsBytesUsed, Is.EqualTo(5368709120L));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/stats"));
            Assert.That(state.Request.Parameters.Single(parameter => parameter.Type == ParameterType.QueryString).Value, Is.EqualTo("documents-bytes-used"));
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            Assert.That(state.Calls, Is.EqualTo(1));
            Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(typeof(UserStatistics).GetProperty("Owner"), Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public async Task OmittedIncludesRetains204AndUnknownRatherThanInventingZero(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent };
            var client = new Client(state);
            var result = sync ? client.GetStatistics("context") : await client.GetStatisticsAsync("context");
            Assert.That(result.DocumentsBytesUsed, Is.Null);
            Assert.That(state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString), Is.Empty);
        }

        [TestCase(0L), TestCase(long.MaxValue)]
        public async Task ZeroAndMaximumCountsRemainKnown(long bytes)
        {
            var state = new State { Json = "{\"documents-bytes-used\":" + bytes + "}" };
            Assert.That((await new Client(state).GetStatisticsAsync("context", new[] { StatisticsRestClient.DocumentsBytesUsed })).DocumentsBytesUsed, Is.EqualTo(bytes));
        }

        [TestCase("empty"), TestCase("null"), TestCase("blank"), TestCase("unknown")]
        public void InvalidExplicitSelectionRejectsBeforeAuthorization(string problem)
        {
            var selected = problem == "empty" ? new string[0] : new[] { problem == "null" ? null : problem == "blank" ? " " : "unknown" };
            var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).GetStatisticsAsync("context", selected); }));
            Assert.That(state.Authorizations, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("{}"), TestCase("null"), TestCase("[]"), TestCase("{\"documents-bytes-used\":null}")]
        [TestCase("{\"documents-bytes-used\":-1}"), TestCase("{\"documents-bytes-used\":1.5}"), TestCase("{\"documents-bytes-used\":\"2\"}")]
        [TestCase("{\"documents-bytes-used\":9223372036854775808}"), TestCase("{\"documents-bytes-used\":1} {}")]
        public void MalformedSelectedCountRetainsOriginalParsingFailure(string json)
        {
            var state = new State { Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).GetStatisticsAsync("context", new[] { StatisticsRestClient.DocumentsBytesUsed }); }));
            Assert.That(error.InnerException, Is.Not.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void ServerErrorsPreserveOriginalCause(int status)
        {
            var cause = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new Client(state).GetStatisticsAsync("context", new[] { StatisticsRestClient.DocumentsBytesUsed }); }));
            Assert.That(error.InnerException, Is.SameAs(cause));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task SelectionIsEnumeratedOnceAndExplicitDuplicateNamesRemainExplicit()
        {
            var enumerations = 0;
            IEnumerable<string> Selection()
            { enumerations++; yield return StatisticsRestClient.DocumentsBytesUsed; yield return StatisticsRestClient.DocumentsBytesUsed; }
            var state = new State { Json = "{\"documents-bytes-used\":1}" };
            await new Client(state).GetStatisticsAsync("context", Selection());
            Assert.That(enumerations, Is.EqualTo(1));
            Assert.That(state.Request.Parameters.Single(parameter => parameter.Type == ParameterType.QueryString).Value,
                Is.EqualTo("documents-bytes-used,documents-bytes-used"));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeRequest()
        {
            var state = new State { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new Client(state).GetStatisticsAsync("context", cancellationToken: cancellation.Token); }));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = new Client(state).GetStatisticsAsync("context", cancellationToken: cancellation.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await pending; }));
            }
        }

        [TestCase(200), TestCase(204), TestCase(503)]
        public async Task PublicStatisticsFactoryUsesActualBodylessGetAndPreservesStatus(int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string header = null;
                var requestCount = 0;
                var server = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < (status == 503 ? 3 : 1); attempt++)
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        requestCount++;
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException();
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        header = Encoding.ASCII.GetString(bytes.ToArray());
                        var payload = Encoding.UTF8.GetBytes(status == 200 ? "{\"documents-bytes-used\":5368709120}" : status == 204 ? "" : "{}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.Statistics;
                    client.DisableOfflineModeSimulation();
                    var includes = status == 204 ? null : new[] { StatisticsRestClient.DocumentsBytesUsed };
                    if (status == 503) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await client.GetStatisticsAsync("context", includes, cancellation.Token); }));
                    else Assert.That((await client.GetStatisticsAsync("context", includes, cancellation.Token)).DocumentsBytesUsed, Is.EqualTo(status == 204 ? (long?)null : 5368709120L));
                    await server;
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0],
                        Is.EqualTo(status == 204 ? "GET /bridge/v2/stats HTTP/1.1" : "GET /bridge/v2/stats?includes=documents-bytes-used HTTP/1.1"));
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None).Any(line => line.StartsWith("Content-Length:") && line.Substring(line.IndexOf(':') + 1).Trim() != "0"), Is.False);
                    Assert.That(listener.Pending(), Is.False);
                    Assert.That(requestCount, Is.EqualTo(status == 503 ? 3 : 1), "Preserve the existing bounded GET-read retry policy.");
                }
                finally
                {
                    cancellation.Cancel(); listener.Stop();
                    try { await server; }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                    catch (OperationCanceledException) { }
                }
            }
        }

        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(State state, Config config) : base(state, config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }

        private sealed class Client : StatisticsRestClient
        {
            private readonly State state;
            internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override async Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                state.Calls++; state.Request = request; state.Token = token; state.Entered.TrySetResult(true);
                if (state.Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = state.Status, Content = state.Json, ErrorException = state.Error };
            }
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-statistics-tests";
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal string Json, Context;
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Exception Error;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            {
                Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
        }
    }
}
