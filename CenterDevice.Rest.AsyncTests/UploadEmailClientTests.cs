using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.UploadEmails;
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
    public class UploadEmailClientTests
    {
        private static JObject Record(string id = "endpoint") => new JObject { ["id"] = id, ["name"] = "ä importer", ["email"] = "secret-import@example.invalid" };
        private static UploadEmailOptions Options() => new UploadEmailOptions { Name = "ä importer" };

        [TestCase(false), TestCase(true)]
        public async Task CreationPreservesOmittedDefaultsAndConfirmsIdentity(bool sync)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" }; var client = new Client(state);
            var result = sync ? client.CreateUploadEmail("context", "secret-import@example.invalid", Options()) : await client.CreateUploadEmailAsync("context", "secret-import@example.invalid", Options());
            Assert.That(result, Is.EqualTo("created")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/upload-emails"));
            Assert.That(Body(state).Properties().Select(item => item.Name), Is.EqualTo(new[] { "name", "email" }));
            Assert.That(Body(state).Value<string>("name"), Is.EqualTo("ä importer")); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(UploadEmailStorage.None, "none"), TestCase(UploadEmailStorage.Text, "text"), TestCase(UploadEmailStorage.Original, "original")]
        public async Task ExplicitStorageAndFlagsKeepTheirWireValues(UploadEmailStorage storage, string value)
        {
            var options = Options(); options.StoreEmail = storage; options.ExtractAttachments = storage == UploadEmailStorage.None; options.AttachmentTextIndexing = storage == UploadEmailStorage.Original;
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" };
            await new Client(state).CreateUploadEmailAsync("context", "secret-import@example.invalid", options);
            Assert.That(Body(state).Value<string>("store-email"), Is.EqualTo(value));
            Assert.That(Body(state).Value<bool>("extract-attachments"), Is.EqualTo(options.ExtractAttachments));
            Assert.That(Body(state).Value<bool>("attachment-text-indexing"), Is.EqualTo(options.AttachmentTextIndexing));
        }

        [Test]
        public async Task SnapshotEnumeratesOnceBeforeAuthorizationAndDoesNotNormalizeStrings()
        {
            var count = 0; IEnumerable<string> Tags() { count++; yield return "ä tag"; yield return ""; yield return "ä tag"; }
            var extensions = new List<string> { ".PDF", "docx" }; var options = Options(); options.Tags = Tags(); options.WhitelistedFileExtensions = extensions; options.CollectionId = "visible";
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}", BeforeAuthorize = () => { options.CollectionId = "changed"; options.Name = "changed"; extensions.Clear(); } };
            await new Client(state).CreateUploadEmailAsync("context", "secret-import@example.invalid", options);
            Assert.That(count, Is.EqualTo(1)); Assert.That(Body(state).Value<string>("collection"), Is.EqualTo("visible"));
            Assert.That(Body(state).Value<string>("name"), Is.EqualTo("ä importer"));
            Assert.That(Body(state)["tags"].Values<string>(), Is.EqualTo(new[] { "ä tag", "", "ä tag" }));
            Assert.That(Body(state)["whitelisted-file-extensions"].Values<string>(), Is.EqualTo(new[] { ".PDF", "docx" }));
        }

        [TestCase(false), TestCase(true)]
        public async Task PartialUpdatePreservesOmissionAndExplicitEmptyArraysWhileAllowingFilterSwitch(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var options = new UploadEmailOptions { Name = "", AttachmentTextIndexing = true, Tags = new string[0], WhitelistedFileExtensions = new string[0], BlacklistedFileExtensions = new[] { ".JPG" } };
            var client = new Client(state);
            if (sync) client.UpdateUploadEmail("context", "id with/slash", options); else await client.UpdateUploadEmailAsync("context", "id with/slash", options);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/upload-email/id%20with%2Fslash")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(Body(state)["email"], Is.Null); Assert.That(Body(state)["store-email"], Is.Null); Assert.That(Body(state)["extract-attachments"], Is.Null);
            Assert.That(Body(state).Value<string>("name"), Is.Empty); Assert.That(Body(state)["tags"], Is.Empty); Assert.That(Body(state)["whitelisted-file-extensions"], Is.Empty);
            Assert.That(Body(state)["blacklisted-file-extensions"].Values<string>(), Is.EqualTo(new[] { ".JPG" }));
        }

        [TestCase("empty"), TestCase("extract"), TestCase("store")]
        public async Task UpdatesDoNotGuessExistingStateOrApplyCreationDefaults(string selection)
        {
            var options = new UploadEmailOptions(); if (selection == "extract") options.ExtractAttachments = false; if (selection == "store") options.StoreEmail = UploadEmailStorage.None;
            var state = new State { Status = HttpStatusCode.NoContent }; await new Client(state).UpdateUploadEmailAsync("context", "endpoint", options);
            Assert.That(Body(state).Count, Is.EqualTo(selection == "empty" ? 0 : 1));
        }

        [TestCase("name"), TestCase("email"), TestCase("options"), TestCase("enum"), TestCase("destinations"), TestCase("blank-location"), TestCase("filters"), TestCase("extract"), TestCase("index-none"), TestCase("index-text"), TestCase("null-tag")]
        public void InvalidCreateCombinationsFailBeforeAuthorization(string problem)
        {
            var options = Options();
            if (problem == "name") options.Name = null; if (problem == "enum") options.StoreEmail = (UploadEmailStorage)99;
            if (problem == "destinations") { options.CollectionId = "collection"; options.FolderId = "folder"; }
            if (problem == "blank-location") options.FolderId = " "; if (problem == "filters") { options.WhitelistedFileExtensions = new[] { "pdf" }; options.BlacklistedFileExtensions = new[] { "jpg" }; }
            if (problem == "extract") options.ExtractAttachments = false; if (problem == "index-none") options.AttachmentTextIndexing = true;
            if (problem == "index-text") { options.StoreEmail = UploadEmailStorage.Text; options.AttachmentTextIndexing = true; }
            if (problem == "null-tag") options.Tags = new string[] { null };
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).CreateUploadEmailAsync("context", problem == "email" ? " " : "secret-import@example.invalid", problem == "options" ? null : options)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public async Task SelectedAndListMetadataPreserveUnknownDefaultsAndImmutableOriginalArrays(bool sync)
        {
            var data = Record(); data["store-email"] = "future-storage"; data["extract-attachments"] = false; data["tags"] = new JArray("ä", ""); data["whitelisted-file-extensions"] = new JArray();
            var state = new State { Json = data.ToString() }; var client = new Client(state);
            var item = sync ? client.GetUploadEmail("context", "endpoint") : await client.GetUploadEmailAsync("context", "endpoint");
            Assert.That(item.StoreEmail, Is.EqualTo("future-storage")); Assert.That(item.ExtractAttachments, Is.False); Assert.That(item.AttachmentTextIndexing, Is.Null);
            Assert.That(item.Email, Is.EqualTo("secret-import@example.invalid")); Assert.That(item.BlacklistedFileExtensions, Is.Null); Assert.That(item.WhitelistedFileExtensions, Is.Empty);
            Assert.That(((IList<string>)item.Tags).IsReadOnly, Is.True); Assert.That(item.ToString(), Does.Not.Contain(item.Email));
            Assert.That(typeof(UploadEmail).GetProperty("Owner"), Is.Null);
            state.Json = new JObject { ["upload-emails"] = new JArray(Record("second"), data) }.ToString();
            var page = sync ? client.GetUploadEmails("context") : await client.GetUploadEmailsAsync("context");
            Assert.That(page.Select(record => record.Id), Is.EqualTo(new[] { "second", "endpoint" })); Assert.That(((IList<UploadEmail>)page).IsReadOnly, Is.True);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString), Is.Empty);
        }

        [TestCase("identity"), TestCase("email"), TestCase("flag"), TestCase("list"), TestCase("null-list-entry"), TestCase("destinations")]
        public void MalformedMetadataRetainsCauseWithoutAddressInValidationDiagnostic(string problem)
        {
            var data = Record(); if (problem == "identity") data["id"] = "different"; if (problem == "email") data["email"] = 1;
            if (problem == "flag") data["extract-attachments"] = "false"; if (problem == "list") data["tags"] = "csv";
            if (problem == "null-list-entry") data["tags"] = new JArray(JValue.CreateNull()); if (problem == "destinations") { data["collection"] = "one"; data["folder"] = "two"; }
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetUploadEmailAsync("context", "endpoint")));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(error.ToString(), Does.Not.Contain("secret-import@example.invalid"));
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":null}"), TestCase("{\"id\":1}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":\"created\"} {}")]
        public void MalformedCreationRetainsCauseAndDoesNotReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateUploadEmailAsync("context", "secret-import@example.invalid", Options())));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("absent"), TestCase("shape"), TestCase("entry"), TestCase("duplicate-guid")]
        public void MalformedListsAndCanonicalDuplicateIdsFail(string problem)
        {
            var data = new JObject(); if (problem == "shape") data["upload-emails"] = "invalid";
            if (problem == "entry") data["upload-emails"] = new JArray(1);
            if (problem == "duplicate-guid") data["upload-emails"] = new JArray(Record("01234567-89ab-cdef-0123-456789abcdef"), Record("{01234567-89AB-CDEF-0123-456789ABCDEF}"));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetUploadEmailsAsync("context")));
        }

        [TestCase(false), TestCase(true)]
        public async Task SelectedRemovalHasNoBodyAndPreservesExactIdentifier(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (sync) client.RemoveUploadEmail("context", "endpoint"); else await client.RemoveUploadEmailAsync("context", "endpoint");
            Assert.That(state.Request.Method, Is.EqualTo(Method.Delete)); Assert.That(state.Request.Resource, Is.EqualTo("v2/upload-email/endpoint")); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase("create"), TestCase("get"), TestCase("list"), TestCase("update"), TestCase("remove")]
        public async Task PreCancellationAvoidsAuthorizationAndActiveCancellationReachesIo(string operation)
        {
            Func<Client, CancellationToken, Task> invoke = (client, token) => operation == "create" ? (Task)client.CreateUploadEmailAsync("context", "secret-import@example.invalid", Options(), token) :
                operation == "get" ? client.GetUploadEmailAsync("context", "endpoint", token) : operation == "list" ? client.GetUploadEmailsAsync("context", token) :
                operation == "update" ? client.UpdateUploadEmailAsync("context", "endpoint", new UploadEmailOptions(), token) : client.RemoveUploadEmailAsync("context", "endpoint", token);
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = invoke(new Client(state), cancellation.Token); await state.Entered.Task; cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await invoke(new Client(state), cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }
        [TestCase(403), TestCase(404), TestCase(503)]
        public void CreatorRestrictionsAndUncertainWriteFailuresRetainOriginalCause(int status)
        {
            var cause = new InvalidOperationException("isolated cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).UpdateUploadEmailAsync("context", "endpoint", new UploadEmailOptions { Tags = new string[0] })));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("."), TestCase("..")]
        public void CreationDoesNotReturnAnIdentityThatCannotBeSelected(string id)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = new JObject { ["id"] = id }.ToString() };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateUploadEmailAsync("context", "secret-import@example.invalid", Options())));
        }

        [Test]
        public async Task CanonicalGuidMatchingPreservesOriginalIdentityAndEmptyOwnList()
        {
            var original = "{01234567-89AB-CDEF-0123-456789ABCDEF}"; var state = new State { Json = Record(original).ToString() }; var client = new Client(state);
            Assert.That((await client.GetUploadEmailAsync("context", "01234567-89ab-cdef-0123-456789abcdef")).Id, Is.EqualTo(original));
            state.Json = "{\"upload-emails\":[]}"; Assert.That(await client.GetUploadEmailsAsync("context"), Is.Empty);
        }

        [TestCase("create", 201), TestCase("create", 503), TestCase("get", 200), TestCase("list", 200), TestCase("get", 503), TestCase("update", 204), TestCase("update", 503), TestCase("remove", 204), TestCase("remove", 503)]
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
                        var response = status == 503 ? "{}" : operation == "create" ? "{\"id\":\"created\"}" : operation == "get" ? Record().ToString() : operation == "list" ? new JObject { ["upload-emails"] = new JArray(Record()) }.ToString() : "";
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
                    var client = factory.UploadEmails; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "create") Assert.That(await client.CreateUploadEmailAsync("context", "secret-import@example.invalid", Options(), cancellation.Token), Is.EqualTo("created"));
                        else if (operation == "get") Assert.That((await client.GetUploadEmailAsync("context", "endpoint", cancellation.Token)).Id, Is.EqualTo("endpoint"));
                        else if (operation == "list") Assert.That((await client.GetUploadEmailsAsync("context", cancellation.Token)).Count, Is.EqualTo(1));
                        else if (operation == "update") await client.UpdateUploadEmailAsync("context", "endpoint", new UploadEmailOptions { Tags = new string[0], AttachmentTextIndexing = true }, cancellation.Token);
                        else await client.RemoveUploadEmailAsync("context", "endpoint", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "create" ? "POST /bridge/v2/upload-emails" : operation == "list" ? "GET /bridge/v2/upload-emails" :
                        (operation == "get" ? "GET" : operation == "update" ? "PUT" : "DELETE") + " /bridge/v2/upload-email/endpoint";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    if (operation == "create") Assert.That(JObject.Parse(sent).Value<string>("name"), Is.EqualTo("ä importer"));
                    else if (operation == "update") Assert.That(JObject.Parse(sent).Properties().Select(property => property.Name), Is.EqualTo(new[] { "attachment-text-indexing", "tags" }));
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
        private sealed class Client : UploadEmailsRestClient
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
            public string UserAgent => "isolated-upload-email-tests";
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
