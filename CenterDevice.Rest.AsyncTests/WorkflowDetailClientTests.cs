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
    public class WorkflowDetailClientTests
    {
        private static JObject Record(string id = "workflow") => new JObject { ["id"] = id };
        [TestCase(null, false), TestCase(false, false), TestCase(true, false), TestCase(null, true), TestCase(false, true), TestCase(true, true)]
        public async Task VisibilityOmissionAndExplicitBooleanKeepBodylessSelectedSyncNativeGet(bool? visibility, bool sync)
        {
            var state = new State { Json = Record("id with/slash").ToString() }; var client = new Client(state);
            var result = sync ? client.GetWorkflowDetails("context", "id with/slash", visibility) : await client.GetWorkflowDetailsAsync("context", "id with/slash", visibility);
            Assert.That(result.Id, Is.EqualTo("id with/slash")); Assert.That(state.Request.Resource, Is.EqualTo("v2/workflow/id%20with%2Fslash")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            var query = state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString).ToArray();
            Assert.That(query.Length, Is.EqualTo(visibility.HasValue ? 1 : 0)); if (visibility.HasValue) { Assert.That(query[0].Name, Is.EqualTo("include-document-visibility")); Assert.That(query[0].Value, Is.EqualTo(visibility.Value ? "true" : "false")); }
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase("omitted"), TestCase("null"), TestCase("empty")]
        public async Task RoleDependentOmissionsRemainUnknownRatherThanEmptyOrCallerState(string projection)
        {
            var data = Record(); foreach (var key in new[] { "users", "groups", "assignees", "users-not-responded", "responses", "attachments" })
            { if (projection == "null") data[key] = JValue.CreateNull(); if (projection == "empty") data[key] = new JArray(); }
            foreach (var key in new[] { "response", "completion-condition", "finalizing-actions" }) { if (projection == "null") data[key] = JValue.CreateNull(); if (projection == "empty") data[key] = new JObject(); }
            var result = await new Client(new State { Json = data.ToString() }).GetWorkflowDetailsAsync("different-caller", "workflow");
            Assert.That(result.CreatorId, Is.Null); Assert.That(result.DocumentVersion, Is.Null); Assert.That(result.Result, Is.Null); Assert.That(result.CompletionDateRaw, Is.Null);
            if (projection == "empty") { Assert.That(result.Users, Is.Empty); Assert.That(result.Responses, Is.Empty); Assert.That(result.Attachments, Is.Empty); Assert.That(result.CallerResponse, Is.Empty); Assert.That(result.FinalizingActions, Is.Empty); }
            else { Assert.That(result.Users, Is.Null); Assert.That(result.Groups, Is.Null); Assert.That(result.Assignees, Is.Null); Assert.That(result.UsersNotResponded, Is.Null); Assert.That(result.Responses, Is.Null); Assert.That(result.Attachments, Is.Null); Assert.That(result.CallerResponse, Is.Null); Assert.That(result.FinalizingActions, Is.Null); }
        }

        [TestCase("read"), TestCase("approval"), TestCase("metadata-form"), TestCase("future-type")]
        public async Task CompleteDetailPreservesOriginalRolesVersionsDatesAndFutureNestedData(string type)
        {
            var data = Record(); data["document-id"] = "document"; data["document-version"] = long.MaxValue; data["creator"] = "actual-creator"; data["type"] = type; data["status"] = "future-status"; data["result"] = "future-result";
            data["confirmation-mode"] = "future-mode"; data["visibility"] = "future-visibility"; data["comment"] = "";
            data["creation-date"] = "2026-10-12T01:02:03.1234567+02:00"; data["last-modified-date"] = "2026-10-12T01:02:04Z"; data["completion-date"] = "2026-10-12T01:02:05";
            data["users"] = new JArray("user", "user"); data["groups"] = new JArray("group"); data["assignees"] = new JArray("assignee"); data["users-not-responded"] = new JArray("not-responded");
            var response = new JObject { ["document-version"] = 7, ["result"] = "future-response", ["response-date"] = "2026-10-12T01:02:05+03:00", ["comment"] = "ä comment", ["future"] = true };
            data["response"] = response; var all = (JObject)response.DeepClone(); all["user-id"] = "responded"; data["responses"] = new JArray(all);
            data["attachments"] = new JArray(new JObject { ["document-id"] = "attachment", ["version"] = long.MaxValue, ["future"] = true });
            data["completion-condition"] = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "future.field", ["default-value"] = "1.2300" }), ["future"] = true };
            data["finalizing-actions"] = new JObject { ["confirmed"] = new JArray(new JObject { ["type"] = "future-action", ["data"] = new JObject { ["future"] = true } }), ["future-result"] = true }; data["future-envelope"] = true;
            var result = await new Client(new State { Json = data.ToString() }).GetWorkflowDetailsAsync("different-caller", "workflow");
            Assert.That(result.DocumentId, Is.EqualTo("document")); Assert.That(result.DocumentVersion, Is.EqualTo(long.MaxValue)); Assert.That(result.CreatorId, Is.EqualTo("actual-creator")); Assert.That(result.Type, Is.EqualTo(type));
            Assert.That(result.Status, Is.EqualTo("future-status")); Assert.That(result.Result, Is.EqualTo("future-result")); Assert.That(result.ConfirmationMode, Is.EqualTo("future-mode")); Assert.That(result.Visibility, Is.EqualTo("future-visibility")); Assert.That(result.Comment, Is.Empty);
            Assert.That(result.CreationDateRaw, Is.EqualTo("2026-10-12T01:02:03.1234567+02:00")); Assert.That(result.LastModifiedDateRaw, Is.EqualTo("2026-10-12T01:02:04Z")); Assert.That(result.CompletionDateRaw, Is.EqualTo("2026-10-12T01:02:05"));
            Assert.That(result.Users, Is.EqualTo(new[] { "user", "user" })); Assert.That(result.Groups.Single(), Is.EqualTo("group")); Assert.That(result.Assignees.Single(), Is.EqualTo("assignee")); Assert.That(result.UsersNotResponded.Single(), Is.EqualTo("not-responded"));
            Assert.That(result.CallerResponse["user-id"], Is.Null); Assert.That(result.Responses.Single().Value<string>("user-id"), Is.EqualTo("responded")); Assert.That(result.Responses.Single().Value<string>("response-date"), Is.EqualTo("2026-10-12T01:02:05+03:00"));
            Assert.That(result.Attachments.Single().DocumentId, Is.EqualTo("attachment")); Assert.That(result.Attachments.Single().Version, Is.EqualTo(long.MaxValue));
            Assert.That(result.CompletionCondition["metadata"][0].Value<string>("default-value"), Is.EqualTo("1.2300")); Assert.That(result.FinalizingActions["future-result"], Is.Not.Null);
            result.CallerResponse.RemoveAll(); result.Responses.Single().RemoveAll(); result.CompletionCondition.RemoveAll(); result.FinalizingActions.RemoveAll(); result.Data.RemoveAll();
            Assert.That(result.Responses.Single()["future"], Is.Not.Null); Assert.That(result.CallerResponse["future"], Is.Not.Null); Assert.That(result.CompletionCondition["future"], Is.Not.Null); Assert.That(result.FinalizingActions["future-result"], Is.Not.Null); Assert.That(result.Data["future-envelope"], Is.Not.Null);
            Assert.That(((System.Collections.Generic.IList<WorkflowAttachment>)result.Attachments).IsReadOnly, Is.True);
        }

        [TestCase("id"), TestCase("document-id"), TestCase("document-version"), TestCase("creator"), TestCase("type"), TestCase("status"), TestCase("date"), TestCase("users"), TestCase("user-entry"), TestCase("response"), TestCase("responses"), TestCase("response-user"), TestCase("response-version"), TestCase("attachments"), TestCase("attachment-id"), TestCase("attachment-version"), TestCase("condition"), TestCase("metadata"), TestCase("default"), TestCase("finalizers"), TestCase("finalizer-shape")]
        public void MalformedKnownSuccessShapeRetainsInnerCause(string problem)
        {
            var data = Record();
            if (problem == "id") data["id"] = "other"; if (problem == "document-id") data["document-id"] = 1; if (problem == "document-version") data["document-version"] = 1.1; if (problem == "creator") data["creator"] = " ";
            if (problem == "type") data["type"] = 1; if (problem == "status") data["status"] = true; if (problem == "date") data["creation-date"] = 1; if (problem == "users") data["users"] = "csv"; if (problem == "user-entry") data["users"] = new JArray(1);
            if (problem == "response") data["response"] = new JArray(); if (problem == "responses") data["responses"] = new JObject(); if (problem == "response-user") data["responses"] = new JArray(new JObject());
            if (problem == "response-version") data["response"] = new JObject { ["document-version"] = 0 }; if (problem == "attachments") data["attachments"] = new JObject();
            if (problem == "attachment-id") data["attachments"] = new JArray(new JObject { ["document-id"] = null, ["version"] = 1 }); if (problem == "attachment-version") data["attachments"] = new JArray(new JObject { ["document-id"] = "document", ["version"] = -1 });
            if (problem == "condition") data["completion-condition"] = new JArray(); if (problem == "metadata") data["completion-condition"] = new JObject { ["metadata"] = new JArray(1) };
            if (problem == "default") data["completion-condition"] = new JObject { ["metadata"] = new JArray(new JObject { ["key"] = "future.field", ["default-value"] = 1 }) };
            if (problem == "finalizers") data["finalizing-actions"] = 1; if (problem == "finalizer-shape") data["finalizing-actions"] = new JObject { ["confirmed"] = new JArray(new JObject { ["type"] = "future-action", ["data"] = new JArray() }) };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetWorkflowDetailsAsync("context", "workflow")));
            Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":1}"), TestCase("{\"id\":\"workflow\"} {}"), TestCase("{\"id\":\"workflow\",\"document-version\":9223372036854775808}")]
        public void InvalidEnvelopeIdentityOrVersionCannotBecomeFakeEmptyMetadata(string json)
        { var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = json }).GetWorkflowDetailsAsync("context", "workflow"))); Assert.That(error.InnerException, Is.Not.Null); }

        [Test]
        public async Task SelectedGuidMatchesCanonicallyWithoutChangingOriginalId()
        {
            var id = "{ABCDEFAB-2222-3333-4444-555555555555}"; var result = await new Client(new State { Json = Record(id).ToString() }).GetWorkflowDetailsAsync("context", "abcdefab-2222-3333-4444-555555555555"); Assert.That(result.Id, Is.EqualTo(id));
        }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectionFailsBeforeAuthorization(string id)
        { var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetWorkflowDetailsAsync("context", id))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase(403), TestCase(404), TestCase(204), TestCase(503)]
        public void VisibilityOrUnexpectedStatusPreservesOriginalDiagnostic(int status)
        {
            var cause = new InvalidOperationException("original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause }; var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetWorkflowDetailsAsync("context", "workflow")));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndIo()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = new Client(state).GetWorkflowDetailsAsync("context", "workflow", cancellationToken: cancellation.Token); await state.Entered.Task;
                cancellation.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token)); Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetWorkflowDetailsAsync("context", "workflow", cancellationToken: cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }

        [TestCase(null, 200), TestCase(false, 200), TestCase(true, 200), TestCase(null, 503)]
        public async Task PublicFactoryVerifiesActualBodylessGetAndExistingBoundedRetry(bool? visibility, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            {
                var expectedRequests = status == 503 ? 3 : 1; var requests = 0; string header = null;
                var server = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < expectedRequests; attempt++) using (var connection = await listener.AcceptTcpClientAsync()) using (var stream = connection.GetStream())
                    {
                        requests++; var bytes = new System.Collections.Generic.List<byte>(); var one = new byte[1];
                        while (true) { if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        header = Encoding.ASCII.GetString(bytes.ToArray()); var payload = Encoding.UTF8.GetBytes(status == 503 ? "{}" : "{\"id\":\"workflow\",\"comment\":\"ä comment\"}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : " Service Unavailable") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token); await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Workflows; client.DisableOfflineModeSimulation();
                    if (status == 503) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await client.GetWorkflowDetailsAsync("context", "workflow", visibility, cancellation.Token)));
                    else Assert.That((await client.GetWorkflowDetailsAsync("context", "workflow", visibility, cancellation.Token)).Comment, Is.EqualTo("ä comment")); await server;
                    var query = visibility.HasValue ? "?include-document-visibility=" + (visibility.Value ? "true" : "false") : "";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("GET /bridge/v2/workflow/workflow" + query + " HTTP/1.1")); Assert.That(header, Does.Not.Contain("Content-Length:"));
                    Assert.That(requests, Is.EqualTo(expectedRequests)); Assert.That(listener.Pending(), Is.False);
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
