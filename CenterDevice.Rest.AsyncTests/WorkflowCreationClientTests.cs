using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Workflows;
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
    public class WorkflowCreationClientTests
    {
        private static WorkflowCreationOptions Options(string type = "approval") => new WorkflowCreationOptions { DocumentId = "main document", DocumentVersion = long.MaxValue, Type = type };
        [TestCase(false), TestCase(true)]
        public async Task SyncNativeDirectCreationPreservesRequiredTopLevelFieldsAndOmittedDefaults(bool sync)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"new-workflow\"}" }; var client = new Client(state);
            var result = sync ? client.CreateWorkflow("context", Options()) : await client.CreateWorkflowAsync("context", Options());
            Assert.That(result, Is.EqualTo("new-workflow")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/workflows")); Assert.That(state.Context, Is.EqualTo("context"));
            var body = Body(state); Assert.That(body.Properties().Select(property => property.Name), Is.EqualTo(new[] { "type", "document-id", "document-version" })); Assert.That(body.Value<long>("document-version"), Is.EqualTo(long.MaxValue));
            Assert.That(body["action"], Is.Null); Assert.That(body["params"], Is.Null); Assert.That(body["data"], Is.Null); Assert.That(body["name"], Is.Null); Assert.That(body["creator"], Is.Null); Assert.That(body["share-document-with-users"], Is.Null);
        }

        [Test]
        public async Task CompleteCreationSnapshotsAllSequencesAndNestedDefaultsBeforeAuthorization()
        {
            var users = new List<string> { "user", "user" }; var groups = new List<string> { "group" }; var attachments = new List<WorkflowAttachment> { new WorkflowAttachment("attachment", long.MaxValue) }; var counts = new int[5];
            IEnumerable<T> Once<T>(int index, IEnumerable<T> values) { counts[index]++; foreach (var value in values) yield return value; }
            var condition = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "user.numbers.amount", ["default-value"] = "1.2300" }, new JObject { ["key"] = "user.dates.date", ["default-value"] = "2026-10-12T10:00:00.1234567+02:00" }), ["future"] = true };
            var options = Options(); options.Users = Once(0, users); options.Groups = Once(1, groups); options.Attachments = Once(2, attachments); options.CompletionCondition = condition;
            options.ConfirmedActions = Once(3, new[] { WorkflowFinalizingAction.MoveToCollection("target", sourceFolderId: "source") }); options.RejectedActions = Once(4, new[] { WorkflowFinalizingAction.RemoveFromLocation(sourceCollectionId: "old-collection") });
            options.ShareDocumentWithUsers = false; options.ConfirmationMode = "all"; options.Comment = ""; options.Visibility = "document"; options.RemoveCreatedSharings = "on-completion";
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"new-workflow\"}", BeforeAuthorize = () => { users.Clear(); groups.Clear(); attachments.Clear(); condition.RemoveAll(); options.Type = "changed"; options.DocumentId = "changed"; options.DocumentVersion = 1; } };
            await new Client(state).CreateWorkflowAsync("context", options); var body = Body(state); Assert.That(counts, Is.EqualTo(new[] { 1, 1, 1, 1, 1 })); Assert.That(body.Properties().Count(), Is.EqualTo(13));
            Assert.That(body.Value<string>("type"), Is.EqualTo("approval")); Assert.That(body.Value<string>("document-id"), Is.EqualTo("main document")); Assert.That(body.Value<long>("document-version"), Is.EqualTo(long.MaxValue));
            Assert.That(body["users"].Values<string>(), Is.EqualTo(new[] { "user", "user" })); Assert.That(body["groups"].Values<string>(), Is.EqualTo(new[] { "group" }));
            Assert.That(body["attachments"][0].Value<string>("document-id"), Is.EqualTo("attachment")); Assert.That(body["attachments"][0].Value<long>("version"), Is.EqualTo(long.MaxValue)); Assert.That(body["attachments"][0]["document-version"], Is.Null);
            Assert.That(body["completion-condition"]["metadata"][0].Value<string>("default-value"), Is.EqualTo("1.2300")); Assert.That(body["completion-condition"]["metadata"][1].Value<string>("default-value"), Is.EqualTo("2026-10-12T10:00:00.1234567+02:00")); Assert.That(body["completion-condition"]["future"], Is.Not.Null);
            Assert.That(body.Value<bool>("share-document-with-users"), Is.False); Assert.That(body.Value<string>("comment"), Is.Empty); Assert.That(body.Value<string>("visibility"), Is.EqualTo("document")); Assert.That(body.Value<string>("remove-created-sharings"), Is.EqualTo("on-completion"));
            Assert.That(body["finalizing-actions"]["confirmed"][0]["data"].Value<string>("collection"), Is.EqualTo("target")); Assert.That(body["finalizing-actions"]["confirmed"][0]["data"].Value<string>("source-folder"), Is.EqualTo("source")); Assert.That(body["finalizing-actions"]["confirmed"][0]["data"]["id"], Is.Null);
        }

        [TestCase("read"), TestCase("approval"), TestCase("metadata-form")]
        public async Task CurrentTypesRetainExplicitEmptySelectionsAndMetadataFormOneOmission(string type)
        {
            var options = Options(type); options.Users = new string[0]; options.Groups = new string[0]; options.Attachments = new WorkflowAttachment[0]; options.ConfirmedActions = new WorkflowFinalizingAction[0]; options.RejectedActions = new WorkflowFinalizingAction[0];
            if (type == "metadata-form") options.CompletionCondition = new JObject { ["metadata"] = new JArray() }; var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"workflow\"}" };
            await new Client(state).CreateWorkflowAsync("context", options); var body = Body(state); Assert.That(body.Value<string>("type"), Is.EqualTo(type)); Assert.That(body["attachments"], Is.Empty); Assert.That(body["users"], Is.Empty); Assert.That(body["groups"], Is.Empty);
            Assert.That(body["finalizing-actions"]["confirmed"], Is.Empty); Assert.That(body["finalizing-actions"]["rejected"], Is.Empty); Assert.That(body["confirmation-mode"], Is.Null);
        }

        [TestCase("read"), TestCase("approval")]
        public async Task ReadAndApprovalPermitExplicitOriginalAttachmentVersionsWithoutDeduplication(string type)
        {
            var options = Options(type); options.Attachments = new[] { new WorkflowAttachment("same document", 1), new WorkflowAttachment("same document", 2) }; options.ShareDocumentWithUsers = true;
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"workflow\"}" }; await new Client(state).CreateWorkflowAsync("context", options);
            Assert.That(Body(state)["attachments"].Select(entry => entry.Value<long>("version")), Is.EqualTo(new long[] { 1, 2 })); Assert.That(Body(state).Value<bool>("share-document-with-users"), Is.True);
        }

        [TestCase("null-options"), TestCase("document"), TestCase("version"), TestCase("type"), TestCase("unknown-type"), TestCase("null-user"), TestCase("blank-group"), TestCase("null-attachment"), TestCase("metadata-attachment"), TestCase("confirmation"), TestCase("metadata-all"), TestCase("metadata-condition"), TestCase("visibility"), TestCase("removal"), TestCase("two-finalizers"), TestCase("null-finalizer"), TestCase("rejected-read"), TestCase("metadata-shape"), TestCase("metadata-key"), TestCase("metadata-default")]
        public void InvalidKnownCreationChoicesFailBeforeAuthorization(string problem)
        {
            var options = Options(); if (problem == "null-options") options = null; else
            {
                if (problem == "document") options.DocumentId = " "; if (problem == "version") options.DocumentVersion = 0; if (problem == "type") options.Type = null; if (problem == "unknown-type") options.Type = "future-type";
                if (problem == "null-user") options.Users = new string[] { null }; if (problem == "blank-group") options.Groups = new[] { " " }; if (problem == "null-attachment") options.Attachments = new WorkflowAttachment[] { null };
                if (problem == "metadata-attachment") { options.Type = "metadata-form"; options.CompletionCondition = new JObject(); options.Attachments = new[] { new WorkflowAttachment("attachment", 1) }; }
                if (problem == "confirmation") options.ConfirmationMode = "future"; if (problem == "metadata-all") { options.Type = "metadata-form"; options.CompletionCondition = new JObject(); options.ConfirmationMode = "all"; }
                if (problem == "metadata-condition") options.Type = "metadata-form"; if (problem == "visibility") options.Visibility = "future"; if (problem == "removal") options.RemoveCreatedSharings = "future";
                if (problem == "two-finalizers") options.ConfirmedActions = new[] { WorkflowFinalizingAction.AddToCollection("one"), WorkflowFinalizingAction.AddToCollection("two") }; if (problem == "null-finalizer") options.ConfirmedActions = new WorkflowFinalizingAction[] { null };
                if (problem == "rejected-read") { options.Type = "read"; options.RejectedActions = new[] { WorkflowFinalizingAction.AddToCollection("one") }; }
                if (problem == "metadata-shape") options.CompletionCondition = new JObject { ["metadata"] = "csv" }; if (problem == "metadata-key") options.CompletionCondition = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "system.strings.unknown" }) };
                if (problem == "metadata-default") options.CompletionCondition = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "user.numbers.amount", ["default-value"] = 1 }) };
            }
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).CreateWorkflowAsync("context", options))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(null, 1L), TestCase("", 1L), TestCase(" ", 1L), TestCase("attachment", 0L), TestCase("attachment", -1L)]
        public void PublicImmutableAttachmentSelectionRequiresOriginalIdentityAndExplicitVersion(string id, long version)
        { Assert.Catch<ArgumentException>((TestDelegate)(() => new WorkflowAttachment(id, version))); }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":1}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":\".\"}"), TestCase("{\"id\":\"workflow\"} {}")]
        public void UncertainCreationResponseCannotBecomeConfirmedIdentityOrReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateWorkflowAsync("context", Options())));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(400), TestCase(403), TestCase(404), TestCase(503), TestCase(200), TestCase(204)]
        public void OriginalVisibilityParticipantRoleOrUnexpectedStatusDiagnosticIsPreservedWithoutReplay(int status)
        {
            var cause = new InvalidOperationException("original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause }; var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).CreateWorkflowAsync("context", Options())));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndIo()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = new Client(state).CreateWorkflowAsync("context", Options(), cancellation.Token); await state.Entered.Task; cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).CreateWorkflowAsync("context", Options(), cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }

        [TestCase(201), TestCase(503)]
        public async Task PublicFactoryVerifiesActualNestedWireAndNoUncertainWriteReplay(int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            {
                string header = null, sent = null;
                var server = Task.Run(async () =>
                {
                    using (var connection = await listener.AcceptTcpClientAsync()) using (var stream = connection.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (true) { if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        header = Encoding.ASCII.GetString(bytes.ToArray()); var lengthHeader = header.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)); var length = int.Parse(lengthHeader.Split(':')[1].Trim());
                        var received = new byte[length]; var count = 0; while (count < length) { var read = await stream.ReadAsync(received, count, length - count, cancellation.Token); if (read == 0) throw new EndOfStreamException(); count += read; } sent = Encoding.UTF8.GetString(received);
                        var payload = Encoding.UTF8.GetBytes(status == 503 ? "{}" : "{\"id\":\"new-workflow\"}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 201 ? " Created" : " Service Unavailable") + "\r\nContent-Type: application/json\r\nLocation: https://untrusted.invalid/workflow/other\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token); await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Workflows; client.DisableOfflineModeSimulation();
                    var options = Options(); options.Attachments = new[] { new WorkflowAttachment("attachment", long.MaxValue) }; options.Comment = "ä notification"; options.ShareDocumentWithUsers = false;
                    if (status == 503) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await client.CreateWorkflowAsync("context", options, cancellation.Token)));
                    else Assert.That(await client.CreateWorkflowAsync("context", options, cancellation.Token), Is.EqualTo("new-workflow")); await server;
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/workflows HTTP/1.1")); var body = JObject.Parse(sent);
                    Assert.That(body.Value<string>("comment"), Is.EqualTo("ä notification")); Assert.That(body.Value<bool>("share-document-with-users"), Is.False); Assert.That(body["attachments"][0].Value<long>("version"), Is.EqualTo(long.MaxValue)); Assert.That(body["action"], Is.Null); Assert.That(body["params"], Is.Null); Assert.That(listener.Pending(), Is.False);
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
