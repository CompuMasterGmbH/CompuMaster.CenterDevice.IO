using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Workflows;
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
    public class WorkflowListClientTests
    {
        private static JObject Record(string id = "workflow") => new JObject { ["id"] = id };
        private static string Page(params JObject[] entries) => new JObject { ["workflows"] = new JArray(entries) }.ToString();
        private static Dictionary<string, string> Query(State state) => state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString).ToDictionary(parameter => parameter.Name, parameter => (string)parameter.Value);
        [TestCase(false), TestCase(true)]
        public async Task DefaultSyncNativeListPreservesBodylessGetServerOrderAndOmittedDefaults(bool sync)
        {
            var state = new State { Json = Page(Record("second"), Record()) }; var client = new Client(state);
            var result = sync ? client.GetWorkflows("context") : await client.GetWorkflowsAsync("context");
            Assert.That(result.Select(item => item.Id), Is.EqualTo(new[] { "second", "workflow" })); Assert.That(((IList<WorkflowDetails>)result).IsReadOnly, Is.True);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(state.Request.Resource, Is.EqualTo("v2/workflows")); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(Query(state), Is.Empty);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(result[0].CreatorId, Is.Null); Assert.That(result[0].Responses, Is.Null); Assert.That(result[0].FinalizingActions, Is.Null);
        }

        [Test]
        public async Task AllSeventeenFieldsSnapshotOnceAndPreserveOriginalCsvDatesBooleansWithoutRangeOrdering()
        {
            var types = new List<string> { "read", "approval" }; var creators = new List<string> { "creator", "creator" }; var responders = new List<string> { "responder" }; var counts = new int[3];
            IEnumerable<string> Once(int index, List<string> values) { counts[index]++; foreach (var value in values) yield return value; }
            var options = new WorkflowQueryOptions { DocumentId = "doc with/slash", Role = "creator", Status = "started", Result = "confirmed", IncludeDocumentVisibility = false,
                Types = Once(0, types), Creators = Once(1, creators), Responders = Once(2, responders), Responded = false, CreationDateFrom = "2026-10-12T10:00:00.1234567+02:00", CreationDateTo = "2026-10-11T10:00:00-03:00",
                LastModifiedDateFrom = "2026-10-12T11:00:00Z", LastModifiedDateTo = "2026-10-12T12:00:00Z", ResponderStatus = "completed", IncludeWhereAttachment = true, Sort = "completion-date", Order = "desc" };
            var state = new State { Json = Page(), BeforeAuthorize = () => { types.Clear(); creators.Clear(); responders.Clear(); options.Role = "changed"; options.DocumentId = "changed"; options.CreationDateFrom = "changed"; options.IncludeWhereAttachment = false; } };
            await new Client(state).GetWorkflowsAsync("context", options); var query = Query(state);
            Assert.That(query.Count, Is.EqualTo(17)); Assert.That(counts, Is.EqualTo(new[] { 1, 1, 1 })); Assert.That(query["document-id"], Is.EqualTo("doc with/slash")); Assert.That(query["role"], Is.EqualTo("creator"));
            Assert.That(query["status"], Is.EqualTo("started")); Assert.That(query["result"], Is.EqualTo("confirmed")); Assert.That(query["include-document-visibility"], Is.EqualTo("false")); Assert.That(query["responded"], Is.EqualTo("false"));
            Assert.That(query["types"], Is.EqualTo("read,approval")); Assert.That(query["creators"], Is.EqualTo("creator,creator")); Assert.That(query["responders"], Is.EqualTo("responder"));
            Assert.That(query["creation-date-from"], Is.EqualTo("2026-10-12T10:00:00.1234567+02:00")); Assert.That(query["creation-date-to"], Is.EqualTo("2026-10-11T10:00:00-03:00"));
            Assert.That(query["last-modified-date-from"], Is.EqualTo("2026-10-12T11:00:00Z")); Assert.That(query["last-modified-date-to"], Is.EqualTo("2026-10-12T12:00:00Z")); Assert.That(query["responder-status"], Is.EqualTo("completed"));
            Assert.That(query["include-where-attachment"], Is.EqualTo("true")); Assert.That(query["sort"], Is.EqualTo("completion-date")); Assert.That(query["order"], Is.EqualTo("desc"));
        }

        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task DocumentVisibilityIncludesOnlyAnExplicitDocumentAndDoesNotInventCallerRole(bool? include)
        {
            var state = new State { Json = Page() }; await new Client(state).GetWorkflowsAsync("different-caller", new WorkflowQueryOptions { DocumentId = "document", IncludeDocumentVisibility = include }); var query = Query(state);
            Assert.That(query["document-id"], Is.EqualTo("document")); Assert.That(query.ContainsKey("role"), Is.False); Assert.That(query.ContainsKey("include-document-visibility"), Is.EqualTo(include.HasValue));
            if (include.HasValue) Assert.That(query["include-document-visibility"], Is.EqualTo(include.Value ? "true" : "false"));
        }

        [Test]
        public async Task ExplicitEmptyAndFalseSelectionsRemainDistinctFromOmission()
        {
            var state = new State { Json = Page() }; await new Client(state).GetWorkflowsAsync("context", new WorkflowQueryOptions { Types = new string[0], Creators = new string[0], Responders = new string[0],
                Role = "", Status = "", Result = "", CreationDateFrom = "", IncludeDocumentVisibility = false, IncludeWhereAttachment = false, Responded = false, Sort = "", Order = "" }); var query = Query(state);
            foreach (var key in new[] { "types", "creators", "responders", "role", "status", "result", "creation-date-from", "sort", "order" }) Assert.That(query[key], Is.Empty);
            foreach (var key in new[] { "include-document-visibility", "include-where-attachment", "responded" }) Assert.That(query[key], Is.EqualTo("false")); Assert.That(query.ContainsKey("document-id"), Is.False);
        }

        [Test]
        public async Task AdditionalTextChoicesRemainServerValidatedWithoutInventedMetadataFormSupport()
        {
            var state = new State { Json = Page() }; await new Client(state).GetWorkflowsAsync("context", new WorkflowQueryOptions { Role = "future-role", Status = "future-status", Result = "future-result", Types = new[] { "metadata-form", "future-type" },
                ResponderStatus = "future-responder-status", Sort = "future-sort", Order = "future-order", CreationDateFrom = "original server-boundary text" }); var query = Query(state);
            Assert.That(query["types"], Is.EqualTo("metadata-form,future-type")); Assert.That(query["role"], Is.EqualTo("future-role")); Assert.That(query["sort"], Is.EqualTo("future-sort")); Assert.That(query["order"], Is.EqualTo("future-order")); Assert.That(query["creation-date-from"], Is.EqualTo("original server-boundary text"));
        }

        [TestCase("visibility-no-document"), TestCase("visibility-role"), TestCase("visibility-empty-role"), TestCase("attachment-no-document"), TestCase("empty-document"), TestCase("blank-document"), TestCase("null-type"), TestCase("blank-creator"), TestCase("csv-responder")]
        public void InvalidDocumentDependenciesAndCsvSelectionsFailBeforeAuthorization(string problem)
        {
            var options = new WorkflowQueryOptions(); if (problem == "visibility-no-document") options.IncludeDocumentVisibility = true;
            if (problem == "visibility-role" || problem == "visibility-empty-role") { options.IncludeDocumentVisibility = true; options.DocumentId = "document"; options.Role = problem == "visibility-role" ? "creator" : ""; }
            if (problem == "attachment-no-document") options.IncludeWhereAttachment = true; if (problem == "empty-document") options.DocumentId = ""; if (problem == "blank-document") options.DocumentId = " ";
            if (problem == "null-type") options.Types = new string[] { null }; if (problem == "blank-creator") options.Creators = new[] { " " }; if (problem == "csv-responder") options.Responders = new[] { "one,two" };
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetWorkflowsAsync("context", options))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(0), TestCase(501)]
        public async Task EmptyAndLargeListsHaveNoInventedPagingOrCap(int count)
        {
            var state = new State { Json = Page(Enumerable.Range(0, count).Select(index => Record("id-" + index)).ToArray()) }; Assert.That((await new Client(state).GetWorkflowsAsync("context")).Count, Is.EqualTo(count)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task OrderedRoleDependentDataIsImmutableAndRetainsOriginalAttachmentVersionsAndDates()
        {
            var first = Record("{ABCDEFAB-2222-3333-4444-555555555555}"); first["type"] = "metadata-form"; first["users"] = new JArray(); first["response"] = new JObject { ["result"] = "confirmed", ["document-version"] = long.MaxValue };
            first["attachments"] = new JArray(new JObject { ["document-id"] = "attachment", ["version"] = long.MaxValue }); first["creation-date"] = "2026-10-12T10:00:00+03:00"; first["future"] = true;
            var result = await new Client(new State { Json = Page(first, Record("second")) }).GetWorkflowsAsync("caller"); Assert.That(result[0].Id, Is.EqualTo("{ABCDEFAB-2222-3333-4444-555555555555}"));
            Assert.That(result[0].Users, Is.Empty); Assert.That(result[0].Groups, Is.Null); Assert.That(result[0].CreatorId, Is.Null); Assert.That(result[0].FinalizingActions, Is.Null);
            Assert.That(result[0].Attachments.Single().Version, Is.EqualTo(long.MaxValue)); Assert.That(result[0].CreationDateRaw, Is.EqualTo("2026-10-12T10:00:00+03:00")); Assert.That(result[0].CallerResponse["user-id"], Is.Null);
            result[0].CallerResponse.RemoveAll(); result[0].Data.RemoveAll(); Assert.That(result[0].Data["future"], Is.Not.Null); Assert.That(result[0].CallerResponse["result"], Is.Not.Null);
        }

        [TestCase("missing"), TestCase("null-array"), TestCase("object-array"), TestCase("null-entry"), TestCase("empty-id"), TestCase("numeric-id"), TestCase("duplicate"), TestCase("canonical-duplicate"), TestCase("detail-version"), TestCase("hidden-shape")]
        public void MalformedSuccessfulListRetainsOriginalInnerCause(string problem)
        {
            var entries = new JArray(Record()); var root = new JObject { ["workflows"] = entries };
            if (problem == "missing") root.RemoveAll(); if (problem == "null-array") root["workflows"] = JValue.CreateNull(); if (problem == "object-array") root["workflows"] = new JObject(); if (problem == "null-entry") entries.Add(JValue.CreateNull());
            if (problem == "empty-id") entries[0]["id"] = " "; if (problem == "numeric-id") entries[0]["id"] = 1; if (problem == "duplicate") entries.Add(Record());
            if (problem == "canonical-duplicate") { entries.Clear(); entries.Add(Record("ABCDEFAB-2222-3333-4444-555555555555")); entries.Add(Record("{abcdefab-2222-3333-4444-555555555555}")); }
            if (problem == "detail-version") entries[0]["document-version"] = -1; if (problem == "hidden-shape") entries[0]["responses"] = "unknown";
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = root.ToString() }).GetWorkflowsAsync("context"))); Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase("[]"), TestCase("null"), TestCase("{\"workflows\":[]} {}")]
        public void InvalidEnvelopeCannotBecomeFakeEmptyList(string json)
        { var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = json }).GetWorkflowsAsync("context"))); Assert.That(error.InnerException, Is.Not.Null); }

        [TestCase(403), TestCase(404), TestCase(204), TestCase(503)]
        public void VisibilityAndUnexpectedStatusRemainOriginalErrors(int status)
        {
            var cause = new InvalidOperationException("original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause }; var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetWorkflowsAsync("context")));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndIo()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = new Client(state).GetWorkflowsAsync("context", cancellationToken: cancellation.Token); await state.Entered.Task; cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetWorkflowsAsync("context", cancellationToken: cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }

        [TestCase("omitted", 200), TestCase("visible", 200), TestCase("empty", 200), TestCase("omitted", 503)]
        public async Task PublicFactoryVerifiesBodylessPrefixedWireAndExistingGetRetry(string mode, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
            {
                var expected = status == 503 ? 3 : 1; var requests = 0; string header = null;
                var server = Task.Run(async () =>
                {
                    for (var attempt = 0; attempt < expected; attempt++) using (var connection = await listener.AcceptTcpClientAsync()) using (var stream = connection.GetStream())
                    {
                        requests++; var bytes = new List<byte>(); var one = new byte[1];
                        while (true) { if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        header = Encoding.ASCII.GetString(bytes.ToArray()); var payload = Encoding.UTF8.GetBytes(status == 503 ? "{}" : "{\"workflows\":[{\"id\":\"workflow\",\"comment\":\"ä comment\"}]}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : " Service Unavailable") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token); await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var client = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Workflows; client.DisableOfflineModeSimulation();
                    var options = mode == "omitted" ? null : mode == "visible" ? new WorkflowQueryOptions { DocumentId = "doc with/slash", IncludeDocumentVisibility = true, IncludeWhereAttachment = true, CreationDateFrom = "2026-10-12T10:00:00+02:00", Types = new[] { "read", "approval" } } :
                        new WorkflowQueryOptions { IncludeWhereAttachment = false, Types = new string[0], Responded = false };
                    if (status == 503) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await client.GetWorkflowsAsync("context", options, cancellation.Token)));
                    else Assert.That((await client.GetWorkflowsAsync("context", options, cancellation.Token)).Single().Comment, Is.EqualTo("ä comment")); await server;
                    var target = header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ')[1]; var path = target.Split('?')[0]; Assert.That(path, Is.EqualTo("/bridge/v2/workflows")); Assert.That(header.StartsWith("GET ", StringComparison.Ordinal), Is.True); Assert.That(header, Does.Not.Contain("Content-Length:"));
                    var query = target.Contains("?") ? target.Substring(target.IndexOf('?') + 1).Split('&').Select(pair => pair.Split(new[] { '=' }, 2)).ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1].Replace("+", " "))) : new Dictionary<string, string>();
                    if (mode == "omitted") Assert.That(query, Is.Empty);
                    if (mode == "visible") { Assert.That(query["document-id"], Is.EqualTo("doc with/slash")); Assert.That(query["include-document-visibility"], Is.EqualTo("true")); Assert.That(query["types"], Is.EqualTo("read,approval")); Assert.That(query["creation-date-from"], Is.EqualTo("2026-10-12T10:00:00+02:00")); Assert.That(query.ContainsKey("role"), Is.False); }
                    if (mode == "empty") { Assert.That(query["types"], Is.Empty); Assert.That(query["include-where-attachment"], Is.EqualTo("false")); Assert.That(query["responded"], Is.EqualTo("false")); }
                    Assert.That(requests, Is.EqualTo(expected)); Assert.That(listener.Pending(), Is.False);
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
