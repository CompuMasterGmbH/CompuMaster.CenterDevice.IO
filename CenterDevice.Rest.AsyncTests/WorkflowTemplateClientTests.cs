using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.WorkflowTemplates;
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
    public class WorkflowTemplateClientTests
    {
        private static JObject Record(string id = "template") => new JObject { ["id"] = id, ["name"] = "ä template", ["type"] = "approval" };
        private static WorkflowTemplateOptions Options() => new WorkflowTemplateOptions { Name = "ä template", Type = "approval" };

        [TestCase(false), TestCase(true)]
        public async Task CreationPreservesOmittedDefaultsAndConfirmsTemplateIdentity(bool sync)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" }; var client = new Client(state);
            var result = sync ? client.CreateWorkflowTemplate("context", Options()) : await client.CreateWorkflowTemplateAsync("context", Options());
            Assert.That(result, Is.EqualTo("created")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/workflow-templates"));
            Assert.That(Body(state).Properties().Select(property => property.Name), Is.EqualTo(new[] { "name", "type" })); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase(false), TestCase(true)]
        public async Task TemplateUseHasExplicitActionDataAndReturnsDistinctWorkflowIdentity(bool sync)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"workflow\"}" }; var client = new Client(state);
            var result = sync ? client.CreateWorkflow("context", "template", "document", long.MaxValue) : await client.CreateWorkflowAsync("context", "template", "document", long.MaxValue);
            Assert.That(result, Is.EqualTo("workflow")); Assert.That(state.Request.Resource, Is.EqualTo("v2/workflow-template/template")); Assert.That(Body(state).Value<string>("action"), Is.EqualTo("workflow"));
            Assert.That(Body(state)["params"], Is.Null); Assert.That(Body(state)["data"].Value<string>("document-id"), Is.EqualTo("document")); Assert.That(Body(state)["data"].Value<long>("document-version"), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public async Task FullCreationSnapshotsSequencesAndNestedMetadataBeforeAuthorizationWithoutNormalization()
        {
            var users = new List<string> { "user", "user" }; var groups = new List<string> { "group" }; var count = 0;
            IEnumerable<WorkflowFinalizingAction> Actions() { count++; yield return WorkflowFinalizingAction.MoveToCollection("target", sourceFolderId: "source"); }
            var condition = new JObject { ["metadata-templates"] = new JArray("metadata-template"), ["metadata"] = new JArray(new JObject { ["key"] = "user.numbers.amount", ["default-value"] = "1.2300" },
                new JObject { ["key"] = "user.dates.date", ["default-value"] = "2026-10-12T10:00:00+02:00" }), ["future"] = new JObject { ["keep"] = true } };
            var options = Options(); options.Users = users; options.Groups = groups; options.CompletionCondition = condition; options.ConfirmedActions = Actions(); options.RejectedActions = new WorkflowFinalizingAction[0];
            options.ShareDocumentWithUsers = false; options.Visibility = "document"; options.RemoveCreatedSharings = "on-completion"; options.Comment = ""; options.ConfirmationMode = "all";
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}", BeforeAuthorize = () => { users.Clear(); groups.Clear(); condition.RemoveAll(); options.Name = "changed"; } };
            await new Client(state).CreateWorkflowTemplateAsync("context", options); var body = Body(state);
            Assert.That(count, Is.EqualTo(1)); Assert.That(body["users"].Values<string>(), Is.EqualTo(new[] { "user", "user" })); Assert.That(body["groups"].Values<string>(), Is.EqualTo(new[] { "group" }));
            Assert.That(body.Value<string>("name"), Is.EqualTo("ä template")); Assert.That(body.Value<bool>("share-document-with-users"), Is.False); Assert.That(body.Value<string>("comment"), Is.Empty);
            Assert.That(body["completion-condition"]["metadata"][0].Value<string>("default-value"), Is.EqualTo("1.2300")); Assert.That(body["completion-condition"]["future"].Value<bool>("keep"), Is.True);
            var action = body["finalizing-actions"]["confirmed"][0]["data"]; Assert.That(action.Value<string>("collection"), Is.EqualTo("target")); Assert.That(action.Value<string>("source-folder"), Is.EqualTo("source")); Assert.That(action["id"], Is.Null);
            Assert.That(body["finalizing-actions"]["rejected"], Is.Empty);
        }

        [TestCase("read"), TestCase("approval"), TestCase("metadata-form")]
        public async Task CurrentTypesAndMetadataFormOneOmissionRetainServerResolution(string type)
        {
            var options = Options(); options.Type = type; if (type == "metadata-form") options.CompletionCondition = new JObject { ["metadata-templates"] = new JArray() };
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" }; await new Client(state).CreateWorkflowTemplateAsync("context", options);
            Assert.That(Body(state).Value<string>("type"), Is.EqualTo(type)); Assert.That(Body(state)["confirmation-mode"], Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public async Task UpdatePreservesOmittedFieldsAndExplicitEmptyListsWithoutGuessingExistingType(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            var options = new WorkflowTemplateOptions { Comment = "", Users = new string[0], Groups = new string[0], RejectedActions = new[] { WorkflowFinalizingAction.RemoveFromLocation(sourceCollectionId: "source") } };
            if (sync) client.UpdateWorkflowTemplate("context", "id with/slash", options); else await client.UpdateWorkflowTemplateAsync("context", "id with/slash", options);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/workflow-template/id%20with%2Fslash")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put)); var body = Body(state);
            Assert.That(body["users"], Is.Empty); Assert.That(body["groups"], Is.Empty); Assert.That(body["type"], Is.Null); Assert.That(body["share-document-with-users"], Is.Null);
            Assert.That(body["finalizing-actions"]["confirmed"], Is.Null); Assert.That(body["finalizing-actions"]["rejected"][0]["data"].Value<string>("source-collection"), Is.EqualTo("source"));
        }

        [TestCase(false), TestCase(true)]
        public void UpdatingAutomaticSharingIsRejectedEvenWhenFalse(bool share)
        {
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateWorkflowTemplateAsync("context", "template", new WorkflowTemplateOptions { ShareDocumentWithUsers = share })));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("name"), TestCase("type"), TestCase("unknown-type"), TestCase("mode"), TestCase("visibility"), TestCase("removal"), TestCase("condition"), TestCase("metadata-all"), TestCase("rejected-read"), TestCase("actions"), TestCase("null-user"), TestCase("metadata-shape"), TestCase("metadata-value")]
        public void InvalidKnownCreationShapesFailBeforeAuthorization(string problem)
        {
            var options = Options(); if (problem == "name") options.Name = null; if (problem == "type") options.Type = null; if (problem == "unknown-type") options.Type = "unknown";
            if (problem == "mode") options.ConfirmationMode = "unknown"; if (problem == "visibility") options.Visibility = "unknown"; if (problem == "removal") options.RemoveCreatedSharings = "unknown";
            if (problem == "condition") options.Type = "metadata-form"; if (problem == "metadata-all") { options.Type = "metadata-form"; options.ConfirmationMode = "all"; options.CompletionCondition = new JObject(); }
            if (problem == "rejected-read") { options.Type = "read"; options.RejectedActions = new[] { WorkflowFinalizingAction.AddToFolder("target") }; }
            if (problem == "actions") options.ConfirmedActions = new[] { WorkflowFinalizingAction.AddToFolder("one"), WorkflowFinalizingAction.AddToFolder("two") }; if (problem == "null-user") options.Users = new string[] { null };
            if (problem == "metadata-shape") options.CompletionCondition = new JObject { ["metadata"] = "csv" };
            if (problem == "metadata-value") options.CompletionCondition = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "user.numbers.amount", ["default-value"] = 1 }) };
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).CreateWorkflowTemplateAsync("context", options))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("add-tags", "tags"), TestCase("remove-tags", "tags"), TestCase("add-to-collection", "collection"), TestCase("add-to-folder", "folder"), TestCase("move-to-collection", "collection"), TestCase("move-to-folder", "folder"), TestCase("remove-from-location", "source-collection")]
        public void AllSevenFinalizersUseWorkflowLocationKeysAndDefensiveData(string type, string key)
        {
            var action = type == "add-tags" ? WorkflowFinalizingAction.AddTags(new[] { "ä tag" }) : type == "remove-tags" ? WorkflowFinalizingAction.RemoveTags(new[] { "ä tag" }) :
                type == "add-to-collection" ? WorkflowFinalizingAction.AddToCollection("target") : type == "add-to-folder" ? WorkflowFinalizingAction.AddToFolder("target") :
                type == "move-to-collection" ? WorkflowFinalizingAction.MoveToCollection("target", sourceCollectionId: "source") : type == "move-to-folder" ? WorkflowFinalizingAction.MoveToFolder("target", sourceFolderId: "source") : WorkflowFinalizingAction.RemoveFromLocation(sourceCollectionId: "source");
            Assert.That(action.Type, Is.EqualTo(type)); Assert.That(action.Data[key], Is.Not.Null); Assert.That(action.Data["id"], Is.Null); var copy = action.Data; copy.RemoveAll(); Assert.That(action.Data[key], Is.Not.Null);
        }

        [TestCase("unknown"), TestCase("empty-tags"), TestCase("null-tags"), TestCase("target"), TestCase("sources"), TestCase("missing-source")]
        public void InvalidFinalizerShapeIsRejectedWithoutExecution(string problem)
        {
            Assert.Catch<ArgumentException>((TestDelegate)(() =>
            {
                if (problem == "unknown") new WorkflowFinalizingAction("unknown", new JObject()); else if (problem == "empty-tags") WorkflowFinalizingAction.AddTags(new string[0]);
                else if (problem == "null-tags") WorkflowFinalizingAction.AddTags(new string[] { null }); else if (problem == "target") WorkflowFinalizingAction.AddToCollection(" ");
                else if (problem == "sources") WorkflowFinalizingAction.MoveToFolder("target", "one", "two"); else WorkflowFinalizingAction.RemoveFromLocation();
            }));
        }

        [TestCase(false), TestCase(true)]
        public async Task ReadMetadataPreservesOriginalCompleteUnknownDataWithoutInventedCreatorOrDefaults(bool sync)
        {
            var data = Record(); data["type"] = "future-type"; data["visibility"] = "future-visibility"; data["completion-condition"] = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "future.key", ["default-value"] = "1.2300" }), ["extra"] = true };
            data["finalizing-actions"] = new JObject { ["confirmed"] = new JArray(new JObject { ["type"] = "future-action", ["data"] = new JObject { ["keep"] = true } }), ["future-result"] = new JObject { ["keep"] = true } };
            data["users"] = new JArray(); data["unknown-envelope"] = new JObject { ["keep"] = true }; var state = new State { Json = data.ToString() }; var client = new Client(state);
            var result = sync ? client.GetWorkflowTemplate("context", "template") : await client.GetWorkflowTemplateAsync("context", "template");
            Assert.That(result.Type, Is.EqualTo("future-type")); Assert.That(result.Visibility, Is.EqualTo("future-visibility")); Assert.That(result.ShareDocumentWithUsers, Is.Null); Assert.That(result.Groups, Is.Null); Assert.That(result.Users, Is.Empty);
            Assert.That(typeof(WorkflowTemplate).GetProperty("Creator"), Is.Null); Assert.That(result.CompletionCondition["metadata"][0].Value<string>("default-value"), Is.EqualTo("1.2300"));
            result.CompletionCondition.RemoveAll(); result.FinalizingActions.RemoveAll(); result.Data.RemoveAll(); Assert.That(result.CompletionCondition["extra"], Is.Not.Null); Assert.That(result.FinalizingActions["future-result"], Is.Not.Null); Assert.That(result.Data["unknown-envelope"], Is.Not.Null);
        }

        [Test]
        public async Task OwnListRetainsServerOrderAndReadonlyResult()
        {
            var state = new State { Json = new JObject { ["workflow-templates"] = new JArray(Record("second"), Record()) }.ToString() }; var result = await new Client(state).GetWorkflowTemplatesAsync("context");
            Assert.That(result.Select(item => item.Id), Is.EqualTo(new[] { "second", "template" })); Assert.That(((IList<WorkflowTemplate>)result).IsReadOnly, Is.True); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase("missing"), TestCase("object"), TestCase("null-entry"), TestCase("duplicate"), TestCase("canonical-duplicate")]
        public void MalformedOwnListFailsWithoutInventingAnEmptyResult(string problem)
        {
            var entries = new JArray(Record()); var root = new JObject { ["workflow-templates"] = entries };
            if (problem == "missing") root.RemoveAll(); if (problem == "object") root["workflow-templates"] = new JObject();
            if (problem == "null-entry") entries.Add(JValue.CreateNull()); if (problem == "duplicate") entries.Add(Record());
            if (problem == "canonical-duplicate") { entries.Clear(); entries.Add(Record("11111111-2222-3333-4444-555555555555")); entries.Add(Record("{11111111-2222-3333-4444-555555555555}")); }
            var state = new State { Json = root.ToString() };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetWorkflowTemplatesAsync("context")));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(0), TestCase(501)]
        public async Task OwnListRetainsEmptyAndUncappedServerResults(int count)
        {
            var entries = new JArray(Enumerable.Range(0, count).Select(index => Record("id-" + index)));
            var state = new State { Json = new JObject { ["workflow-templates"] = entries }.ToString() };
            Assert.That((await new Client(state).GetWorkflowTemplatesAsync("context")).Count, Is.EqualTo(count));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task SelectedIdentityAcceptsEquivalentGuidWithoutChangingOriginalMetadata(bool sync)
        {
            var id = "{ABCDEFAB-2222-3333-4444-555555555555}"; var state = new State { Json = Record(id).ToString() }; var client = new Client(state);
            var result = sync ? client.GetWorkflowTemplate("context", "abcdefab-2222-3333-4444-555555555555") : await client.GetWorkflowTemplateAsync("context", "abcdefab-2222-3333-4444-555555555555");
            Assert.That(result.Id, Is.EqualTo(id));
        }
        [TestCase("id"), TestCase("type"), TestCase("users"), TestCase("condition"), TestCase("flag"), TestCase("actions")]
        public void MalformedSuccessMetadataPreservesOriginalCause(string problem)
        {
            var data = Record(); if (problem == "id") data["id"] = "different"; if (problem == "type") data["type"] = 1; if (problem == "users") data["users"] = "csv";
            if (problem == "condition") data["completion-condition"] = 1; if (problem == "flag") data["share-document-with-users"] = "false"; if (problem == "actions") data["finalizing-actions"] = new JObject { ["confirmed"] = new JArray(1) };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetWorkflowTemplateAsync("context", "template"))); Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":1}"), TestCase("{\"id\":\"created\"} {}")]
        public void MalformedCreationDoesNotReplay(string json)
        { var state = new State { Status = HttpStatusCode.Created, Json = json }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateWorkflowTemplateAsync("context", Options()))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase("template"), TestCase("document"), TestCase("version")]
        public void InvalidExplicitWorkflowSelectionFailsBeforeAuthorization(string problem)
        { var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).CreateWorkflowAsync("context", problem == "template" ? " " : "template", problem == "document" ? " " : "document", problem == "version" ? 0 : 1))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void CreatorAndVisibilityFailuresRetainOriginalCause(int status)
        {
            var cause = new InvalidOperationException("original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).CreateWorkflowAsync("context", "template", "document", 1))); Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("create"), TestCase("use"), TestCase("get"), TestCase("list"), TestCase("update"), TestCase("remove")]
        public async Task PreAndActiveCancellationReachAuthorizationAndIo(string operation)
        {
            Func<Client, CancellationToken, Task> invoke = (client, token) => operation == "create" ? (Task)client.CreateWorkflowTemplateAsync("context", Options(), token) : operation == "use" ? client.CreateWorkflowAsync("context", "template", "document", 1, token) :
                operation == "get" ? client.GetWorkflowTemplateAsync("context", "template", token) : operation == "list" ? client.GetWorkflowTemplatesAsync("context", token) : operation == "update" ? client.UpdateWorkflowTemplateAsync("context", "template", new WorkflowTemplateOptions(), token) : client.RemoveWorkflowTemplateAsync("context", "template", token);
            using (var cancellation = new CancellationTokenSource())
            { var state = new State { Pending = true }; var pending = invoke(new Client(state), cancellation.Token); await state.Entered.Task; cancellation.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
                Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token)); state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await invoke(new Client(state), cancellation.Token))); Assert.That(state.Authorizations, Is.Zero); }
        }
        [TestCase("use", 201), TestCase("use", 503), TestCase("create", 201), TestCase("create", 503), TestCase("get", 200), TestCase("list", 200), TestCase("get", 503), TestCase("update", 204), TestCase("update", 503), TestCase("remove", 204), TestCase("remove", 503)]
        public async Task PublicFactoryVerifiesRealWireAndPreservesGetRetriesWithoutMutationReplay(string operation, int status)
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
                        var response = status == 503 ? "{}" : operation == "create" || operation == "use" ? "{\"id\":\"created\"}" : operation == "get" ? Record().ToString() : operation == "list" ? new JObject { ["workflow-templates"] = new JArray(Record()) }.ToString() : "";
                        var payload = Encoding.UTF8.GetBytes(response);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 201 ? " Created" : status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.WorkflowTemplates; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "use") Assert.That(await client.CreateWorkflowAsync("context", "template", "document", 1, cancellation.Token), Is.EqualTo("created"));
                        else if (operation == "create") Assert.That(await client.CreateWorkflowTemplateAsync("context", Options(), cancellation.Token), Is.EqualTo("created"));
                        else if (operation == "get") Assert.That((await client.GetWorkflowTemplateAsync("context", "template", cancellation.Token)).Id, Is.EqualTo("template"));
                        else if (operation == "list") Assert.That((await client.GetWorkflowTemplatesAsync("context", cancellation.Token)).Count, Is.EqualTo(1));
                        else if (operation == "update") await client.UpdateWorkflowTemplateAsync("context", "template", new WorkflowTemplateOptions { Comment = "", Users = new string[0] }, cancellation.Token);
                        else await client.RemoveWorkflowTemplateAsync("context", "template", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "create" ? "POST /bridge/v2/workflow-templates" : operation == "list" ? "GET /bridge/v2/workflow-templates" :
                        (operation == "get" ? "GET" : operation == "use" ? "POST" : operation == "update" ? "PUT" : "DELETE") + " /bridge/v2/workflow-template/template";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    if (operation == "create") Assert.That(JObject.Parse(sent).Value<string>("name"), Is.EqualTo("ä template"));
                    else if (operation == "update") Assert.That(JObject.Parse(sent).Properties().Select(property => property.Name), Is.EqualTo(new[] { "users", "comment" }));
                    else if (operation == "use") Assert.That(JObject.Parse(sent)["data"].Value<long>("document-version"), Is.EqualTo(1));
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
        {
            using (var text = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader);
        }
        private sealed class Client : WorkflowTemplatesRestClient
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
