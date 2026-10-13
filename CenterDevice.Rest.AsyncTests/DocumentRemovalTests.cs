using CenterDevice.Rest.Clients;
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
    public class DocumentRemovalTests
    {
        [TestCaseSource(nameof(WireCases))]
        public async Task SelectedAndBulkRemovalDistinguishAllWireChoices(bool bulk, bool erase, bool sync, bool? detailed, bool partial)
        {
            const string id = "selected/ö ?";
            var failure = detailed == true ? new JArray(new JObject { ["code"] = 9876543210123456789UL, ["message"] = " original failure ", ["data"] = new JObject { ["document-id"] = id, ["date"] = "2025-01-02" }, ["future"] = false }) : new JArray(id);
            var state = new State { Status = partial ? HttpStatusCode.OK : HttpStatusCode.NoContent, Json = partial ? new JObject { ["failed-documents"] = failure }.ToString() : "ignored after 204" };
            var result = await Run(state, bulk, erase, sync, new[] { id, "other", id }, detailed);
            Assert.That(state.Request.Method, Is.EqualTo(!bulk && !erase ? Method.Delete : Method.Post));
            Assert.That(state.Request.Resource, Is.EqualTo(bulk ? "v2/documents" : "v2/document/selected%2F%C3%B6%20%3F").IgnoreCase);
            var query = state.Request.Parameters.SingleOrDefault(p => p.Name == "include-error-info");
            Assert.That(query?.Value, Is.EqualTo(detailed.HasValue ? detailed.Value ? "true" : "false" : null));
            var body = state.Request.Parameters.OfType<BodyParameter>().SingleOrDefault();
            if (!bulk && !erase) Assert.That(body, Is.Null);
            else
            {
                var json = JObject.FromObject(body.Value);
                Assert.That(json.Value<string>("action"), Is.EqualTo(erase ? "erase" : "delete"));
                if (bulk) Assert.That(json["params"]["documents"].Values<string>(), Is.EqualTo(new[] { id, "other", id }));
                else Assert.That(json["params"], Is.Null);
            }
            Assert.That(result.AllSucceeded, Is.EqualTo(!partial));
            Assert.That(result.FailedDocuments, Is.EqualTo(partial ? new[] { id } : new string[0]));
            Assert.That(result.RawJson, Is.EqualTo(partial ? state.Json : null));
            Assert.That(state.Calls, Is.EqualTo(1)); Assert.That(state.Context, Is.EqualTo("context"));
            if (partial)
            {
                var detail = result.Failures.Single();
                Assert.That(detail.ErrorCode, Is.EqualTo(detailed == true ? "9876543210123456789" : null));
                Assert.That(detail.Message, Is.EqualTo(detailed == true ? " original failure " : null));
                if (detailed == true)
                {
                    detail.Data["document-id"] = "forged"; detail.Details["message"] = "forged";
                    Assert.That(detail.Data.Value<string>("document-id"), Is.EqualTo(id));
                    Assert.That(detail.Data.Value<string>("date"), Is.EqualTo("2025-01-02"));
                    Assert.That(detail.Details.Value<bool>("future"), Is.False);
                    Assert.That(detail.Message, Is.EqualTo(" original failure "));
                }
                else { Assert.That(detail.Data, Is.Null); Assert.That(detail.Details, Is.Null); }
                Assert.Throws<NotSupportedException>((Action)(() => ((IList<DocumentRemovalFailure>)result.Failures).Clear()));
            }
        }
        public static IEnumerable<TestCaseData> WireCases()
        {
            foreach (var bulk in new[] { false, true }) foreach (var erase in new[] { false, true }) foreach (var sync in new[] { false, true })
                foreach (var detailed in bulk ? new bool?[] { null, false, true } : new bool?[] { null }) foreach (var partial in new[] { false, true })
                    yield return new TestCaseData(bulk, erase, sync, detailed, partial);
        }
        [TestCase(null), TestCase(""), TestCase("{}"), TestCase("{\"failed-documents\":[]}"), TestCase("{\"failed-documents\":[null]}"), TestCase("{\"failed-documents\":[42]}"), TestCase("{\"failed-documents\":[\"foreign\"]}"), TestCase("{\"failed-documents\":[\"selected\"],\"failed-documents\":[\"selected\"]}"), TestCase("{\"failed-documents\":[\"selected\"]}{}")]
        public void InvalidIdentifierFailureCannotBecomeSuccess(string json)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = json };
            var error = ExpectAsync<InvalidResponseDataException>(async () => await Run(state, false, true, false, new[] { "selected" }, null));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }
        [TestCase("{}"), TestCase("{\"code\":1,\"message\":\"error\",\"data\":{\"document-id\":\"foreign\"}}"), TestCase("{\"code\":1.1,\"message\":\"error\",\"data\":{\"document-id\":\"selected\"}}"), TestCase("{\"code\":1,\"message\":false,\"data\":{\"document-id\":\"selected\"}}"), TestCase("{\"code\":1,\"message\":\"error\",\"data\":{\"document-id\":null}}"), TestCase("\"selected\"")]
        public void InvalidDetailedFailureCannotBecomeSuccess(string entry)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = "{\"failed-documents\":[" + entry + "]}" };
            var error = ExpectAsync<InvalidResponseDataException>(async () => await Run(state, true, false, false, new[] { "selected" }, true));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }
        [Test]
        public async Task CanonicalIdsAndDuplicateFailuresRetainServerSpellingAndOrder()
        {
            const string id = "9E91DAD2-0820-4BC0-BF37-D923191CE44D";
            var state = new State { Status = HttpStatusCode.OK, Json = new JObject { ["failed-documents"] = new JArray(id.ToLowerInvariant(), id.ToLowerInvariant()) }.ToString() };
            var result = await Run(state, true, true, false, new[] { id }, false);
            Assert.That(result.FailedDocuments, Is.EqualTo(new[] { id.ToLowerInvariant(), id.ToLowerInvariant() }));
        }
        [Test]
        public void SelectedFailureCannotClaimMultipleDocuments()
        { var state = new State { Status = HttpStatusCode.OK, Json = "{\"failed-documents\":[\"selected\",\"selected\"]}" }; ExpectAsync<InvalidResponseDataException>(async () => await Run(state, false, true, false, new[] { "selected" }, null)); }
        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectedIdentityDoesNotAuthorize(string id)
        { var state = new State(); ExpectAsync<ArgumentException>(async () => await Run(state, false, true, false, new[] { id }, null)); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(0), TestCase(1), TestCase(2)]
        public void InvalidBulkSelectionDoesNotAuthorize(int choice)
        { var state = new State(); ExpectAsync(choice == 0 ? typeof(ArgumentNullException) : typeof(ArgumentException), async () => await Run(state, true, false, false, choice == 0 ? null : choice == 1 ? new string[0] : new[] { " " }, null)); Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task BulkEnumerationAndSnapshotPrecedeAuthorization()
        {
            var values = new List<string> { "original", "original" }; int enumerations = 0;
            IEnumerable<string> Read() { enumerations++; foreach (var value in values) yield return value; }
            var state = new State { OnAuthorize = () => values.Add("later") };
            await new Bulk(state).EraseDocumentsAsync("context", Read(), true);
            var json = JObject.FromObject(state.Request.Parameters.OfType<BodyParameter>().Single().Value);
            Assert.That(json["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "original", "original" })); Assert.That(enumerations, Is.EqualTo(1));
        }
        [TestCase(false), TestCase(true)]
        public void PreCanceledOperationsDoNotEnumerateOrAuthorize(bool bulk)
        { var state = new State(); using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); ExpectAsync<OperationCanceledException>(async () => { if (bulk) await new Bulk(state).EraseDocumentsAsync("context", ThrowIfEnumerated(), true, cancellation.Token); else await new Single(state).EraseDocumentAsync("context", null, cancellation.Token); }); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task ActiveNativeIoReceivesCancellationWithoutReplay()
        {
            var state = new State { Pending = true }; using (var cancellation = new CancellationTokenSource())
            { var pending = new Bulk(state).MoveDocumentsToTrashAsync("context", new[] { "selected" }, false, cancellation.Token); await state.Entered.Task; cancellation.Cancel(); ExpectAsync<TaskCanceledException>(async () => await pending); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); }
            Assert.That(state.Calls, Is.EqualTo(1));
        }
        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void OriginalHttpErrorsRemainIntactWithoutReplay(int status, Type type)
        { var cause = new IOException("isolated failure"); var state = new State { Status = (HttpStatusCode)status, Error = cause, Json = "{\"code\":300,\"message\":\"test failure\"}" }; var error = ExpectAsync(type, async () => await Run(state, true, true, false, new[] { "selected" }, true)); Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(false, false, false, 204), TestCase(false, true, false, 200), TestCase(true, false, true, 200), TestCase(true, true, true, 204), TestCase(true, true, false, 503), TestCase(false, true, false, 503)]
        public async Task PublicFactoryUsesActualHttpShapesUtf8ErrorsAndNoUncertainReplay(bool bulk, bool erase, bool detailed, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string capturedHeader = null, capturedBody = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(one, 0, 1, cancellation.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        capturedHeader = Encoding.ASCII.GetString(bytes.ToArray());
                        var length = capturedHeader.Split(new[] { "\r\n" }, StringSplitOptions.None).SingleOrDefault(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                        var body = new byte[length == null ? 0 : int.Parse(length.Substring(length.IndexOf(':') + 1).Trim())];
                        for (int offset = 0; offset < body.Length;) { int count = await stream.ReadAsync(body, offset, body.Length - offset, cancellation.Token); if (count == 0) throw new EndOfStreamException(); offset += count; }
                        capturedBody = Encoding.UTF8.GetString(body);
                        var json = status == 204 ? "" : status == 503 ? "{\"code\":300,\"message\":\"isolated failure\"}" : detailed ? "{\"failed-documents\":[{\"code\":999999999999999999999999,\"message\":\"unverändert ä\",\"data\":{\"document-id\":\"owned\"}}]}" : "{\"failed-documents\":[\"owned\"]}";
                        var payload = Encoding.UTF8.GetBytes(json);
                        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 204 ? " No Content" : status == 200 ? " OK" : " Service Unavailable") + "\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, 0, header.Length, cancellation.Token); if (payload.Length > 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new State(), new Configuration("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var selected = factory.Document; var documents = factory.Documents; selected.DisableOfflineModeSimulation(); documents.DisableOfflineModeSimulation();
                    Func<Task<DocumentRemovalResult>> execute = () => bulk ? erase ? documents.EraseDocumentsAsync("context", new[] { "owned" }, detailed, cancellation.Token) : documents.MoveDocumentsToTrashAsync("context", new[] { "owned" }, detailed, cancellation.Token) : erase ? selected.EraseDocumentAsync("context", "owned", cancellation.Token) : selected.MoveDocumentToTrashAsync("context", "owned", cancellation.Token);
                    if (status == 503) Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await execute()));
                    else { var result = await execute(); Assert.That(result.AllSucceeded, Is.EqualTo(status == 204)); if (status == 200 && detailed) { Assert.That(result.Failures.Single().ErrorCode, Is.EqualTo("999999999999999999999999")); Assert.That(result.Failures.Single().Message, Is.EqualTo("unverändert ä")); } }
                    await server;
                    var route = capturedHeader.Split(new[] { "\r\n" }, StringSplitOptions.None)[0];
                    Assert.That(route, Does.StartWith((!bulk && !erase ? "DELETE" : "POST") + " /bridge/v2/" + (bulk ? "documents?include-error-info=" + (detailed ? "true" : "false") : "document/owned") + " "));
                    if (!bulk && !erase) Assert.That(capturedBody, Is.Empty);
                    else { var json = JObject.Parse(capturedBody); Assert.That(json.Value<string>("action"), Is.EqualTo(erase ? "erase" : "delete")); if (bulk) Assert.That(json["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "owned" })); else Assert.That(json["params"], Is.Null); }
                    Assert.That(listener.Pending(), Is.False, "An uncertain removal must not reconnect for replay.");
                }
                finally { cancellation.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }
        private sealed class Factory : CenterDeviceClientBase { internal Factory(State state, Configuration configuration) : base(state, configuration, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private static T ExpectAsync<T>(Func<Task> action) where T : Exception => Assert.ThrowsAsync<T>(action);
        private static Exception ExpectAsync(Type type, Func<Task> action) => Assert.ThrowsAsync(type, action);
        private static IEnumerable<string> ThrowIfEnumerated() { foreach (var value in new string[0]) yield return value; throw new AssertionException("Canceled selection enumerated."); }
        private static Task<DocumentRemovalResult> Run(State state, bool bulk, bool erase, bool sync, string[] ids, bool? detailed)
        {
            if (bulk) { var client = new Bulk(state); return sync ? Task.FromResult(erase ? client.EraseDocuments("context", ids, detailed) : client.MoveDocumentsToTrash("context", ids, detailed)) : erase ? client.EraseDocumentsAsync("context", ids, detailed) : client.MoveDocumentsToTrashAsync("context", ids, detailed); }
            var selected = new Single(state); return sync ? Task.FromResult(erase ? selected.EraseDocument("context", ids[0]) : selected.MoveDocumentToTrash("context", ids[0])) : erase ? selected.EraseDocumentAsync("context", ids[0]) : selected.MoveDocumentToTrashAsync("context", ids[0]);
        }
        private sealed class Configuration : IRestClientConfiguration { internal Configuration(string address = "https://unused.invalid/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-removal-tests"; }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal HttpStatusCode Status = HttpStatusCode.NoContent; internal string Json, Context; internal Exception Error; internal RestRequest Request; internal CancellationToken Token;
            internal int Calls, Authorizations; internal bool Pending; internal Action OnAuthorize; internal TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native async authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; OnAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(OAuthInfo info, RestRequest request, CancellationToken token) { Calls++; Context = info.UserId; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json, ErrorException = Error }; }
        }
        private sealed class Single : DocumentRestClient { private readonly State state; internal Single(State state) : base(state, new Configuration(), null, null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(info, request, token); }
        private sealed class Bulk : DocumentsRestClient { private readonly State state; internal Bulk(State state) : base(state, new Configuration(), null, null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(info, request, token); }
    }
}
