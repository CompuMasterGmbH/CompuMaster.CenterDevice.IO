using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.AuditLog;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class AuditLogClientTests
    {
        [TestCase(false), TestCase(true)]
        public async Task SearchSnapshotsNestedFiltersBeforeAuthorizationAndPreservesWireNames(bool sync)
        {
            var start = DateTimeOffset.Parse("2026-10-12T12:00:00+02:00");
            var request = new AuditLogSearchRequest { Types = new List<string> { "document-viewed" }, Users = new List<string>(),
                DocumentIds = new List<string> { "document" }, Timestamp = new AuditLogTimestampRange { From = start, To = start }, Offset = 5000, Rows = 1000 };
            var state = new State { BeforeAuthorize = () => { request.Types.Clear(); request.Timestamp.From = null; request.Rows = 1; } };
            var client = new Client(state);
            var page = sync ? client.Search("context", "collection with/slash", request) : await client.SearchAsync("context", "collection with/slash", request);
            var body = Body(state);
            Assert.That(body.Value<string>("action"), Is.EqualTo("search"));
            Assert.That(body["params"]["types"].Values<string>(), Is.EqualTo(new[] { "document-viewed" }));
            Assert.That(body["params"]["users"], Is.TypeOf<JArray>().And.Empty);
            Assert.That(body["params"]["document-ids"].Values<string>(), Is.EqualTo(new[] { "document" }));
            Assert.That(body["params"].Value<int>("offset"), Is.EqualTo(5000));
            Assert.That(body["params"].Value<int>("rows"), Is.EqualTo(1000));
            Assert.That(DateTimeOffset.Parse(body["params"]["timestamp"].Value<string>("from")), Is.EqualTo(start));
            Assert.That(DateTimeOffset.Parse(body["params"]["timestamp"].Value<string>("to")), Is.EqualTo(start));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/collection/collection%20with%2Fslash/auditlog"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(page, Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task ScrollPreservesCursorAndLogicalServerOrderWithoutInferredTotal(bool sync)
        {
            var state = new State { Json = "{\"entries\":[{\"id\":\"later\",\"type\":\"future-event\",\"timestamp\":\"2026-10-12T10:00:00Z\"},{\"id\":\"earlier\",\"type\":\"future-event\",\"timestamp\":\"2026-10-12T09:00:00Z\"}]}" };
            var request = new AuditLogScrollRequest { PreviousId = "previous", Sort = "asc", Rows = 2 };
            var client = new Client(state);
            var page = sync ? client.Scroll("context", "collection", request) : await client.ScrollAsync("context", "collection", request);
            Assert.That(page.Select(entry => entry.Id), Is.EqualTo(new[] { "later", "earlier" }));
            Assert.That(Body(state)["params"].Value<string>("previous-id"), Is.EqualTo("previous"));
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo("scroll"));
            Assert.That(Body(state)["params"]["offset"], Is.Null);
            Assert.That(((IList<AuditLogEntry>)page).IsReadOnly, Is.True);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("document-viewed"), TestCase("document-downloaded")]
        public async Task AccessEventsRetainVersionFilenameOffsetAndDefensiveUnknownDetails(string type)
        {
            var state = new State { Json = "{\"entries\":[{\"id\":\"event\",\"type\":\"" + type + "\",\"timestamp\":\"2026-10-12T12:30:00+02:00\",\"creator\":\"other-user\",\"details\":{\"document-id\":\"doc\",\"version\":7,\"filename\":\"ältere.txt\",\"future\":{\"count\":5368709120}}}]}" };
            var entry = (await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest()))[0];
            Assert.That(entry.Type, Is.EqualTo(type));
            Assert.That(entry.Timestamp.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(entry.TimestampText, Is.EqualTo("2026-10-12T12:30:00+02:00"));
            Assert.That(entry.Creator, Is.EqualTo("other-user"));
            Assert.That(entry.DocumentAccess.DocumentId, Is.EqualTo("doc"));
            Assert.That(entry.DocumentAccess.Version, Is.EqualTo(7));
            Assert.That(entry.DocumentAccess.Filename, Is.EqualTo("ältere.txt"));
            entry.Details["future"]["count"] = 0;
            Assert.That(entry.Details["future"].Value<long>("count"), Is.EqualTo(5368709120L));
        }

        [TestCase("2026-10-12T12:30:00"), TestCase(null)]
        public async Task MissingOffsetCreatorAndAccessDetailsRemainUnknown(string timestamp)
        {
            var data = new JObject { ["entries"] = new JArray(new JObject { ["id"] = "event", ["type"] = "document-viewed", ["timestamp"] = timestamp, ["details"] = new JObject() }) };
            var state = new State { Json = data.ToString() };
            var entry = (await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest()))[0];
            Assert.That(entry.Timestamp, Is.Null);
            Assert.That(entry.TimestampText, Is.EqualTo(timestamp));
            Assert.That(entry.Creator, Is.Null);
            Assert.That(entry.DocumentAccess.DocumentId, Is.Null);
            Assert.That(entry.DocumentAccess.Version, Is.Null);
            Assert.That(entry.DocumentAccess.Filename, Is.Null);
            Assert.That(Body(state)["params"].Children(), Is.Empty);
        }

        [TestCase(0), TestCase(-1)]
        public void InvalidRowsRejectBeforeAuthorization(int rows)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest { Rows = rows }); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [Test]
        public async Task LargeRequestedPageRetainsRequestButEnforcesServerReturnCap()
        {
            var state = new State();
            await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest { Rows = int.MaxValue });
            Assert.That(Body(state)["params"].Value<int>("rows"), Is.EqualTo(int.MaxValue));
            var entries = new JArray(Enumerable.Range(0, 1001).Select(index => new JObject { ["id"] = index.ToString(), ["type"] = "t" }));
            state = new State { Json = new JObject { ["entries"] = entries }.ToString() };
            var client = new Client(state);
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await client.SearchAsync("context", "collection", new AuditLogSearchRequest { Rows = int.MaxValue }); }));
        }

        [TestCase(-1), TestCase(5001)]
        public void InvalidOffsetRejectsBeforeAuthorization(int offset)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest { Offset = offset }); }));
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("types", 20), TestCase("users", 10), TestCase("document-ids", 10)]
        public async Task AuditLimitsCountDistinctValuesAndDifferFromTimeline(string filter, int maximum)
        {
            var values = Enumerable.Range(0, maximum).Select(index => index.ToString()).ToList();
            values.Add(values[0]);
            var request = new AuditLogSearchRequest { Types = filter == "types" ? values : null, Users = filter == "users" ? values : null, DocumentIds = filter == "document-ids" ? values : null };
            var state = new State();
            await new Client(state).SearchAsync("context", "collection", request);
            Assert.That(((JArray)Body(state)["params"][filter]).Count, Is.EqualTo(maximum + 1));
            values.Add("overflow");
            state = new State();
            var client = new Client(state);
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await client.SearchAsync("context", "collection", request); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("cursor"), TestCase("sort"), TestCase("range"), TestCase("filter"), TestCase("collection"), TestCase("request")]
        public void InvalidScrollInputsRejectBeforeAuthorization(string problem)
        {
            var state = new State();
            var request = new AuditLogScrollRequest { PreviousId = problem == "cursor" ? " " : null, Sort = problem == "sort" ? "timestamp" : null,
                Users = problem == "filter" ? new List<string> { null } : null,
                Timestamp = problem == "range" ? new AuditLogTimestampRange { From = DateTimeOffset.UtcNow, To = DateTimeOffset.MinValue } : null };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).ScrollAsync("context", problem == "collection" ? " " : "collection", problem == "request" ? null : request); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"entries\":null}"), TestCase("{\"entries\":[null]}"), TestCase("{\"entries\":[{}]}"), TestCase("{\"entries\":[]} {}")]
        [TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"t\",\"details\":[]}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"t\",\"timestamp\":\"yesterday\"}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"t\",\"creator\":1}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"t\"},{\"id\":\"x\",\"type\":\"t\"}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":18}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"document-viewed\",\"details\":{\"version\":9223372036854775808}}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"document-viewed\",\"details\":{\"version\":0}}]}"), TestCase("{\"entries\":[{\"id\":\"x\",\"type\":\"document-viewed\",\"details\":{\"version\":\"1\"}}]}")]
        public void MalformedSuccessRetainsOriginalCause(string json)
        {
            var state = new State { Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest()); }));
            Assert.That(error.InnerException, Is.Not.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void CursorIsExclusiveAndRepeatedGuidSpellingCannotEvadeIdentityChecks(bool repeat)
        {
            var id = Guid.NewGuid();
            var events = new JArray(new JObject { ["id"] = id.ToString("B").ToUpperInvariant(), ["type"] = "t" });
            if (repeat) events.Add(new JObject { ["id"] = id.ToString("D"), ["type"] = "t" });
            var state = new State { Json = new JObject { ["entries"] = events }.ToString() };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).ScrollAsync("context", "collection", new AuditLogScrollRequest { PreviousId = repeat ? null : id.ToString("D") }); }));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void RoleCursorAndUncertainPostErrorsAreNotHiddenOrReplayed(int status)
        {
            var cause = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new Client(state).ScrollAsync("context", "collection", new AuditLogScrollRequest { PreviousId = "unknown" }); }));
            Assert.That(error.InnerException, Is.SameAs(cause));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task CancellationReachesAuthorizationAndNativeHttp()
        {
            var state = new State { Pending = true };
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new Client(state).SearchAsync("context", "collection", new AuditLogSearchRequest(), token.Token); }));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var token = new CancellationTokenSource())
            {
                var pending = new Client(state).ScrollAsync("context", "collection", new AuditLogScrollRequest(), token.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(token.Token));
                Assert.That(state.AuthorizationToken, Is.EqualTo(token.Token));
                token.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await pending; }));
            }
        }

        [TestCase(200), TestCase(503)]
        public async Task PublicAuditFactoryUsesActualNestedPostAndNeverReplaysUncertainOutcome(int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string header = null;
                var requestCount = 0;
                var server = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < 1; attempt++)
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
                        var length = int.Parse(header.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                        var received = new byte[length]; var count = 0;
                        while (count < length) { var read = await stream.ReadAsync(received, count, length - count, cancellation.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        var body = JObject.Parse(Encoding.UTF8.GetString(received)); Assert.That(body.Value<string>("action"), Is.EqualTo("search")); Assert.That(body["params"]["types"].Values<string>(), Is.EqualTo(new[] { "document-viewed" }));
                        var payload = Encoding.UTF8.GetBytes(status == 200 ? "{\"entries\":[]}" : "{}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.AuditLog;
                    client.DisableOfflineModeSimulation();
                    var options = new AuditLogSearchRequest { Types = new List<string> { "document-viewed" } };
                    if (status == 503) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await client.SearchAsync("context", "collection", options, cancellation.Token); }));
                    else Assert.That(await client.SearchAsync("context", "collection", options, cancellation.Token), Is.Empty);
                    await server;
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0],
                        Is.EqualTo("POST /bridge/v2/collection/collection/auditlog HTTP/1.1"));
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None).Any(line => line.StartsWith("Content-Length:") && line.Substring(line.IndexOf(':') + 1).Trim() != "0"), Is.True);
                    Assert.That(listener.Pending(), Is.False);
                    Assert.That(requestCount, Is.EqualTo(1), "Preserve the existing bounded GET-read retry policy.");
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

        private static JObject Body(State state) { using (var text = new System.IO.StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value)) using (var reader = new Newtonsoft.Json.JsonTextReader(text) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None }) return JObject.Load(reader); }
        private sealed class Client : AuditLogRestClient
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
            public string UserAgent => "isolated-audit-tests";
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal string Json = "{\"entries\":[]}", Context;
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
