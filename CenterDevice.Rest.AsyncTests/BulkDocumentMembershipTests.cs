using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Common;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Clients.OAuth;
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
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class BulkDocumentMembershipTests
    {
        [TestCase("collection-add", false), TestCase("collection-add", true), TestCase("collection-remove", false), TestCase("collection-remove", true)]
        [TestCase("folder-add", false), TestCase("folder-add", true), TestCase("folder-remove", false), TestCase("folder-remove", true)]
        public async Task ExplicitOriginalIdsAndActionsPreserveReferencesWithoutExtraWrites(string operation, bool sync)
        {
            var documents = new[] { "one", "two", "one" }; var state = new State { BeforeAuthorize = () => documents[0] = "mutated" };
            var result = await Invoke(state, operation, sync, documents, "part/with?query"); var body = Body(state);
            Assert.That(body.Value<string>("action"), Is.EqualTo(operation.EndsWith("add") ? "add-documents" : "remove-documents"));
            Assert.That(body["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "one", "two", "one" }));
            Assert.That(((JObject)body["params"]).Properties().Select(p => p.Name), Is.EqualTo(new[] { "documents" }));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/" + (operation.StartsWith("collection") ? "collection" : "folder") + "/part%2Fwith%3Fquery"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
            Assert.That(result.AllSucceeded, Is.True); Assert.That(result.HasFailedDocuments, Is.False); Assert.That(result.FailedDocuments, Is.Empty);
        }

        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task FolderRemovalDistinguishesDefaultCollectionRetentionAndExplicitChoice(bool? remove)
        {
            var state = new State(); await Invoke(state, "folder-remove", remove: remove); var parameters = (JObject)Body(state)["params"];
            Assert.That(parameters.Property("remove-from-collection") != null, Is.EqualTo(remove.HasValue)); if (remove.HasValue) Assert.That(parameters.Value<bool>("remove-from-collection"), Is.EqualTo(remove.Value));
        }

        [TestCase("collection-add"), TestCase("collection-remove"), TestCase("folder-remove")]
        public async Task PartialFailuresRemainOriginalImmutableAndDoNotReplay(string operation)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = "{\"failed-documents\":[\"two\",\"two\"]}" }; var result = await Invoke(state, operation, documents: new[] { "one", "two" }, remove: true);
            Assert.That(result.FailedDocuments, Is.EqualTo(new[] { "two", "two" })); Assert.That(result.AllSucceeded, Is.False); Assert.That(result.HasFailedDocuments, Is.True); Assert.That(((ICollection<string>)result.FailedDocuments).IsReadOnly, Is.True); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PartialFailureGuidEquivalenceRetainsOriginalServerSpelling()
        {
            var state = new State { Status = HttpStatusCode.OK, Json = "{\"failed-documents\":[\"550E8400-E29B-41D4-A716-446655440000\"]}" };
            Assert.That((await Invoke(state, "collection-remove", documents: new[] { "550e8400-e29b-41d4-a716-446655440000" })).FailedDocuments.Single(), Is.EqualTo("550E8400-E29B-41D4-A716-446655440000"));
        }

        [TestCase("{}"), TestCase("{\"failed-documents\":null}"), TestCase("{\"failed-documents\":{}}"), TestCase("{\"failed-documents\":[null]}"), TestCase("{\"failed-documents\":[1]}")]
        [TestCase("{\"failed-documents\":[\"foreign\"]}"), TestCase("{\"failed-documents\":[\" \"]}"), TestCase("{\"failed-documents\":[],\"failed-documents\":[]}"), TestCase("{\"failed-documents\":[]} {}")]
        public void MalformedOrUnselectedFailureMetadataRetainsAnInnerDiagnostic(string json)
        { var state = new State { Status = HttpStatusCode.OK, Json = json }; Assert.That(Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await Invoke(state, "collection-add"))).InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase("folder-add", null), TestCase("folder-remove", null), TestCase("folder-remove", false)]
        public void UndocumentedFolderPartialResponsesDoNotBecomeSuccess(string operation, bool? remove)
        { Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(new State { Status = HttpStatusCode.OK, Json = "{\"failed-documents\":[]}" }, operation, remove: remove))); }

        [TestCase("collection-add", 403), TestCase("collection-remove", 404), TestCase("folder-add", 403), TestCase("folder-remove", 500)]
        public void HttpFailuresDoNotReplayOrBecomeSuccessfulMembership(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void UnusableTargetIdentitiesFailBeforeAuthorization(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await Invoke(state, "collection-add", id: id))); Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase("null"), TestCase("empty"), TestCase("null-entry"), TestCase("blank")]
        public void UnusableExplicitDocumentSelectionsFailBeforeAuthorization(string kind)
        {
            var documents = kind == "null" ? null : kind == "empty" ? Array.Empty<string>() : new[] { kind == "null-entry" ? null : " " }; var state = new State();
            Func<Task> action = async () => await Invoke(state, "folder-add", documents: documents, useDefaultDocuments: false);
            if (kind == "null") Assert.ThrowsAsync<ArgumentNullException>(action); else Assert.ThrowsAsync<ArgumentException>(action); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("collection-add"), TestCase("collection-remove"), TestCase("folder-add"), TestCase("folder-remove")]
        public void PreCancellationDoesNotAuthorizeOrSend(string operation)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, token: source.Token))); } Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase("collection-add"), TestCase("collection-remove"), TestCase("folder-add"), TestCase("folder-remove")]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp(string operation)
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var pending = Invoke(state, operation, token: source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); } }

        [TestCase("collection-add"), TestCase("collection-remove"), TestCase("folder-add"), TestCase("folder-remove")]
        public async Task FactorySendsOriginalMembershipActionsOverPrivateTcpLoopback(string operation)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null, sent = null; var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1]; while (bytes.Count < 32768) { if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        headers = Encoding.ASCII.GetString(bytes.ToArray()); var size = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]); var body = new byte[size]; var count = 0;
                        while (count < size) { var read = await stream.ReadAsync(body, count, size - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; } sent = Encoding.UTF8.GetString(body);
                        var payload = operation == "folder-add" ? Array.Empty<byte>() : Encoding.UTF8.GetBytes("{\"failed-documents\":[\"two\"]}"); var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + (payload.Length == 0 ? "204 No Content" : "200 OK") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n"); await stream.WriteAsync(reply, 0, reply.Length, source.Token); if (payload.Length > 0) await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")); DocumentMembershipResult result;
                    if (operation.StartsWith("collection")) { var client = factory.Collection; client.DisableOfflineModeSimulation(); result = operation.EndsWith("add") ? await client.AddDocumentsToCollectionAsync("context", "selected", new[] { "one", "two" }, source.Token) : await client.RemoveDocumentsFromCollectionAsync("context", "selected", new[] { "one", "two" }, source.Token); }
                    else { var client = factory.Folder; client.DisableOfflineModeSimulation(); result = operation.EndsWith("add") ? await client.AddDocumentsAsync("context", "selected", new[] { "one", "two" }, source.Token) : await client.RemoveDocumentsAsync("context", "selected", new[] { "one", "two" }, true, source.Token); }
                    await server; Assert.That(result.AllSucceeded, Is.EqualTo(operation == "folder-add")); Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/" + (operation.StartsWith("collection") ? "collection" : "folder") + "/selected HTTP/1.1"));
                    var json = JObject.Parse(sent); Assert.That(json.Value<string>("action"), Is.EqualTo(operation.EndsWith("add") ? "add-documents" : "remove-documents")); Assert.That(json["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "one", "two" }));
                    if (operation == "folder-remove") Assert.That(json["params"].Value<bool>("remove-from-collection"), Is.True); else Assert.That(((JObject)json["params"]).Property("remove-from-collection"), Is.Null);
                    Assert.That(listener.Pending(), Is.False);
                }
                finally { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private static async Task<DocumentMembershipResult> Invoke(State state, string operation, bool sync = false, string[] documents = null, string id = "selected", bool? remove = null, CancellationToken token = default(CancellationToken), bool useDefaultDocuments = true)
        {
            if (documents == null && useDefaultDocuments) documents = new[] { "one", "two" };
            if (operation.StartsWith("collection")) { var client = new CollectionClient(state); if (operation.EndsWith("add")) return sync ? client.AddDocumentsToCollection("context", id, documents, token) : await client.AddDocumentsToCollectionAsync("context", id, documents, token); return sync ? client.RemoveDocumentsFromCollection("context", id, documents, token) : await client.RemoveDocumentsFromCollectionAsync("context", id, documents, token); }
            var folder = new FolderClient(state); if (operation.EndsWith("add")) return sync ? folder.AddDocuments("context", id, documents, token) : await folder.AddDocumentsAsync("context", id, documents, token); return sync ? folder.RemoveDocuments("context", id, documents, remove, token) : await folder.RemoveDocumentsAsync("context", id, documents, remove, token);
        }
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-membership-tests"; }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class CollectionClient : CollectionRestClient
        { private readonly State state; internal CollectionClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class FolderClient : FolderRestClient
        { private readonly State state; internal FolderClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.NoContent; internal string Context, Json = "{}"; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
