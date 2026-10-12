using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Workflows;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
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
    public class WorkflowCommandClientTests
    {
        private static Task Invoke(Client client, string operation, CancellationToken token = default(CancellationToken), bool sync = false, string id = "workflow")
        {
            if (sync)
            {
                if (operation == "assign") client.AssignToWorkflow("context", id, token);
                else if (operation == "unassign") client.UnassignFromWorkflow("context", id, token);
                else if (operation == "respond") client.RespondToWorkflow("context", id, "confirmed", long.MaxValue, cancellationToken: token);
                else client.RemoveWorkflow("context", id, token);
                return Task.CompletedTask;
            }
            return operation == "assign" ? client.AssignToWorkflowAsync("context", id, token) : operation == "unassign" ? client.UnassignFromWorkflowAsync("context", id, token) :
                operation == "respond" ? client.RespondToWorkflowAsync("context", id, "confirmed", long.MaxValue, cancellationToken: token) : client.RemoveWorkflowAsync("context", id, token);
        }

        [TestCase("assign", false), TestCase("assign", true), TestCase("unassign", false), TestCase("unassign", true), TestCase("respond", false), TestCase("respond", true), TestCase("remove", false), TestCase("remove", true)]
        public async Task SelectedCommandsPreserveSyncNativeWireIdentityAndContext(string operation, bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; await Invoke(new Client(state), operation, sync: sync, id: "id with/slash");
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Request.Resource, Is.EqualTo("v2/workflow/id%20with%2Fslash")); Assert.That(state.Calls, Is.EqualTo(1));
            Assert.That(state.Request.Method, Is.EqualTo(operation == "remove" ? Method.Delete : Method.Post));
            if (operation == "remove") Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            else
            {
                var body = Body(state); Assert.That(body.Value<string>("action"), Is.EqualTo(operation)); Assert.That(body["data"], Is.Null);
                if (operation == "respond") { Assert.That(body["params"].Value<long>("document-version"), Is.EqualTo(long.MaxValue)); Assert.That(body["params"].Value<string>("result"), Is.EqualTo("confirmed")); Assert.That(body["params"]["comment"], Is.Null); }
                else Assert.That(body.Properties().Select(property => property.Name), Is.EqualTo(new[] { "action" }));
            }
        }

        [TestCase("confirmed", null), TestCase("confirmed", ""), TestCase("rejected", "ä notification\noriginal")]
        public async Task ResponsePreservesOriginalCommentAndDoesNotGuessWorkflowTypeOrState(string result, string comment)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; await new Client(state).RespondToWorkflowAsync("context", "workflow", result, 123, comment);
            var parameters = Body(state)["params"]; Assert.That(parameters.Value<string>("result"), Is.EqualTo(result)); Assert.That(parameters.Value<long>("document-version"), Is.EqualTo(123));
            if (comment == null) Assert.That(parameters["comment"], Is.Null); else Assert.That(parameters.Value<string>("comment"), Is.EqualTo(comment));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(null, 1L), TestCase("future", 1L), TestCase("CONFIRMED", 1L), TestCase("confirmed", 0L), TestCase("rejected", -1L)]
        public void InvalidCurrentResponseFailsBeforeAuthorization(string result, long version)
        { var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).RespondToWorkflowAsync("context", "workflow", result, version))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase("assign"), TestCase("unassign"), TestCase("respond"), TestCase("remove")]
        public void UnusableSelectedIdentityFailsBeforeAuthorization(string operation)
        {
            foreach (var id in new[] { null, "", " ", ".", ".." })
            { var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await Invoke(new Client(state), operation, id: id))); Assert.That(state.Authorizations, Is.Zero); }
        }

        [TestCase("assign", 403), TestCase("unassign", 404), TestCase("respond", 400), TestCase("respond", 403), TestCase("respond", 503), TestCase("remove", 403), TestCase("remove", 503)]
        public void ServerVisibilityParticipationAndCompletionFailuresPreserveCauseWithoutReplay(string operation, int status)
        {
            var cause = new InvalidOperationException("original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(new Client(state), operation)));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("assign", 200), TestCase("unassign", 201), TestCase("respond", 202), TestCase("remove", 200)]
        public void UnexpectedSuccessStatusDoesNotBecomeConfirmedCommand(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(new Client(state), operation))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase("assign"), TestCase("unassign"), TestCase("respond"), TestCase("remove")]
        public async Task PreAndActiveCancellationReachAuthorizationAndIo(string operation)
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = Invoke(new Client(state), operation, cancellation.Token); await state.Entered.Task;
                cancellation.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
                Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(new Client(state), operation, cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }

        [TestCase("assign", 204), TestCase("assign", 503), TestCase("unassign", 204), TestCase("unassign", 503), TestCase("respond", 204), TestCase("respond", 503), TestCase("remove", 204), TestCase("remove", 503)]
        public async Task PublicFactoryVerifiesActualWireWithoutMutationReplay(string operation, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            {
                string header = null, sent = null;
                var server = Task.Run(async () =>
                {
                    using (var connection = await listener.AcceptTcpClientAsync()) using (var stream = connection.GetStream())
                    {
                        var bytes = new System.Collections.Generic.List<byte>(); var one = new byte[1];
                        while (true)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        header = Encoding.ASCII.GetString(bytes.ToArray()); var lengthHeader = header.Split(new[] { "\r\n" }, StringSplitOptions.None).SingleOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var length = lengthHeader == null ? 0 : int.Parse(lengthHeader.Split(':')[1].Trim()); var received = new byte[length]; var count = 0;
                        while (count < length) { var read = await stream.ReadAsync(received, count, length - count, cancellation.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        sent = Encoding.UTF8.GetString(received); var payload = Encoding.UTF8.GetBytes(status == 503 ? "{}" : "");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 204 ? " No Content" : " Service Unavailable") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token); if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Workflows; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = () => operation == "assign" ? client.AssignToWorkflowAsync("context", "workflow", cancellation.Token) : operation == "unassign" ? client.UnassignFromWorkflowAsync("context", "workflow", cancellation.Token) :
                        operation == "respond" ? client.RespondToWorkflowAsync("context", "workflow", "rejected", 123, "ä original", cancellation.Token) : client.RemoveWorkflowAsync("context", "workflow", cancellation.Token);
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke(); await server;
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo((operation == "remove" ? "DELETE" : "POST") + " /bridge/v2/workflow/workflow HTTP/1.1"));
                    if (operation == "remove") Assert.That(sent, Is.Empty);
                    else { var body = JObject.Parse(sent); Assert.That(body.Value<string>("action"), Is.EqualTo(operation)); Assert.That(body["data"], Is.Null); if (operation == "respond") Assert.That(body["params"].Value<string>("comment"), Is.EqualTo("ä original")); else Assert.That(body["params"], Is.Null); }
                    Assert.That(listener.Pending(), Is.False);
                }
                finally { listener.Stop(); }
            }
        }
        private static JObject Body(State state)
        {
            using (var text = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader);
        }
        private sealed class Client : WorkflowsRestClient
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
            public string UserAgent => "isolated-workflow-template-tests";
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
