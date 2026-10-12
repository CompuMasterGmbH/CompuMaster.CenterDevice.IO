using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.SearchHistory;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public class SearchHistoryClientTests
    {
        private static JObject Record(string id = "entry", string term = "Ä term") => new JObject { ["id"] = id, ["term"] = term, ["timestamp"] = "2026-10-12T12:30:00+02:00", ["count"] = 5368709120L };

        [TestCase(false), TestCase(true)]
        public async Task AddTermTracksUsageWithoutExecutingSearchOrInventingOwner(bool sync)
        {
            var state = new State { Json = Record().ToString() }; var client = new Client(state);
            var result = sync ? client.AddTerm("context", "ä TERM") : await client.AddTermAsync("context", "ä TERM");
            Assert.That(result.Id, Is.EqualTo("entry")); Assert.That(result.Term, Is.EqualTo("Ä term"));
            Assert.That(result.Count, Is.EqualTo(5368709120L));
            Assert.That(result.Timestamp.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(result.TimestampText, Is.EqualTo("2026-10-12T12:30:00+02:00"));
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo("add"));
            Assert.That(Body(state)["params"].Value<string>("term"), Is.EqualTo("ä TERM"));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/search-history"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(typeof(SearchHistoryEntry).GetProperty("Owner"), Is.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(""), TestCase("  spaced  ")]
        public async Task OriginalEmptyAndWhitespaceTermTextRemainsExplicit(string term)
        {
            var state = new State { Json = Record(term: term).ToString() };
            Assert.That((await new Client(state).AddTermAsync("context", term)).Term, Is.EqualTo(term));
            Assert.That(Body(state)["params"].Value<string>("term"), Is.EqualTo(term));
        }

        [TestCase(false), TestCase(true)]
        public async Task OffsetFreeTimestampRetainsTextWithoutMachineTimezone(bool sync)
        {
            var data = Record(); data["timestamp"] = "2026-10-12T12:30:00";
            var state = new State { Json = data.ToString() }; var client = new Client(state);
            var result = sync ? client.AddTerm("context", "Ä term") : await client.AddTermAsync("context", "Ä term");
            Assert.That(result.Timestamp, Is.Null); Assert.That(result.TimestampText, Is.EqualTo("2026-10-12T12:30:00"));
        }

        [TestCase(false, null), TestCase(true, null), TestCase(false, SearchHistorySort.MostRecentFirst), TestCase(true, SearchHistorySort.MostUsedFirst)]
        public async Task HistoryPreservesServerOrderAndExplicitVersusOmittedSort(bool sync, SearchHistorySort? sort)
        {
            var state = new State { Json = new JObject { ["history"] = new JArray(Record("second"), Record("first")) }.ToString() }; var client = new Client(state);
            var page = sync ? client.GetHistory("context", sort) : await client.GetHistoryAsync("context", sort);
            Assert.That(page.Select(entry => entry.Id), Is.EqualTo(new[] { "second", "first" }));
            Assert.That(((IList<SearchHistoryEntry>)page).IsReadOnly, Is.True);
            var query = state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString).ToArray();
            Assert.That(query.Length, Is.EqualTo(sort.HasValue ? 1 : 0));
            if (sort.HasValue) { Assert.That(query[0].Name, Is.EqualTo("sort")); Assert.That(query[0].Value, Is.EqualTo(sort == SearchHistorySort.MostRecentFirst ? "most-recent-first" : "most-used-first")); }
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
        }

        [TestCase(false), TestCase(true)]
        public async Task SelectedDeletionSnapshotsIdsAndDoesNotInferWholeHistory(bool sync)
        {
            var ids = new List<string> { "selected", "all" };
            var state = new State { Status = HttpStatusCode.NoContent, BeforeAuthorize = () => ids.Clear() }; var client = new Client(state);
            if (sync) client.RemoveEntries("context", ids); else await client.RemoveEntriesAsync("context", ids);
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo("delete"));
            Assert.That(Body(state)["params"]["ids"], Is.TypeOf<JArray>());
            Assert.That(Body(state)["params"]["ids"].Values<string>(), Is.EqualTo(new[] { "selected", "all" }));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task WholeHistoryClearingIsAnExplicitDistinctStringSelection(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (sync) client.ClearHistory("context"); else await client.ClearHistoryAsync("context");
            Assert.That(Body(state)["params"]["ids"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(Body(state)["params"].Value<string>("ids"), Is.EqualTo("all"));
        }

        [TestCase("term"), TestCase("sort"), TestCase("ids-null"), TestCase("ids-empty"), TestCase("ids-blank")]
        public void InvalidInputsRejectBeforeAuthorization(string problem)
        {
            var state = new State(); var client = new Client(state);
            if (problem == "term") Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await client.AddTermAsync("context", null); }));
            else if (problem == "sort") Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await client.GetHistoryAsync("context", (SearchHistorySort)42); }));
            else Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await client.RemoveEntriesAsync("context", problem == "ids-null" ? null : problem == "ids-empty" ? new string[0] : new[] { " " }); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("id"), TestCase("term"), TestCase("timestamp"), TestCase("count")]
        public void MalformedRequiredMetadataRetainsOriginalCause(string field)
        {
            var data = Record(); data[field] = field == "count" ? (JToken)"1" : (JToken)1;
            var state = new State { Json = data.ToString() };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).AddTermAsync("context", "Ä term"); }));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("0"), TestCase("-1"), TestCase("1.5"), TestCase("9223372036854775808")]
        public void InvalidCountsCannotBeRoundedOrWrapped(string count)
        {
            var data = Record(); data["count"] = JToken.Parse(count);
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = data.ToString() }).AddTermAsync("context", "Ä term"); }));
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"history\":null}"), TestCase("{\"history\":[null]}"), TestCase("{\"history\":[]} {}")]
        public void InvalidHistoryStructuresCannotBeGuessed(string json)
        {
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = json }).GetHistoryAsync("context"); }));
        }

        [TestCase(0), TestCase(500), TestCase(501)]
        public async Task First500CapIsNotAnInventedPagingOrFullHistoryContract(int count)
        {
            var state = new State { Json = new JObject { ["history"] = new JArray(Enumerable.Range(0, count).Select(index => Record(index.ToString()))) }.ToString() };
            if (count > 500) Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).GetHistoryAsync("context"); }));
            else Assert.That((await new Client(state).GetHistoryAsync("context")).Count, Is.EqualTo(count));
        }

        [Test]
        public void RepeatedCanonicalIdsAndUnselectedTermsAreRejected()
        {
            var id = Guid.NewGuid(); var records = new JArray(Record(id.ToString("B").ToUpperInvariant()), Record(id.ToString("D")));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = new JObject { ["history"] = records }.ToString() }).GetHistoryAsync("context"); }));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = Record().ToString() }).AddTermAsync("context", "other"); }));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void DisabledHistoryVisibilityAndUncertainWriteErrorsRetainCause(int status)
        {
            var cause = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new Client(state).AddTermAsync("context", "term"); }));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeHttp()
        {
            var state = new State { Pending = true };
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new Client(state).ClearHistoryAsync("context", token.Token); }));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var token = new CancellationTokenSource())
            {
                var pending = new Client(state).GetHistoryAsync("context", cancellationToken: token.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(token.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(token.Token));
                token.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await pending; }));
            }
        }

        [TestCase("get", 200), TestCase("get", 503), TestCase("add", 200), TestCase("add", 503), TestCase("selected", 204), TestCase("selected", 503), TestCase("all", 204)]
        public async Task PublicFactoryRetainsTermAndNestedDeleteShapesWithoutUsageReplay(string operation, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string header = null, sent = null; var requests = 0;
                var expectedRequests = operation == "get" && status == 503 ? 3 : 1;
                var server = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < expectedRequests; attempt++)
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        requests++; var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException();
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        header = Encoding.ASCII.GetString(bytes.ToArray());
                        var lengthHeader = header.Split(new[] { "\r\n" }, StringSplitOptions.None).SingleOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var length = lengthHeader == null ? 0 : int.Parse(lengthHeader.Split(':')[1].Trim());
                        var received = new byte[length]; var count = 0;
                        while (count < length) { var read = await stream.ReadAsync(received, count, length - count, cancellation.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        sent = Encoding.UTF8.GetString(received);
                        var response = status == 503 ? "{}" : operation == "add" ? Record().ToString() : operation == "get" ? new JObject { ["history"] = new JArray(Record()) }.ToString() : "";
                        var payload = Encoding.UTF8.GetBytes(response);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).SearchHistory;
                    client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "get") Assert.That((await client.GetHistoryAsync("context", SearchHistorySort.MostUsedFirst, cancellation.Token)).Count, Is.EqualTo(1));
                        else if (operation == "add") Assert.That((await client.AddTermAsync("context", "ä TERM", cancellation.Token)).Count, Is.EqualTo(5368709120L));
                        else if (operation == "selected") await client.RemoveEntriesAsync("context", new[] { "entry", "all" }, cancellation.Token);
                        else await client.ClearHistoryAsync("context", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(operation == "get" ? "GET /bridge/v2/search-history?sort=most-used-first HTTP/1.1" : "POST /bridge/v2/search-history HTTP/1.1"));
                    if (operation == "get") Assert.That(sent, Is.Empty);
                    else if (operation == "add") Assert.That(JObject.Parse(sent)["params"].Value<string>("term"), Is.EqualTo("ä TERM"));
                    else if (operation == "selected") Assert.That(JObject.Parse(sent)["params"]["ids"].Values<string>(), Is.EqualTo(new[] { "entry", "all" }));
                    else Assert.That(JObject.Parse(sent)["params"].Value<string>("ids"), Is.EqualTo("all"));
                    Assert.That(requests, Is.EqualTo(expectedRequests)); Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    cancellation.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static JObject Body(State state)
        {
            using (var text = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader);
        }
        private sealed class Client : SearchHistoryRestClient
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
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(State state, Config config) : base(state, config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-search-history-tests";
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
