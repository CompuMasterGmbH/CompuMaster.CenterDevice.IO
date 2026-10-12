using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Rules;
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
    public class RuleClientTests
    {
        private static RuleCreateRequest Definition() => new RuleCreateRequest { Description = "Ä rule",
            Scope = new RuleComponent("collection", new JObject { ["id"] = "collection", ["include-subfolders"] = false }),
            Trigger = new RuleComponent("document-added-to-collection", new JObject()),
            Actions = new List<RuleComponent> { new RuleComponent("add-tags", new JObject { ["tags"] = new JArray("tag") }) } };
        private static JObject Record(string id = "rule") => new JObject { ["id"] = id, ["description"] = "rule", ["type"] = "user", ["status"] = "active", ["creation-date"] = "2026-10-12T12:30:00+02:00",
            ["scope"] = new JObject { ["type"] = "collection", ["data"] = new JObject { ["id"] = "collection" } },
            ["trigger"] = new JObject { ["type"] = "document-added-to-collection", ["data"] = new JObject() },
            ["actions"] = new JArray(new JObject { ["type"] = "add-tags", ["data"] = new JObject { ["tags"] = new JArray("tag") } }) };

        [TestCase(false), TestCase(true)]
        public async Task CreateSnapshotsWholeDefinitionBeforeAuthorizationWithoutChangingDefaults(bool sync)
        {
            var input = Definition(); var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}", BeforeAuthorize = () => { input.Description = "changed"; input.Actions.Clear(); input.Scope = null; } };
            var client = new Client(state);
            var id = sync ? client.CreateRule("context", input) : await client.CreateRuleAsync("context", input);
            Assert.That(id, Is.EqualTo("created")); Assert.That(Body(state).Value<string>("description"), Is.EqualTo("Ä rule"));
            Assert.That(Body(state).Value<string>("type"), Is.EqualTo("user"));
            Assert.That(Body(state)["scope"]["data"].Value<bool>("include-subfolders"), Is.False);
            Assert.That(Body(state)["trigger"].Value<string>("type"), Is.EqualTo("document-added-to-collection"));
            Assert.That(Body(state)["actions"][0]["data"]["tags"].Values<string>(), Is.EqualTo(new[] { "tag" }));
            Assert.That(Body(state)["status"], Is.Null); Assert.That(Body(state)["creator"], Is.Null);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/rules")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase("add-tags"), TestCase("remove-tags"), TestCase("set-extended-metadata"), TestCase("remove-extended-metadata"), TestCase("add-to-collection"), TestCase("add-to-folder"), TestCase("move-to-collection"), TestCase("move-to-folder"), TestCase("add-workflow"), TestCase("split-by-marker")]
        public async Task AllCurrentActionDataRetainsCompleteNestedPayloadWithoutInventedValidation(string type)
        {
            var payload = new JObject { ["future"] = new JObject { ["size"] = 5368709120L, ["date"] = "2026-10-12T12:00:00" },
                ["metadata"] = new JArray(new JObject { ["key"] = "user.numbers.amount", ["value"] = "1.2300" }), ["marker-types"] = new JArray("QR_CODE"), ["keep-tags"] = false, ["workflow-creator"] = "rule-triggerer" };
            var action = new RuleComponent(type, payload); payload["keep-tags"] = true;
            var copy = action.Data; copy["future"]["size"] = 0;
            var request = Definition(); request.Actions = new List<RuleComponent> { action };
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" };
            await new Client(state).CreateRuleAsync("context", request);
            Assert.That(Body(state)["actions"][0].Value<string>("type"), Is.EqualTo(type));
            Assert.That(Body(state)["actions"][0]["data"]["future"].Value<long>("size"), Is.EqualTo(5368709120L));
            Assert.That(Body(state)["actions"][0]["data"].Value<bool>("keep-tags"), Is.False);
            Assert.That(Body(state)["actions"][0]["data"]["metadata"][0].Value<string>("value"), Is.EqualTo("1.2300"));
            Assert.That(Body(state)["actions"][0]["data"]["future"].Value<string>("date"), Is.EqualTo("2026-10-12T12:00:00"));
        }

        [TestCase(false), TestCase(true)]
        public async Task OwnReadRetainsUnknownStatusReasonActionsAndDefensiveData(bool sync)
        {
            var data = Record(); data["status"] = "future-status"; data["deactivation-reason"] = "future-reason"; data["actions"][0]["type"] = "future-action";
            var state = new State { Json = data.ToString() }; var client = new Client(state);
            var result = sync ? client.GetRule("context", "rule") : await client.GetRuleAsync("context", "rule");
            Assert.That(result.Status, Is.EqualTo("future-status")); Assert.That(result.DeactivationReason, Is.EqualTo("future-reason"));
            Assert.That(result.Actions[0].Type, Is.EqualTo("future-action")); Assert.That(result.CreationDate.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            result.Actions[0].Data["tags"] = null;
            Assert.That(result.Actions[0].Data["tags"].Values<string>(), Is.EqualTo(new[] { "tag" }));
            Assert.That(((IList<RuleComponent>)result.Actions).IsReadOnly, Is.True);
            Assert.That(typeof(Rule).GetProperty("Owner"), Is.Null);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task AbsentSelectedRuleAndEmptyListRetainDocumented204Semantics(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            Assert.That(sync ? client.GetRule("context", "rule") : await client.GetRuleAsync("context", "rule"), Is.Null);
            Assert.That(sync ? client.GetRules("context") : await client.GetRulesAsync("context"), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task ListPreservesUniqueServerOrderWithoutInventingPaging(bool sync)
        {
            var state = new State { Json = new JObject { ["rules"] = new JArray(Record("second"), Record("first")) }.ToString() }; var client = new Client(state);
            var result = sync ? client.GetRules("context") : await client.GetRulesAsync("context");
            Assert.That(result.Select(rule => rule.Id), Is.EqualTo(new[] { "second", "first" }));
            Assert.That(((IList<Rule>)result).IsReadOnly, Is.True);
            Assert.That(state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task UpdateOnlySendsSelectedFieldsAndCannotChangeRuleType(bool sync)
        {
            var input = new RuleUpdateRequest { Description = "" };
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (sync) client.UpdateRule("context", "rule", input); else await client.UpdateRuleAsync("context", "rule", input);
            Assert.That(Body(state).Properties().Select(property => property.Name), Is.EqualTo(new[] { "description" }));
            Assert.That(Body(state).Value<string>("description"), Is.EqualTo("")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(typeof(RuleUpdateRequest).GetProperty("Type"), Is.Null);
        }

        [TestCase("activate", false), TestCase("activate", true), TestCase("deactivate", false), TestCase("deactivate", true), TestCase("remove", false), TestCase("remove", true)]
        public async Task ActivationCommandsAreDistinctAndRemovalIsBodyless(string operation, bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (operation == "activate") { if (sync) client.ActivateRule("context", "id with/slash"); else await client.ActivateRuleAsync("context", "id with/slash"); }
            else if (operation == "deactivate") { if (sync) client.DeactivateRule("context", "id with/slash"); else await client.DeactivateRuleAsync("context", "id with/slash"); }
            else { if (sync) client.RemoveRule("context", "id with/slash"); else await client.RemoveRuleAsync("context", "id with/slash"); }
            Assert.That(state.Request.Resource, Is.EqualTo("v2/rule/id%20with%2Fslash"));
            Assert.That(state.Request.Method, Is.EqualTo(operation == "remove" ? Method.Delete : Method.Post));
            if (operation == "remove") Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            else { Assert.That(Body(state).Value<string>("action"), Is.EqualTo(operation)); Assert.That(Body(state)["params"], Is.Null); }
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("scope"), TestCase("trigger"), TestCase("actions"), TestCase("unknown-action"), TestCase("description"), TestCase("scope-type"), TestCase("scope-id"), TestCase("scope-deep")]
        public void InvalidDefinitionFailsBeforeAuthorization(string problem)
        {
            var input = Definition();
            if (problem == "scope") input.Scope = null;
            if (problem == "trigger") input.Trigger = new RuleComponent("document-added-to-folder", new JObject());
            if (problem == "actions") input.Actions.Clear();
            if (problem == "unknown-action") input.Actions[0] = new RuleComponent("unknown", new JObject());
            if (problem == "description") input.Description = null;
            if (problem.StartsWith("scope-")) input.Scope = new RuleComponent(problem == "scope-type" ? "tenant" : "collection", new JObject { ["id"] = problem == "scope-id" ? " " : "collection", ["include-subfolders"] = problem == "scope-deep" ? (JToken)"true" : (JToken)true });
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).CreateRuleAsync("context", input); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("empty-update"), TestCase("null-update"), TestCase("empty-actions"), TestCase("id")]
        public void InvalidUpdatesAndSelectionsDoNotReachAuthorization(string problem)
        {
            var state = new State(); var input = problem == "null-update" ? null : new RuleUpdateRequest { Actions = problem == "empty-actions" ? new List<RuleComponent>() : null };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new Client(state).UpdateRuleAsync("context", problem == "id" ? " " : "rule", input); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("id"), TestCase("description"), TestCase("type"), TestCase("status"), TestCase("creation-date"), TestCase("scope"), TestCase("trigger"), TestCase("actions"), TestCase("deactivation-reason")]
        public void MalformedSuccessfulMetadataRetainsOriginalCause(string field)
        {
            var data = Record(); data[field] = 1;
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = data.ToString() }).GetRuleAsync("context", "rule"); }));
            Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":null}"), TestCase("{\"id\":\"created\"} {}")]
        public void InvalidCreationDoesNotInventIdentityOrReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(state).CreateRuleAsync("context", Definition()); }));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task OffsetFreeCreationTextAndCanonicalIdentityRemainOriginal()
        {
            var id = Guid.NewGuid(); var data = Record(id.ToString("B").ToUpperInvariant()); data["creation-date"] = "2026-10-12T12:30:00";
            var result = await new Client(new State { Json = data.ToString() }).GetRuleAsync("context", id.ToString("D"));
            Assert.That(result.CreationDate, Is.Null); Assert.That(result.CreationDateText, Is.EqualTo("2026-10-12T12:30:00")); Assert.That(result.DeactivationReason, Is.Null);
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = data.ToString() }).GetRuleAsync("context", "foreign"); }));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new Client(new State { Json = new JObject { ["rules"] = new JArray(data, Record(id.ToString("D"))) }.ToString() }).GetRulesAsync("context"); }));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void CreatorGuestStateAndUncertainWriteFailuresRemainOriginal(int status)
        {
            var cause = new InvalidOperationException("isolated failure"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new Client(state).ActivateRuleAsync("context", "rule"); }));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeIo()
        {
            var state = new State { Pending = true };
            using (var token = new CancellationTokenSource())
            {
                token.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new Client(state).RemoveRuleAsync("context", "rule", token.Token); }));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var token = new CancellationTokenSource())
            {
                var pending = new Client(state).GetRulesAsync("context", token.Token); await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(token.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(token.Token));
                token.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await pending; }));
            }
        }

        [TestCase("create", 201), TestCase("create", 503), TestCase("get", 200), TestCase("get", 204), TestCase("get", 503), TestCase("list", 200), TestCase("list", 204), TestCase("update", 204), TestCase("update", 503), TestCase("activate", 204), TestCase("deactivate", 204), TestCase("remove", 204), TestCase("remove", 503)]
        public async Task PublicFactoryPreservesAllWireRoutesAndAbsenceWithoutUncertainMutationReplay(string operation, int status)
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
                        var response = status == 503 ? "{}" : status == 204 ? "" : operation == "create" ? "{\"id\":\"created\"}" : operation == "get" ? Record().ToString() : new JObject { ["rules"] = new JArray(Record()) }.ToString();
                        var payload = Encoding.UTF8.GetBytes(response);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 201 ? " Created" : status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Rules;
                    client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "create") Assert.That(await client.CreateRuleAsync("context", Definition(), cancellation.Token), Is.EqualTo("created"));
                        else if (operation == "get") Assert.That((await client.GetRuleAsync("context", "rule", cancellation.Token))?.Id, Is.EqualTo(status == 204 ? null : "rule"));
                        else if (operation == "list") Assert.That((await client.GetRulesAsync("context", cancellation.Token)).Count, Is.EqualTo(status == 204 ? 0 : 1));
                        else if (operation == "update") await client.UpdateRuleAsync("context", "rule", new RuleUpdateRequest { Description = "ä update" }, cancellation.Token);
                        else if (operation == "activate") await client.ActivateRuleAsync("context", "rule", cancellation.Token);
                        else if (operation == "deactivate") await client.DeactivateRuleAsync("context", "rule", cancellation.Token);
                        else await client.RemoveRuleAsync("context", "rule", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "create" ? "POST /bridge/v2/rules" : operation == "list" ? "GET /bridge/v2/rules" :
                        (operation == "get" ? "GET" : operation == "update" ? "PUT" : operation == "remove" ? "DELETE" : "POST") + " /bridge/v2/rule/rule";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    if (operation == "create") Assert.That(JObject.Parse(sent)["actions"][0]["data"]["tags"].Values<string>(), Is.EqualTo(new[] { "tag" }));
                    else if (operation == "update") Assert.That(JObject.Parse(sent).Properties().Select(property => property.Name), Is.EqualTo(new[] { "description" }));
                    else if (operation == "activate" || operation == "deactivate") { Assert.That(JObject.Parse(sent).Value<string>("action"), Is.EqualTo(operation)); Assert.That(JObject.Parse(sent)["params"], Is.Null); }
                    else Assert.That(sent, Is.Empty);
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
        { using (var text = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value)) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private sealed class Client : RulesRestClient
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
            public string UserAgent => "isolated-rule-tests";
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
