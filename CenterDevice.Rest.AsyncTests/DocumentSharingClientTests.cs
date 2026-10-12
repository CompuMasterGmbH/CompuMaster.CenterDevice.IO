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
    public class DocumentSharingClientTests
    {
        [TestCase(false, false, true), TestCase(false, true, true), TestCase(true, false, true), TestCase(true, true, true)]
        [TestCase(false, false, false), TestCase(false, true, false), TestCase(true, false, false), TestCase(true, true, false)]
        public async Task SelectedRecipientsAndDocumentIdsSnapshotBeforeAuthorization(bool bulk, bool sync, bool share)
        {
            var users = new List<string> { "user" }; var emails = new List<string> { "bad-address" }; var documents = new List<string> { "selected" };
            int enumerations = 0;
            IEnumerable<string> Emails() { enumerations++; foreach (var email in emails) yield return email; }
            var recipients = new DocumentSharingRecipients { Users = users, Emails = Emails(), Groups = new string[0] };
            var state = new State { BeforeAuthorize = () => { users.Clear(); emails.Clear(); documents.Clear(); recipients.Groups = new[] { "later" }; } };
            var result = await Invoke(state, bulk, sync, share, recipients, documents);
            Assert.That(result.AllSucceeded, Is.True);
            var body = Body(state);
            Assert.That(body.Value<string>("action"), Is.EqualTo(share ? "share" : "unshare"));
            Assert.That(body["params"]["users"].Values<string>(), Is.EqualTo(new[] { "user" }));
            Assert.That(body["params"]["emails"].Values<string>(), Is.EqualTo(new[] { "bad-address" }));
            Assert.That(body["params"]["groups"], Is.TypeOf<JArray>().And.Empty);
            Assert.That(body["params"]["comment"]?.Value<string>(), Is.EqualTo(share ? "ä notification" : null));
            Assert.That(body["params"]["documents"]?.Values<string>(), Is.EqualTo(bulk ? new[] { "selected" } : null));
            Assert.That(state.Request.Resource, Is.EqualTo(bulk ? "v2/documents" : "v2/document/selected"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(enumerations, Is.EqualTo(1));
            Assert.That(((IList<string>)result.FailedDocuments).IsReadOnly, Is.True);
        }

        [TestCase(false), TestCase(true)]
        public async Task InvitationsAreSuccessAndUnknownErrorCodesArePreserved(bool sync)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = "{\"invited\":[{\"email\":\"PERSON@example.test\",\"user-id\":\"created\"}],\"not-invited\":[{\"email\":\"bad-address\",\"code\":9000000000}],\"failed-users\":[],\"failed-groups\":[]}" };
            var result = await Invoke(state, true, sync, true, new DocumentSharingRecipients { Emails = new[] { "person@example.test", "bad-address" } });
            Assert.That(result.Invited.Single().Email, Is.EqualTo("PERSON@example.test"));
            Assert.That(result.Invited.Single().UserId, Is.EqualTo("created"));
            Assert.That(result.NotInvited.Single().Code, Is.EqualTo(9000000000L));
            Assert.That(result.AllSucceeded, Is.False);
            Assert.That(state.Calls, Is.EqualTo(1));
            state.Json = "{\"invited\":[{\"email\":\"person@example.test\",\"user-id\":\"created\"}]}";
            result = await Invoke(state, true, sync, true, new DocumentSharingRecipients { Emails = new[] { "person@example.test" } });
            Assert.That(result.AllSucceeded, Is.True);
        }

        [TestCase(false), TestCase(true)]
        public async Task BulkFailuresPreserveServerOrderAndCanonicalGuidIdentity(bool share)
        {
            var selected = "6bc23208-2243-43f9-84b2-493715161adc";
            var state = new State { Status = HttpStatusCode.OK,
                Json = "{\"failed-documents\":[\"selected\"],\"failed-users\":[\"6BC23208-2243-43F9-84B2-493715161ADC\"],\"failed-groups\":[\"group\"],\"failed-emails\":[]}" };
            if (!share) state.Json = state.Json.Replace("\"failed-emails\":[]", "\"failed-emails\":[\"bad-address\"]");
            var result = await Invoke(state, true, false, share, new DocumentSharingRecipients { Users = new[] { selected }, Groups = new[] { "group" }, Emails = new[] { "bad-address" } });
            Assert.That(result.AllSucceeded, Is.False);
            Assert.That(result.FailedDocuments, Is.EqualTo(new[] { "selected" }));
            Assert.That(result.FailedUsers.Single(), Is.EqualTo(selected.ToUpperInvariant()));
            Assert.That(result.FailedGroups, Is.EqualTo(new[] { "group" }));
            Assert.That(result.FailedEmails, Is.EqualTo(share ? new string[0] : new[] { "bad-address" }));
        }

        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task RemoveAllSharingsUsesSeparateExplicitEmptyRecipientCommand(bool bulk, bool sync)
        {
            var state = new State(); var result = await Invoke(state, bulk, sync, false, null, all: true);
            Assert.That(result.AllSucceeded, Is.True);
            var parameters = (JObject)Body(state)["params"];
            Assert.That(parameters.Properties().Select(property => property.Name), Is.EqualTo(bulk ? new[] { "documents" } : new string[0]));
        }

        [TestCase(null), TestCase("")]
        public async Task SharingOmittedAndEmptyCommentsAndRecipientsRemainDistinct(string comment)
        {
            var state = new State();
            await new Single(state).ShareAsync("context", "with space/slash", new DocumentSharingRecipients { Users = new[] { "user" } }, comment);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/document/with%20space%2Fslash"));
            var parameters = Body(state)["params"];
            Assert.That(parameters["comment"]?.Value<string>(), Is.EqualTo(comment));
            Assert.That(parameters["emails"], Is.Null); Assert.That(parameters["groups"], Is.Null);
        }

        [TestCase("{}"), TestCase("null"), TestCase("[]"), TestCase("{\"failed-users\":null}"), TestCase("{\"failed-users\":[null]}")]
        [TestCase("{\"failed-users\":[1]}"), TestCase("{\"failed-users\":[\"foreign\"]}"), TestCase("{\"failed-users\":[\"user\",\"user\"]}")]
        [TestCase("{\"failed-documents\":[\"foreign\"]}"), TestCase("{\"failed-groups\":[\"foreign\"]}")]
        [TestCase("{\"invited\":[{\"email\":\"foreign\",\"user-id\":\"created\"}]}")]
        [TestCase("{\"invited\":[{\"email\":\"bad-address\",\"user-id\":null}]}")]
        [TestCase("{\"not-invited\":[{\"email\":\"bad-address\",\"code\":\"1\"}]}")]
        [TestCase("{\"not-invited\":[{\"email\":\"bad-address\",\"code\":1.5}]}")]
        [TestCase("{\"invited\":null}"), TestCase("{\"not-invited\":{}}")]
        [TestCase("{\"invited\":[{\"email\":\"bad-address\",\"user-id\":\"created\"}],\"not-invited\":[{\"email\":\"bad-address\",\"code\":1}]}")]
        [TestCase("{\"failed-users\":[\"user\"]} {}")]
        [TestCase("{\"failed-users\":[\"foreign\"],\"failed-users\":[\"user\"]}")]
        public void MalformedUnselectedRepeatedAndAmbiguousResponsesRejectWithoutReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await Invoke(state, true, false, true,
                new DocumentSharingRecipients { Users = new[] { "user" }, Emails = new[] { "bad-address" } })));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(true), TestCase(false)]
        public void ResponseCannotReportOutcomesFromTheOtherSharingAction(bool share)
        {
            var state = new State { Status = HttpStatusCode.OK,
                Json = share ? "{\"failed-emails\":[\"bad-address\"]}" : "{\"invited\":[{\"email\":\"bad-address\",\"user-id\":\"created\"}]}" };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await Invoke(state, true, false, share,
                new DocumentSharingRecipients { Emails = new[] { "bad-address" } })));
        }

        [TestCase("null"), TestCase("empty"), TestCase("blank"), TestCase("null-item")]
        public void SelectedUnsharingNeverTurnsAnEmptySelectionIntoUnshareAll(string problem)
        {
            var state = new State();
            var recipients = problem == "null" ? null : new DocumentSharingRecipients { Users = problem == "empty" ? new string[0] : new[] { problem == "blank" ? " " : null } };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await Invoke(state, true, false, false, recipients)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void SingleSelectionRejectsUnusableDocumentBeforeAuthorization(string id)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Single(state).UnshareAllAsync("context", id)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("null"), TestCase("empty"), TestCase("blank"), TestCase("null-item")]
        public void BulkSelectionRejectsUnusableDocumentsBeforeAuthorization(string problem)
        {
            var state = new State();
            var documents = problem == "null" ? null : problem == "empty" ? new string[0] : new[] { problem == "blank" ? " " : null };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Bulk(state).UnshareAllAsync("context", documents)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void ServerErrorsRetainTheirOriginalCauseAndNeverReplay(int status, Type type)
        {
            var cause = new IOException("isolated cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause, Json = "{}" };
            var error = Assert.ThrowsAsync(type, (Func<Task>)(async () => await Invoke(state, false, false, true, new DocumentSharingRecipients { Users = new[] { "user" } })));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task NativeCancellationReachesAuthorizationAndHttpAndNeverEnumeratesPreCanceledInput()
        {
            var state = new State { Pending = true };
            IEnumerable<string> Failing() { yield return Throw(); }
            string Throw() => throw new AssertionException("Canceled input was enumerated.");
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Bulk(state).ShareAsync("context", Failing(), new DocumentSharingRecipients { Users = Failing() }, cancellationToken: source.Token)));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var source = new CancellationTokenSource())
            {
                var pending = new Single(state).UnshareAllAsync("context", "selected", source.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token));
                source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
                Assert.That(state.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(200), TestCase(204), TestCase(503)]
        public async Task ActualRestSharpWirePreservesPrefixNestedRecipientsUnicodeAndNoWriteReplay(int status)
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
                        var length = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                        var body = new byte[length]; int count = 0;
                        while (count < length) { var read = await stream.ReadAsync(body, count, length - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        sent = Encoding.UTF8.GetString(body);
                        var replyBody = status == 200 ? "{\"invited\":[{\"email\":\"person@example.test\",\"user-id\":\"created\"}]}" : status == 503 ? "{}" : "";
                        var payload = Encoding.UTF8.GetBytes(replyBody);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                        if (payload.Length > 0) await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var state = new State();
                    var factory = new Factory(state, new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.Documents; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        var result = await client.ShareAsync("context", new[] { "selected" }, new DocumentSharingRecipients { Emails = new[] { "person@example.test" } }, "ä notification", source.Token);
                        Assert.That(result.AllSucceeded, Is.True);
                        Assert.That(result.Invited.Count, Is.EqualTo(status == 200 ? 1 : 0));
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/documents HTTP/1.1"));
                    Assert.That(JObject.Parse(sent)["params"]["comment"].Value<string>(), Is.EqualTo("ä notification"));
                    Assert.That(JObject.Parse(sent)["params"]["documents"].Values<string>(), Is.EqualTo(new[] { "selected" }));
                    Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static Task<DocumentSharingResult> Invoke(State state, bool bulk, bool sync, bool share, DocumentSharingRecipients recipients,
            IEnumerable<string> documents = null, bool all = false)
        {
            documents = documents ?? new[] { "selected" };
            if (bulk)
            {
                var client = new Bulk(state);
                if (all) return sync ? Task.FromResult(client.UnshareAll("context", documents)) : client.UnshareAllAsync("context", documents);
                if (share) return sync ? Task.FromResult(client.Share("context", documents, recipients, "ä notification")) : client.ShareAsync("context", documents, recipients, "ä notification");
                return sync ? Task.FromResult(client.Unshare("context", documents, recipients)) : client.UnshareAsync("context", documents, recipients);
            }
            var single = new Single(state);
            if (all) return sync ? Task.FromResult(single.UnshareAll("context", "selected")) : single.UnshareAllAsync("context", "selected");
            if (share) return sync ? Task.FromResult(single.Share("context", "selected", recipients, "ä notification")) : single.ShareAsync("context", "selected", recipients, "ä notification");
            return sync ? Task.FromResult(single.Unshare("context", "selected", recipients)) : single.UnshareAsync("context", "selected", recipients);
        }
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-sharing-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(State state, Config config) : base(state, config, null, "v2/") { }
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
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.NoContent;
            internal string Json, Context;
            internal Exception Error;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            {
                Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token)
            {
                Calls++; Request = request; Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = Status, Content = Json, ErrorException = Error };
            }
        }
    }
}
