using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Timeline;
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
    public class TimelineContractTests
    {
        private const string Page = "{\"entries\":[{\"id\":\"event\",\"type\":\"future-type\",\"timestamp\":\"2025-01-02T03:04:05.123456789+05:30\",\"creator\":\"actual-creator\",\"details\":{\"document-id\":\"document\",\"version\":999999999999999999999999,\"date\":\"2025-01-02\",\"future\":false},\"future\":null}],\"future\":{\"retained\":true}}";
        [TestCaseSource(nameof(WireCases))]
        public async Task AllFormsRetainCompleteOptionsOriginalPageAndDefaults(bool scroll, bool sync, int variant)
        {
            var state = new State(); var client = new Client(state);
            var search = new TimelineSearchRequest(); var iteration = new TimelineScrollRequest();
            if (variant == 1)
            {
                search.Types = new List<string> { "Original", "Original" }; search.Users = new List<string> { "user" }; search.DocumentIds = new List<string> { "document" }; search.Timestamp = new TimelineTimestampRange { From = "2025-01-02T03:04:05.1234567+05:30", To = "2025-02-03T04:05:06Z" }; search.Offset = 0; search.Rows = 5001;
                iteration.Types = search.Types; iteration.Users = search.Users; iteration.DocumentIds = search.DocumentIds; iteration.PreviousId = " original cursor "; iteration.Rows = 5001; iteration.Sort = "desc";
            }
            else if (variant == 2) { search.Types = new List<string>(); search.Timestamp = new TimelineTimestampRange(); search.Rows = 1; iteration.Types = new List<string>(); iteration.Rows = 1; iteration.Sort = "asc"; }
            var page = scroll ? sync ? client.ScrollTimeline("context", iteration) : await client.ScrollTimelineAsync("context", iteration) : sync ? client.SearchTimeline("context", search) : await client.SearchTimelineAsync("context", search);
            var body = ReadJson(state.Body); Assert.That(body.Value<string>("action"), Is.EqualTo(scroll ? "scroll" : "search")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/timeline"));
            var options = (JObject)body["params"];
            if (variant == 0) Assert.That(options.Count, Is.Zero);
            else if (variant == 1)
            {
                Assert.That(options["types"].Values<string>(), Is.EqualTo(new[] { "Original", "Original" })); Assert.That(options["users"].Values<string>(), Is.EqualTo(new[] { "user" })); Assert.That(options["document-ids"].Values<string>(), Is.EqualTo(new[] { "document" })); Assert.That(options.Value<int>("rows"), Is.EqualTo(5001));
                if (scroll) { Assert.That(options.Value<string>("previous-id"), Is.EqualTo(" original cursor ")); Assert.That(options.Value<string>("sort"), Is.EqualTo("desc")); Assert.That(options["timestamp"], Is.Null); Assert.That(options["offset"], Is.Null); }
                else { Assert.That(options["timestamp"].Value<string>("from"), Is.EqualTo(search.Timestamp.From)); Assert.That(options["timestamp"].Value<string>("to"), Is.EqualTo(search.Timestamp.To)); Assert.That(options.Value<int>("offset"), Is.Zero); Assert.That(options["sort"], Is.Null); }
            }
            else { Assert.That(((JArray)options["types"]).Count, Is.Zero); Assert.That(options.Value<int>("rows"), Is.EqualTo(1)); if (!scroll) Assert.That(((JObject)options["timestamp"]).Count, Is.Zero); }
            var entry = page.Entries.Single(); Assert.That(entry.Id, Is.EqualTo("event")); Assert.That(entry.Type, Is.EqualTo("future-type")); Assert.That(entry.Creator, Is.EqualTo("actual-creator")); Assert.That(entry.Timestamp, Is.EqualTo("2025-01-02T03:04:05.123456789+05:30")); Assert.That(entry.Details["version"].ToString(), Is.EqualTo("999999999999999999999999")); Assert.That(entry.Details.Value<string>("date"), Is.EqualTo("2025-01-02")); Assert.That(page.RawJson, Is.EqualTo(Page));
            entry.Details["document-id"] = "forged"; entry.GetRawDetails()["timestamp"] = "forged"; page.GetRawDetails()["entries"] = new JArray(); Assert.That(entry.Details.Value<string>("document-id"), Is.EqualTo("document")); Assert.That(page.Entries.Count, Is.EqualTo(1)); Assert.Throws<NotSupportedException>((Action)(() => ((IList<TimelineEventDetails>)page.Entries).Clear())); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
        }
        public static IEnumerable<TestCaseData> WireCases() { foreach (var scroll in new[] { false, true }) foreach (var sync in new[] { false, true }) foreach (var variant in new[] { 0, 1, 2 }) yield return new TestCaseData(scroll, sync, variant); }
        [TestCase(null), TestCase(""), TestCase("{}"), TestCase("{\"entries\":null}"), TestCase("{\"entries\":[false]}"), TestCase("{\"entries\":[{}]}"), TestCase("{\"entries\":[{\"id\":\"id\",\"type\":5}]}"), TestCase("{\"entries\":[{\"id\":\"id\",\"type\":\"type\",\"timestamp\":5}]}"), TestCase("{\"entries\":[{\"id\":\"id\",\"type\":\"type\",\"details\":[]}]}"), TestCase("{\"entries\":[],\"entries\":[]}"), TestCase("{\"entries\":[]}{}")]
        public void InvalidPagesRetainInnerDiagnostics(string json) { var state = new State { Json = json }; var error = ExpectAsync<InvalidResponseDataException>(async () => await new Client(state).SearchTimelineAsync("context", new TimelineSearchRequest())); Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(false), TestCase(true)]
        public async Task EmptyOrHiddenMetadataIsNotInferredFromAuthentication(bool empty)
        { var state = new State { Json = empty ? "{\"entries\":[]}" : "{\"entries\":[{\"id\":\"id\",\"type\":\"unknown\",\"timestamp\":null,\"creator\":null,\"details\":null}]}" }; var page = await new Client(state).SearchTimelineAsync("context", new TimelineSearchRequest()); if (empty) Assert.That(page.Entries.Count, Is.Zero); else { Assert.That(page.Entries[0].Timestamp, Is.Null); Assert.That(page.Entries[0].Creator, Is.Null); Assert.That(page.Entries[0].Details, Is.Null); } }
        [TestCase(false), TestCase(true)]
        public void RepeatedCanonicalEventsOrExclusiveCursorAreInvalid(bool previous)
        { const string id = "9E91DAD2-0820-4BC0-BF37-D923191CE44D"; var entry = new JObject { ["id"] = id.ToLowerInvariant(), ["type"] = "future" }; var state = new State { Json = new JObject { ["entries"] = previous ? new JArray(entry) : new JArray(entry, new JObject { ["id"] = id, ["type"] = "future" }) }.ToString() }; ExpectAsync<InvalidResponseDataException>(async () => await new Client(state).ScrollTimelineAsync("context", new TimelineScrollRequest { PreviousId = previous ? id : null })); }
        [TestCase(false), TestCase(true)]
        public void OversizedPageCannotHideAFalseCursorBoundary(bool serverLimit)
        { var entries = new JArray(Enumerable.Range(0, serverLimit ? 1001 : 2).Select(index => new JObject { ["id"] = index.ToString(), ["type"] = "future" })); var state = new State { Json = new JObject { ["entries"] = entries }.ToString() }; ExpectAsync<InvalidResponseDataException>(async () => await new Client(state).SearchTimelineAsync("context", new TimelineSearchRequest { Rows = serverLimit ? 5001 : 1 })); }
        [TestCase("types"), TestCase("users"), TestCase("documents"), TestCase("null-value"), TestCase("blank-value"), TestCase("rows"), TestCase("offset-negative"), TestCase("offset-large"), TestCase("from"), TestCase("to"), TestCase("sort"), TestCase("cursor")]
        public void InvalidChoicesFailBeforeAuthorization(string choice)
        {
            var state = new State(); var request = new TimelineSearchRequest(); var scroll = new TimelineScrollRequest();
            if (choice == "types") request.Types = Enumerable.Range(0, 51).Select(index => "type" + index).ToList(); else if (choice == "users") request.Users = Enumerable.Range(0, 11).Select(index => "user" + index).ToList(); else if (choice == "documents") request.DocumentIds = Enumerable.Range(0, 101).Select(index => "document" + index).ToList(); else if (choice == "null-value" || choice == "blank-value") request.Types = new List<string> { choice == "null-value" ? null : " " }; else if (choice == "rows") request.Rows = 0; else if (choice.StartsWith("offset")) request.Offset = choice == "offset-negative" ? -1 : 5001; else if (choice == "from" || choice == "to") request.Timestamp = new TimelineTimestampRange { From = choice == "from" ? "invalid" : null, To = choice == "to" ? "invalid" : null }; else if (choice == "sort") scroll.Sort = "DESC"; else scroll.PreviousId = " ";
            ExpectAsync<ArgumentException>(async () => { if (choice == "sort" || choice == "cursor") await new Client(state).ScrollTimelineAsync("context", scroll); else await new Client(state).SearchTimelineAsync("context", request); }); Assert.That(state.Authorizations, Is.Zero);
        }
        [TestCase(false), TestCase(true)]
        public void NullRequestDoesNotAuthorize(bool scroll) { var state = new State(); ExpectAsync<ArgumentNullException>(async () => { if (scroll) await new Client(state).ScrollTimelineAsync("context", null); else await new Client(state).SearchTimelineAsync("context", null); }); Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task OriginalDuplicateFiltersAreCountedDistinctlyAndNeverNormalized()
        { var state = new State(); const string id = "9E91DAD2-0820-4BC0-BF37-D923191CE44D"; var values = Enumerable.Repeat(id, 101).Concat(new[] { id.ToLowerInvariant() }).ToList(); var request = new TimelineScrollRequest { Types = Enumerable.Repeat("future", 51).ToList(), Users = values, DocumentIds = values, Sort = "asc" }; await new Client(state).ScrollTimelineAsync("context", request); var json = ReadJson(state.Body); Assert.That(json["params"]["users"].Values<string>(), Is.EqualTo(values)); Assert.That(json["params"]["types"].Count(), Is.EqualTo(51)); }
        [Test]
        public async Task EveryMutableChoiceIsSnapshottedBeforeAuthorization()
        { var request = new TimelineSearchRequest { Types = new List<string> { "original" }, Users = new List<string> { "user" }, DocumentIds = new List<string> { "document" }, Timestamp = new TimelineTimestampRange { From = "2025-01-02T03:04:05Z" }, Rows = 1 }; var state = new State { OnAuthorize = () => { request.Types.Add("later"); request.Users.Clear(); request.DocumentIds.Clear(); request.Timestamp.From = "changed"; request.Rows = 99; } }; await new Client(state).SearchTimelineAsync("context", request); var options = ReadJson(state.Body)["params"]; Assert.That(options["types"].Values<string>(), Is.EqualTo(new[] { "original" })); Assert.That(options["users"].Values<string>(), Is.EqualTo(new[] { "user" })); Assert.That(options["document-ids"].Values<string>(), Is.EqualTo(new[] { "document" })); Assert.That(options["timestamp"].Value<string>("from"), Is.EqualTo("2025-01-02T03:04:05Z")); Assert.That(options.Value<int>("rows"), Is.EqualTo(1)); }
        [TestCase(false), TestCase(true)]
        public void PreCanceledRequestsDoNotAuthorizeOrValidateNullOptions(bool scroll)
        { var state = new State(); using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); ExpectAsync<OperationCanceledException>(async () => { if (scroll) await new Client(state).ScrollTimelineAsync("context", null, cancellation.Token); else await new Client(state).SearchTimelineAsync("context", null, cancellation.Token); }); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task ActiveNativeIoReceivesOriginalCancellation()
        { var state = new State { Pending = true }; using (var cancellation = new CancellationTokenSource()) { var pending = new Client(state).ScrollTimelineAsync("context", new TimelineScrollRequest(), cancellation.Token); await state.Entered.Task; cancellation.Cancel(); ExpectAsync<TaskCanceledException>(async () => await pending); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); } Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void OriginalServerErrorsRemainIntactWithoutReconciliation(int status, Type type)
        { var cause = new IOException("isolated failure"); var state = new State { Status = (HttpStatusCode)status, Error = cause, Json = "{\"code\":300,\"message\":\"test failure\"}" }; var error = Assert.ThrowsAsync(type,(Func<Task>)(async () => await new Client(state).ScrollTimelineAsync("context", new TimelineScrollRequest { PreviousId = "expired" }))); Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(false), TestCase(true)]
        public async Task HistoricalAsyncMethodsRetainTheirOriginalOptions(bool scroll)
        { var state = new State(); var client = new Client(state); if (scroll) await client.ScrollAsync("context", null, null); else await client.GetTimelineEventsAsync("context", new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), 0, 7, null); var options = ReadJson(state.Body)["params"]; Assert.That(options.Value<int>("rows"), Is.EqualTo(scroll ? 500 : 7)); Assert.That(options["document-ids"], Is.Null); Assert.That(options["users"], Is.Null); if (!scroll) Assert.That(options["timestamp"].Value<string>("from"), Is.EqualTo("2025-01-02T03:04:05Z")); }
        [TestCase(false, 200), TestCase(true, 200), TestCase(true, 503)]
        public async Task PublicFactoryRetainsActualHttpFiltersUtf8RawPageAndErrors(bool scroll, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string capturedHeader = null, capturedBody = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768) { if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        capturedHeader = Encoding.ASCII.GetString(bytes.ToArray()); var length = capturedHeader.Split(new[] { "\r\n" }, StringSplitOptions.None).SingleOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var body = new byte[length == null ? 0 : int.Parse(length.Substring(length.IndexOf(':') + 1).Trim())];
                        for (int offset = 0; offset < body.Length;) { int count = await stream.ReadAsync(body, offset, body.Length - offset, cancellation.Token); if (count == 0) throw new EndOfStreamException(); offset += count; } capturedBody = Encoding.UTF8.GetString(body);
                        var payload = Encoding.UTF8.GetBytes(status == 200 ? Page.Replace("actual-creator", "Original ä") : "{\"code\":300,\"message\":\"isolated failure\"}");
                        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : " Service Unavailable") + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n"); await stream.WriteAsync(header, 0, header.Length, cancellation.Token); await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Configuration("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Timeline; client.DisableOfflineModeSimulation();
                    Func<Task<TimelineEventPage>> execute = () => scroll ? client.ScrollTimelineAsync("context", new TimelineScrollRequest { PreviousId = "previous", Sort = "desc", Users = new List<string> { "user" }, DocumentIds = new List<string> { "document" }, Rows = 5001 }, cancellation.Token) : client.SearchTimelineAsync("context", new TimelineSearchRequest { Timestamp = new TimelineTimestampRange { To = "2025-01-02T03:04:05.1234567+05:30" }, Users = new List<string> { "user" }, DocumentIds = new List<string> { "document" }, Rows = 5001 }, cancellation.Token);
                    if (status != 200) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await execute())); else { var result = await execute(); Assert.That(result.Entries.Single().Creator, Is.EqualTo("Original ä")); Assert.That(result.RawJson, Is.EqualTo(Page.Replace("actual-creator", "Original ä"))); Assert.That(result.Entries.Single().Details["version"].ToString(), Is.EqualTo("999999999999999999999999")); }
                    await server; Assert.That(capturedHeader.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/timeline HTTP/1.1"));
                    var body = ReadJson(capturedBody); Assert.That(body.Value<string>("action"), Is.EqualTo(scroll ? "scroll" : "search")); Assert.That(body["params"].Value<int>("rows"), Is.EqualTo(5001)); Assert.That(body["params"]["users"].Values<string>(), Is.EqualTo(new[] { "user" })); Assert.That(body["params"]["document-ids"].Values<string>(), Is.EqualTo(new[] { "document" }));
                    if (scroll) { Assert.That(body["params"].Value<string>("sort"), Is.EqualTo("desc")); Assert.That(body["params"].Value<string>("previous-id"), Is.EqualTo("previous")); } else Assert.That(body["params"]["timestamp"].Value<string>("to"), Is.EqualTo("2025-01-02T03:04:05.1234567+05:30")); Assert.That(listener.Pending(), Is.False);
                }
                finally { cancellation.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }
        private sealed class Factory : CenterDeviceClientBase { internal Factory(State state, Configuration configuration) : base(state, configuration, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private static JObject ReadJson(string json) { using (var source = new StringReader(json)) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private static T ExpectAsync<T>(Func<Task> action) where T : Exception => Assert.ThrowsAsync<T>(action);
        private sealed class Configuration : IRestClientConfiguration { internal Configuration(string address = "https://unused.invalid/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-timeline-contracts"; }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal HttpStatusCode Status = HttpStatusCode.OK; internal string Json = Page, Body, Context; internal Exception Error; internal RestRequest Request; internal CancellationToken Token; internal int Calls, Authorizations; internal Action OnAuthorize; internal bool Pending; internal TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native async auth required."); public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; OnAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(OAuthInfo info, RestRequest request, CancellationToken token) { Calls++; Context = info.UserId; Request = request; Token = token; var body = request.Parameters.OfType<BodyParameter>().Single().Value; Body = body is string text ? text : JsonConvert.SerializeObject(body); Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json, ErrorException = Error }; }
        }
        private sealed class Client : TimelineRestClient
        {
            private readonly State state; internal Client(State state) : base(state, new Configuration(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(info, request, token);
            protected override async Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) { var response = await state.Send(info, request, token); return new RestResponse<T>(request) { StatusCode = response.StatusCode, Content = response.Content, Data = (T)(object)new TimelineSearchResults { Entries = new List<TimelineEntry>() } }; }
        }
    }
}
