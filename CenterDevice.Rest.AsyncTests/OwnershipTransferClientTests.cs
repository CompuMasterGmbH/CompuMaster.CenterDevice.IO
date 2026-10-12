using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents;
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
    public class OwnershipTransferClientTests
    {
        [TestCase("document", false), TestCase("document", true), TestCase("documents", false), TestCase("documents", true), TestCase("collection", false), TestCase("collection", true)]
        public async Task SelectedOwnershipUsesManufacturerRouteBodyAndExactUserId(string kind, bool sync)
        {
            var state = new State();
            await Invoke(state, kind, sync, new[] { "selected" }, "new owner");
            Assert.That(state.Request.Resource, Is.EqualTo(kind == "collection" ? "v2/collection/selected" : "v2/documents"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            var data = Body(state);
            Assert.That(data.Value<string>("action"), Is.EqualTo("transfer-ownership"));
            Assert.That(data["params"].Value<string>("new-owner"), Is.EqualTo("new owner"));
            Assert.That(data["params"]["documents"]?.Values<string>(), Is.EqualTo(kind == "collection" ? null : new[] { "selected" }));
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task DocumentsAreEnumeratedOnceAndSnapshotBeforeAuthorizationChangesTheCallerList()
        {
            int enumerations = 0; var ids = new List<string> { "one", "two" };
            IEnumerable<string> Values() { enumerations++; foreach (var id in ids) yield return id; }
            var state = new State { BeforeAuthorize = () => ids.Clear() };
            await new Bulk(state).TransferOwnershipAsync("context", Values(), "owner");
            Assert.That(Body(state)["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "one", "two" }));
            Assert.That(enumerations, Is.EqualTo(1));
        }

        [Test]
        public async Task CollectionEscapesTheSelectedResourceWithoutResolvingOwnerNames()
        {
            var state = new State();
            await new CollectionClient(state).TransferOwnershipAsync("context", "with space/slash", "exact-owner-id");
            Assert.That(state.Request.Resource, Is.EqualTo("v2/collection/with%20space%2Fslash"));
            Assert.That(((JObject)Body(state)["params"]).Properties().Select(property => property.Name), Is.EqualTo(new[] { "new-owner" }));
        }

        [TestCase("document", null), TestCase("documents", ""), TestCase("collection", " ")]
        public void MissingNewOwnerRejectsBeforeAuthorization(string kind, string owner)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await Invoke(state, kind, false, new[] { "selected" }, owner)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("null"), TestCase("empty"), TestCase("blank"), TestCase("null-item")]
        public void BulkSelectionMustContainExplicitUsableDocumentIds(string problem)
        {
            var state = new State();
            var ids = problem == "null" ? null : problem == "empty" ? new string[0] : new[] { problem == "blank" ? " " : null };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Bulk(state).TransferOwnershipAsync("context", ids, "owner")));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidCollectionSelectionsCannotEscapeTheSelectedRoute(string id)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new CollectionClient(state).TransferOwnershipAsync("context", id, "owner")));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(null), TestCase(""), TestCase(" ")]
        public void SingleDocumentRequiresAnExplicitIdentity(string id)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Single(state).TransferOwnershipAsync("context", id, "owner")));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("document"), TestCase("documents"), TestCase("collection")]
        public void Http200IsNotTheDeclaredOwnershipConfirmation(string kind)
        {
            var state = new State { Status = HttpStatusCode.OK };
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, kind, false, new[] { "selected" }, "owner")));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void ServerRoleAndIdentityFailuresRetainTheOriginalCauseWithoutReplay(int status, Type type)
        {
            var cause = new IOException("isolated cause");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.ThrowsAsync(type, (Func<Task>)(async () => await Invoke(state, "documents", false, new[] { "selected" }, "owner")));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public void PreCancellationDoesNotEnumerateDocumentSelectionsOrAuthorize()
        {
            IEnumerable<string> Values() { yield return Throw(); }
            string Throw() => throw new AssertionException("Pre-canceled input was enumerated.");
            var state = new State();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Bulk(state).TransferOwnershipAsync("context", Values(), "owner", source.Token)));
            }
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("document"), TestCase("documents"), TestCase("collection")]
        public async Task ActiveCancellationReachesAuthorizationAndHttpWithoutCompensatingWrites(string kind)
        {
            var state = new State { Pending = true };
            using (var source = new CancellationTokenSource())
            {
                var pending = Invoke(state, kind, false, new[] { "selected" }, "owner", source.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token));
                source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
                Assert.That(state.Calls, Is.EqualTo(1));
            }
        }

        [TestCase("document", 204), TestCase("documents", 204), TestCase("collection", 204), TestCase("documents", 503)]
        public async Task ActualFactorySendsExactOwnerIdentityAndNoUncertainWriteReplay(string kind, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null, sent = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException();
                            bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        headers = Encoding.ASCII.GetString(bytes.ToArray());
                        int length = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                        var payload = new byte[length]; int count = 0;
                        while (count < length) { var read = await stream.ReadAsync(payload, count, length - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        sent = Encoding.UTF8.GetString(payload);
                        var responsePayload = Encoding.UTF8.GetBytes(status == 503 ? "{}" : "");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + responsePayload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                        if (responsePayload.Length > 0) await stream.WriteAsync(responsePayload, 0, responsePayload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var doc = factory.Document; var docs = factory.Documents; var collection = factory.Collection;
                    doc.DisableOfflineModeSimulation(); docs.DisableOfflineModeSimulation(); collection.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (kind == "document") await doc.TransferOwnershipAsync("context", "selected", "owner", source.Token);
                        else if (kind == "documents") await docs.TransferOwnershipAsync("context", new[] { "selected", "other" }, "owner", source.Token);
                        else await collection.TransferOwnershipAsync("context", "selected", "owner", source.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(kind == "collection" ? "POST /bridge/v2/collection/selected HTTP/1.1" : "POST /bridge/v2/documents HTTP/1.1"));
                    var data = JObject.Parse(sent);
                    Assert.That(data.Value<string>("action"), Is.EqualTo("transfer-ownership"));
                    Assert.That(data["params"].Value<string>("new-owner"), Is.EqualTo("owner"));
                    Assert.That(data["params"]["documents"]?.Values<string>(), Is.EqualTo(kind == "collection" ? null : kind == "document" ? new[] { "selected" } : new[] { "selected", "other" }));
                    Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static Task Invoke(State state, string kind, bool sync, string[] ids, string owner, CancellationToken token = default(CancellationToken))
        {
            if (kind == "document") { var client = new Single(state); if (sync) { client.TransferOwnership("context", ids[0], owner, token); return Task.CompletedTask; } return client.TransferOwnershipAsync("context", ids[0], owner, token); }
            if (kind == "collection") { var client = new CollectionClient(state); if (sync) { client.TransferOwnership("context", ids[0], owner, token); return Task.CompletedTask; } return client.TransferOwnershipAsync("context", ids[0], owner, token); }
            var bulk = new Bulk(state); if (sync) { bulk.TransferOwnership("context", ids, owner, token); return Task.CompletedTask; } return bulk.TransferOwnershipAsync("context", ids, owner, token);
        }
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-ownership-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(Config config) : base(new State(), config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Single : DocumentRestClient
        {
            private readonly State state;
            internal Single(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class Bulk : DocumentsRestClient
        {
            private readonly State state;
            internal Bulk(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class CollectionClient : CollectionRestClient
        {
            private readonly State state;
            internal CollectionClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.NoContent;
            internal Exception Error;
            internal string Context;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token)
            {
                Calls++; Request = request; Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = Status, Content = "{}", ErrorException = Error };
            }
        }
    }
}
