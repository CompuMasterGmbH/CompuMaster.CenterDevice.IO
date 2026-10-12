using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Trash;
using CenterDevice.Rest.Exceptions;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class TrashClientTests
    {
        [TestCase(false), TestCase(true)]
        public async Task PagePreservesServerOrderLargeSizeOffsetsAndUnknownMetadata(bool sync)
        {
            var state = new State { Json = "{\"documents\":[{\"id\":\"newer\",\"size\":5368709120,\"deletion-date\":\"2026-10-12T08:09:10+02:00\",\"tags\":[]},{\"id\":\"older\"}]}" };
            var client = new Client(state);
            var page = sync ? client.GetDocuments("context", 501, 2) : await client.GetDocumentsAsync("context", 501, 2);
            Assert.That(page.Select(document => document.Id), Is.EqualTo(new[] { "newer", "older" }));
            Assert.That(page[0].Size, Is.EqualTo(5L * 1024 * 1024 * 1024));
            Assert.That(page[0].DeletionDate.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(page[0].Tags, Is.Empty);
            Assert.That(page[1].Tags, Is.Null);
            Assert.That(page[1].Size, Is.Null);
            Assert.That(page[1].UploadDate, Is.Null);
            Assert.That(page[1].DeletionDate, Is.Null);
            Assert.That(typeof(TrashDocument).GetProperty("Owner"), Is.Null);
            Assert.That(typeof(TrashDocument).GetProperty("Version"), Is.Null);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/trash"));
            Assert.That(Query(state.Request, "offset"), Is.EqualTo("501"));
            Assert.That(Query(state.Request, "rows"), Is.EqualTo("2"));
            Assert.Throws<NotSupportedException>((Action)(() => { ((IList<TrashDocument>)page).Clear(); }));
        }

        [Test]
        public async Task KnownEmptyPageAndDefaultBoundsDoNotInventHitsOrAnotherPage()
        {
            var state = new State { Json = "{\"documents\":[]}" };
            Assert.That(await new Client(state).GetDocumentsAsync("context"), Is.Empty);
            Assert.That(Query(state.Request, "offset"), Is.EqualTo("0"));
            Assert.That(Query(state.Request, "rows"), Is.EqualTo("500"));
        }

        [TestCase(-1, 1), TestCase(0, 0), TestCase(0, -1), TestCase(0, 501)]
        public void InvalidBoundsRejectBeforeAuthorization(int offset, int rows)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(async () => { await new Client(state).GetDocumentsAsync("context", offset, rows); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("{}"), TestCase("{\"documents\":null}"), TestCase("{\"documents\":[null]}")]
        [TestCase("{\"documents\":[{}]}"), TestCase("{\"documents\":[{\"id\":\" \"}]}")]
        [TestCase("{\"documents\":[{\"id\":\"one\",\"size\":-1}]}")]
        [TestCase("{\"documents\":[{\"id\":\"one\"},{\"id\":\"one\"}]}")]
        [TestCase("{\"documents\":[{\"id\":\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"},{\"id\":\"AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE\"}]}")]
        public void IncompleteOrMalformedSuccessfulListingIsRejected(string json)
        {
            var state = new State { Json = json };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).GetDocumentsAsync("context"); }));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulPageCannotExceedTheRequestedLimit()
        {
            var state = new State { Json = "{\"documents\":[{\"id\":\"one\"},{\"id\":\"two\"}]}" };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).GetDocumentsAsync("context", rows: 1); }));
        }

        [TestCase("restore", false), TestCase("restore", true), TestCase("erase", false), TestCase("erase", true), TestCase("all", false), TestCase("all", true)]
        public async Task CommandsHaveExactDistinctWireShapesAndPreservePartialFailures(string action, bool sync)
        {
            var state = new State { Json = "{\"failed-documents\":[\"second\",\"first\"]}", Status = HttpStatusCode.OK };
            var result = await Invoke(new Client(state), action, sync);
            Assert.That(result.AllSucceeded, Is.False);
            Assert.That(result.FailedDocuments, Is.EqualTo(new[] { "second", "first" }));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/trash"));
            Assert.That(state.Request.Method, Is.EqualTo(action == "all" ? Method.Delete : Method.Post));
            if (action == "all") Assert.That(state.Body, Is.Null, "Whole-trash deletion is a distinct bodyless DELETE.");
            else
            {
                var body = JObject.Parse(state.Body);
                Assert.That(body.Count, Is.EqualTo(2));
                Assert.That((string)body["action"], Is.EqualTo(action == "restore" ? "restore-documents" : "erase-documents"));
                Assert.That(body["params"].Type, Is.EqualTo(JTokenType.Object));
                Assert.That(body["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "first", "second" }));
            }
            Assert.That(state.Calls, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>((Action)(() => { ((IList<string>)result.FailedDocuments).Clear(); }));
        }

        [TestCase("restore"), TestCase("erase"), TestCase("all")]
        public async Task NoContentMeansCompleteServerReportedSuccess(string action)
        {
            var state = new State { Status = HttpStatusCode.NoContent };
            Assert.That((await Invoke(new Client(state), action)).AllSucceeded, Is.True);
        }

        [TestCase("restore"), TestCase("erase")]
        public async Task SelectionIsSnapshottedOnceBeforeAuthorization(string action)
        {
            var ids = new[] { "first", "second" };
            var count = 0;
            IEnumerable<string> Read() { count++; foreach (var id in ids) yield return id; }
            var state = new State { Status = HttpStatusCode.NoContent, BeforeAuthorize = () => ids[0] = "changed" };
            var client = new Client(state);
            if (action == "restore") await client.RestoreDocumentsAsync("context", Read());
            else await client.EraseDocumentsAsync("context", Read());
            Assert.That(count, Is.EqualTo(1));
            Assert.That(JObject.Parse(state.Body)["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "first", "second" }));
        }

        [TestCase("restore", "null"), TestCase("restore", "empty"), TestCase("restore", "blank")]
        [TestCase("erase", "null"), TestCase("erase", "empty"), TestCase("erase", "blank")]
        public void InvalidSelectedIdsRejectBeforeAuthorization(string action, string problem)
        {
            var state = new State();
            var client = new Client(state);
            var ids = problem == "null" ? null : problem == "empty" ? new string[0] : new[] { " " };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () =>
            {
                if (action == "restore") await client.RestoreDocumentsAsync("context", ids);
                else await client.EraseDocumentsAsync("context", ids);
            }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
        }

        [TestCase("restore", "{}"), TestCase("erase", "{}"), TestCase("all", "{}")]
        [TestCase("restore", "{\"failed-documents\":[]}"), TestCase("erase", "{\"failed-documents\":[4]}"), TestCase("all", "{\"failed-documents\":[null]}")]
        [TestCase("restore", "{\"failed-documents\":[\"unselected\"]}"), TestCase("erase", "{\"failed-documents\":[\"unselected\"]}")]
        public void MalformedPartialSuccessDoesNotBecomeCompleteSuccessOrReplay(string action, string json)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = json };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await Invoke(new Client(state), action); }));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test, Combinatorial]
        public void ErrorsPreserveOriginalCauseAndDoNotReplay([Values("restore", "erase", "all", "get")] string action, [Values(403, 404, 503)] int status)
        {
            var original = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = original };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () =>
            {
                if (action == "get") await new Client(state).GetDocumentsAsync("context");
                else await Invoke(new Client(state), action);
            }));
            Assert.That(error.InnerException, Is.SameAs(original));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("restore"), TestCase("erase"), TestCase("all"), TestCase("get")]
        public void PreCancellationAvoidsAuthorizationAndDispatch(string action)
        {
            var state = new State();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () =>
                {
                    if (action == "get") await new Client(state).GetDocumentsAsync("context", cancellationToken: cancellation.Token);
                    else await Invoke(new Client(state), action, token: cancellation.Token);
                }));
            }
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("restore"), TestCase("erase"), TestCase("all"), TestCase("get")]
        public async Task ActiveCancellationReachesAuthorizationAndHttpSend(string action)
        {
            var state = new State { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                Task operation = action == "get" ? (Task)new Client(state).GetDocumentsAsync("context", cancellationToken: cancellation.Token)
                    : Invoke(new Client(state), action, token: cancellation.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await operation; }));
            }
        }

        private static string Query(RestRequest request, string name) => request.Parameters.Single(parameter => parameter.Type == ParameterType.QueryString && parameter.Name == name).Value.ToString();
        private static Task<DocumentCommandResult> Invoke(Client client, string action, bool sync = false, CancellationToken token = default(CancellationToken))
        {
            var ids = new[] { "first", "second" };
            if (action == "all") return sync ? Task.FromResult(client.EraseAllDocuments("context", token)) : client.EraseAllDocumentsAsync("context", token);
            if (action == "restore") return sync ? Task.FromResult(client.RestoreDocuments("context", ids, token)) : client.RestoreDocumentsAsync("context", ids, token);
            return sync ? Task.FromResult(client.EraseDocuments("context", ids, token)) : client.EraseDocumentsAsync("context", ids, token);
        }
        [TestCase("get"), TestCase("erase"), TestCase("all")]
        public async Task ActualLoopbackParsesUtf8MetadataAndDoesNotReplayTrashWrites(string action)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string capturedHeader = null, capturedBody = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>();
                        var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException();
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        capturedHeader = Encoding.ASCII.GetString(bytes.ToArray());
                        var lengthHeader = capturedHeader.Split(new[] { "\r\n" }, StringSplitOptions.None).SingleOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var body = new byte[lengthHeader == null ? 0 : int.Parse(lengthHeader.Substring(lengthHeader.IndexOf(':') + 1).Trim())];
                        for (var offset = 0; offset < body.Length;)
                        {
                            var read = await stream.ReadAsync(body, offset, body.Length - offset, cancellation.Token);
                            if (read == 0) throw new EndOfStreamException();
                            offset += read;
                        }
                        capturedBody = Encoding.UTF8.GetString(body);
                        var json = action == "get" ? "{\"documents\":[{\"id\":\"owned\",\"filename\":\"ä.txt\",\"mimetype\":\"text/plain\",\"deletion-date\":\"2026-10-12T08:09:10Z\"}]}" : "{}";
                        var payload = Encoding.UTF8.GetBytes(json);
                        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + (action == "get" ? "200 OK" : "503 Service Unavailable") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, 0, header.Length, cancellation.Token);
                        await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Trash;
                    client.DisableOfflineModeSimulation();
                    if (action == "get")
                    {
                        var page = await client.GetDocumentsAsync("context", rows: 1, cancellationToken: cancellation.Token);
                        Assert.That(page.Single().Filename, Is.EqualTo("ä.txt"));
                        Assert.That(page.Single().MimeType, Is.EqualTo("text/plain"));
                        Assert.That(page.Single().DeletionDate.HasValue, Is.True);
                        Assert.That(page.Single().Size, Is.Null);
                    }
                    else Assert.CatchAsync<RestClientException>((Func<Task>)(async () =>
                    {
                        if (action == "all") await client.EraseAllDocumentsAsync("context", cancellation.Token);
                        else await client.EraseDocumentsAsync("context", new[] { "owned" }, cancellation.Token);
                    }));
                    await server;
                    Assert.That(capturedHeader.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Does.StartWith(action == "get" ? "GET /bridge/v2/trash?" : action == "all" ? "DELETE /bridge/v2/trash " : "POST /bridge/v2/trash "));
                    if (action == "erase") Assert.That((string)JObject.Parse(capturedBody)["action"], Is.EqualTo("erase-documents"));
                    else Assert.That(capturedBody, Is.Empty);
                    Assert.That(listener.Pending(), Is.False, "Uncertain trash writes must not reconnect for replay.");
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

        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-trash-tests";
        }
        private sealed class Client : TrashRestClient
        {
            private readonly State state;
            internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override async Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                await state.Send(request, token);
                return new RestResponse(request) { StatusCode = state.Status, Content = state.Json, ErrorException = state.Error };
            }
            protected override async Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                await state.Send(request, token);
                return new RestResponse<T>(request) { StatusCode = state.Status, Content = state.Json, ErrorException = state.Error,
                    Data = state.Status == HttpStatusCode.OK && state.Json != null ? JsonSerializer.Deserialize<T>(state.Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : default(T) };
            }
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal string Json, Body;
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Exception Error;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, AuthorizationCalls;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            {
                AuthorizationCalls++; AuthorizationToken = token; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
            internal async Task Send(RestRequest request, CancellationToken token)
            {
                Calls++; Request = request; Token = token;
                var body = request.Parameters.OfType<BodyParameter>().SingleOrDefault();
                Body = body == null ? null : body.Value is string text ? text : JsonSerializer.Serialize(body.Value);
                Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
            }
        }
    }
}
