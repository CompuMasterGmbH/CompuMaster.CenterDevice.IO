using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using RestSharp.Serializers.Json;
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
    public class DocumentCopyTests
    {
        [TestCase(false), TestCase(true)]
        public async Task ActualHttpCopyWireIsValidUtf8JsonAndTransientFailureIsNotReplayed(bool bulk)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string captured = null;
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var server = Task.Run(async () =>
                {
                    using (var connection = await listener.AcceptTcpClientAsync())
                    using (var stream = connection.GetStream())
                    {
                        var header = new List<byte>();
                        var next = new byte[1];
                        while (header.Count < 65536)
                        {
                            if (await stream.ReadAsync(next, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException();
                            header.Add(next[0]);
                            var count = header.Count;
                            if (count >= 4 && header[count - 4] == 13 && header[count - 3] == 10 && header[count - 2] == 13 && header[count - 1] == 10) break;
                        }
                        var text = Encoding.ASCII.GetString(header.ToArray());
                        var lengthHeader = text.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var body = new byte[int.Parse(lengthHeader.Substring(lengthHeader.IndexOf(':') + 1).Trim())];
                        for (var offset = 0; offset < body.Length;)
                        {
                            var read = await stream.ReadAsync(body, offset, body.Length - offset, cancellation.Token);
                            if (read == 0) throw new EndOfStreamException();
                            offset += read;
                        }
                        captured = Encoding.UTF8.GetString(body);
                        var response = Encoding.ASCII.GetBytes("HTTP/1.1 503 Service Unavailable\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
                        await stream.WriteAsync(response, 0, response.Length, cancellation.Token);
                    }
                });
                try
                {
                    var config = new Config("http://127.0.0.1:" + port + "/bridge/");
                    var auth = new State();
                    if (bulk)
                    {
                        var client = new DocumentsRestClient(auth, config, null, null, "v2/");
                        client.DisableOfflineModeSimulation();
                        Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await client.CopyDocumentsAsync("context", Selections(), cancellationToken: cancellation.Token); }));
                    }
                    else
                    {
                        var client = new DocumentRestClient(auth, config, null, null, "v2/");
                        client.DisableOfflineModeSimulation();
                        Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await client.CopyDocumentAsync("context", "source", "copy ü.txt", cancellationToken: cancellation.Token); }));
                    }
                    await server;
                    var body = JObject.Parse(captured);
                    Assert.That((string)body["action"], Is.EqualTo("copy"));
                    Assert.That(body["params"].Type, Is.EqualTo(JTokenType.Object));
                    Assert.That((string)(bulk ? body["params"]["documents"][0]["filename"] : body["params"]["filename"]), Is.EqualTo(bulk ? "first ü.txt" : "copy ü.txt"));
                    Assert.That(listener.Pending(), Is.False, "An uncertain copy POST must not reconnect to replay after HTTP503.");
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

        [Test, Combinatorial]
        public async Task CopyCommandsUseExactServerBodiesAndOnlyNewIdentifiers(
            [Values(false, true)] bool bulk, [Values(false, true)] bool sync, [Values("none", "collection", "folder")] string target)
        {
            var state = new State { Bulk = bulk };
            var result = await Invoke(state, bulk, sync, target == "collection" ? "destination" : null, target == "folder" ? "destination" : null);
            Assert.That(result, Is.EqualTo(bulk ? new[] { "new-second", "new-first" } : new[] { "new-first" }));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Request.Resource, Is.EqualTo(bulk ? "v2/documents" : "v2/document/source%2Fwith%3Fquery"));
            Assert.That(state.UserId, Is.EqualTo("selected-context"));
            var body = JObject.Parse(state.Json);
            Assert.That(body.Count, Is.EqualTo(2));
            Assert.That((string)body["action"], Is.EqualTo("copy"));
            var parameters = (JObject)body["params"];
            Assert.That(parameters.Count, Is.EqualTo(target == "none" ? 1 : 2));
            if (target != "none") Assert.That((string)parameters[target], Is.EqualTo("destination"));
            if (bulk)
            {
                var documents = (JArray)parameters["documents"];
                Assert.That(documents.Count, Is.EqualTo(2));
                Assert.That(documents.Select(d => (string)d["document-id"]), Is.EqualTo(new[] { "source", "source" }), "Repeated sources are separate copy selections.");
                Assert.That(documents.Select(d => (string)d["filename"]), Is.EqualTo(new[] { "first ü.txt", "second.txt" }));
                Assert.That(documents.All(d => ((JObject)d).Count == 2), Is.True, "No source metadata or sharing is inherited by the request.");
            }
            else Assert.That((string)parameters["filename"], Is.EqualTo("copy ü.txt"));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task BulkResultsAreReadOnlyAndDoNotInventASourceMapping()
        {
            var state = new State { Bulk = true };
            var result = await new BulkClient(state).CopyDocumentsAsync("context", Selections());
            Assert.That(result, Is.EqualTo(new[] { "new-second", "new-first" }));
            Assert.Throws<NotSupportedException>((Action)(() => ((IList<string>)result)[0] = "changed"));
        }

        [Test]
        public async Task SourceSequenceIsEnumeratedOnceAndSnapshottedBeforeAuthorization()
        {
            var state = new State { Bulk = true };
            var sources = Selections();
            var enumerations = 0;
            IEnumerable<DocumentCopyRequest> Read()
            {
                enumerations++;
                foreach (var source in sources) yield return source;
            }
            state.BeforeAuthorize = () => sources[0] = new DocumentCopyRequest("changed", "changed.txt");
            await new BulkClient(state).CopyDocumentsAsync("context", Read());
            Assert.That(enumerations, Is.EqualTo(1));
            Assert.That((string)JObject.Parse(state.Json)["params"]["documents"][0]["document-id"], Is.EqualTo("source"));
        }

        [TestCase(null, "name"), TestCase("", "name"), TestCase(" ", "name"), TestCase(".", "name"), TestCase("..", "name")]
        [TestCase("id", null), TestCase("id", ""), TestCase("id", " ")]
        public void ImmutableSelectionsRejectMissingIdentityOrFilename(string id, string filename)
            => Assert.Throws<ArgumentException>((Action)(() => { new DocumentCopyRequest(id, filename); }));

        [TestCase(false, "both"), TestCase(true, "both")]
        [TestCase(false, "collection"), TestCase(true, "collection")]
        [TestCase(false, "folder"), TestCase(true, "folder")]
        public void InvalidTargetsFailBeforeAuthorization(bool bulk, string invalid)
        {
            var state = new State { Bulk = bulk };
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await Invoke(state, bulk, false,
                invalid == "both" ? "collection" : invalid == "collection" ? " " : null,
                invalid == "both" ? "folder" : invalid == "folder" ? " " : null); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("null"), TestCase("empty"), TestCase("null-element")]
        public void InvalidBulkSequencesFailBeforeAuthorization(string invalid)
        {
            var state = new State { Bulk = true };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new BulkClient(state).CopyDocumentsAsync("context",
                invalid == "null" ? null : invalid == "empty" ? new DocumentCopyRequest[0] : new DocumentCopyRequest[] { null }); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
        }

        [TestCase(false, "{}"), TestCase(false, "{\"id\":null}"), TestCase(false, "{\"id\":4}")]
        [TestCase(false, "{\"id\":\"source/with?query\"}"), TestCase(false, "broken")]
        [TestCase(true, "{}"), TestCase(true, "{\"documents\":null}"), TestCase(true, "{\"documents\":[]}")]
        [TestCase(true, "{\"documents\":[4]}"), TestCase(true, "{\"documents\":[\"\"]}")]
        [TestCase(true, "{\"documents\":[\"source\"]}"), TestCase(true, "{\"documents\":[\"new\",\"new\"]}")]
        [TestCase(true, "{\"documents\":[\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\",\"AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE\"]}")]
        public void MalformedOrSourceReusingResultsAreRejectedWithoutAnotherCopyAttempt(bool bulk, string json)
        {
            var state = new State { Bulk = bulk, JsonOverride = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await Invoke(state, bulk); }));
            Assert.That(error.InnerException, Is.Not.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false, 403), TestCase(true, 403), TestCase(false, 404), TestCase(true, 404), TestCase(false, 503), TestCase(true, 503)]
        public void ServerErrorsRetainOriginalCauseAndAreNotReplayed(bool bulk, int status)
        {
            var original = new InvalidOperationException("isolated failure");
            var state = new State { Bulk = bulk, StatusOverride = (HttpStatusCode)status, Error = original };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await Invoke(state, bulk); }));
            Assert.That(error.InnerException, Is.SameAs(original));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void PreCancellationAvoidsAuthorizationAndCopyDispatch(bool bulk)
        {
            var state = new State { Bulk = bulk };
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await Invoke(state, bulk, token: cancellation.Token); }));
            }
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public async Task ActiveCancellationIsPassedToAuthorizationAndNativeSend(bool bulk)
        {
            var state = new State { Bulk = bulk, Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                var task = Invoke(state, bulk, token: cancellation.Token);
                await state.Entered.Task;
                Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await task; }));
            }
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        private static DocumentCopyRequest[] Selections() => new[] { new DocumentCopyRequest("source", "first ü.txt"), new DocumentCopyRequest("source", "second.txt") };
        private static async Task<string[]> Invoke(State state, bool bulk, bool sync = false, string collectionId = null, string folderId = null, CancellationToken token = default(CancellationToken))
        {
            if (bulk)
            {
                var client = new BulkClient(state);
                var ids = sync ? client.CopyDocuments("selected-context", Selections(), collectionId, folderId, token) : await client.CopyDocumentsAsync("selected-context", Selections(), collectionId, folderId, token);
                return ids.ToArray();
            }
            var selected = new SingleClient(state);
            return new[] { sync ? selected.CopyDocument("selected-context", "source/with?query", "copy ü.txt", collectionId, folderId, token)
                : await selected.CopyDocumentAsync("selected-context", "source/with?query", "copy ü.txt", collectionId, folderId, token) };
        }
        private sealed class Config : IRestClientConfiguration
        {
            private readonly string address;
            internal Config(string address = "https://unused.test/bridge/") { this.address = address; }
            public string BaseAddress => address;
            public string UserAgent => "server-copy-tests";
        }
        private sealed class SingleClient : DocumentRestClient
        {
            private readonly State state;
            internal SingleClient(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Execute(info, request, token);
        }
        private sealed class BulkClient : DocumentsRestClient
        {
            private readonly State state;
            internal BulkClient(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Execute(info, request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal bool Bulk, Pending;
            internal string Json, JsonOverride, UserId;
            internal HttpStatusCode? StatusOverride;
            internal Exception Error;
            internal RestRequest Request;
            internal int Calls, AuthorizationCalls;
            internal Action BeforeAuthorize;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native async authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                AuthorizationCalls++; AuthorizationToken = cancellationToken; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
            internal async Task<RestResponse> Execute(OAuthInfo info, RestRequest request, CancellationToken token)
            {
                Calls++; Request = request; Token = token; UserId = info.UserId;
                var body = request.Parameters.OfType<BodyParameter>().Single();
                Json = body.Value is string text ? text : new SystemTextJsonSerializer().Serialize(body.Value);
                Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = StatusOverride ?? (Bulk ? HttpStatusCode.OK : HttpStatusCode.Created), ErrorException = Error,
                    Content = JsonOverride ?? (Bulk ? "{\"documents\":[\"new-second\",\"new-first\"]}" : "{\"id\":\"new-first\"}") };
            }
        }
    }
}
