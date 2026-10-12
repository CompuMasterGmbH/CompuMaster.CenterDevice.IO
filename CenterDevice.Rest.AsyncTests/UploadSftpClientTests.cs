using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.UploadSftps;
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
    public class UploadSftpClientTests
    {
        private static JObject Record(string id = "endpoint") => new JObject { ["id"] = id, ["name"] = "ä importer", ["hostname"] = "sftp.example.invalid", ["port"] = 2022,
            ["username"] = "isolated-import-user", ["password"] = "isolated-import-secret", ["server-public-key"] = "original ssh-public-key text" };
        private static UploadSftpOptions Options() => new UploadSftpOptions { Name = "ä importer", Password = "isolated-import-secret" };

        [TestCase(false), TestCase(true)]
        public async Task CreationReturnsConfirmedConnectionWithoutRequiringAbsentNameOrGuessingOwnership(bool sync)
        {
            var data = Record("created"); data.Remove("name"); var state = new State { Status = HttpStatusCode.Created, Json = data.ToString() }; var client = new Client(state);
            var result = sync ? client.CreateUploadSftp("context", Options()) : await client.CreateUploadSftpAsync("context", Options());
            Assert.That(result.Id, Is.EqualTo("created")); Assert.That(result.Hostname, Is.EqualTo("sftp.example.invalid")); Assert.That(result.Port, Is.EqualTo(2022));
            Assert.That(result.Username, Is.EqualTo("isolated-import-user")); Assert.That(result.Password, Is.EqualTo("isolated-import-secret")); Assert.That(result.ServerPublicKey, Is.EqualTo("original ssh-public-key text"));
            Assert.That(result.ToString(), Does.Not.Contain(result.Password)); Assert.That(typeof(UploadSftpConnection).GetProperty("Owner"), Is.Null);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/upload-sftps"));
            Assert.That(Body(state).Properties().Select(item => item.Name), Is.EqualTo(new[] { "name", "password" })); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase(""), TestCase("  ä secret  ")]
        public async Task PasswordPolicyIsServerValidatedWithoutTrimmingOrInventedMinimum(string password)
        {
            var options = Options(); options.Name = ""; options.Password = password; var data = Record("created"); data["password"] = password;
            var state = new State { Status = HttpStatusCode.Created, Json = data.ToString() }; var result = await new Client(state).CreateUploadSftpAsync("context", options);
            Assert.That(Body(state).Value<string>("password"), Is.EqualTo(password)); Assert.That(result.Password, Is.EqualTo(password)); Assert.That(Body(state).Value<string>("name"), Is.Empty);
        }

        [Test]
        public async Task SnapshotEnumeratesTagsOnceBeforeAuthorizationAndPreservesOriginalSelections()
        {
            var count = 0; IEnumerable<string> Tags() { count++; yield return "ä tag"; yield return ""; yield return "ä tag"; }
            var options = Options(); options.Tags = Tags(); options.CollectionId = "visible";
            var state = new State { Status = HttpStatusCode.Created, Json = Record("created").ToString(), BeforeAuthorize = () => { options.CollectionId = "changed"; options.Password = "changed"; options.Name = "changed"; } };
            await new Client(state).CreateUploadSftpAsync("context", options);
            Assert.That(count, Is.EqualTo(1)); Assert.That(Body(state).Value<string>("collection"), Is.EqualTo("visible")); Assert.That(Body(state).Value<string>("password"), Is.EqualTo("isolated-import-secret"));
            Assert.That(Body(state).Value<string>("name"), Is.EqualTo("ä importer")); Assert.That(Body(state)["tags"].Values<string>(), Is.EqualTo(new[] { "ä tag", "", "ä tag" }));
        }

        [TestCase(false), TestCase(true)]
        public async Task PartialUpdatePreservesOmittedPasswordAndExplicitEmptyTagsAndNames(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state); var options = new UploadSftpOptions { Name = "", Tags = new string[0] };
            if (sync) client.UpdateUploadSftp("context", "id with/slash", options); else await client.UpdateUploadSftpAsync("context", "id with/slash", options);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/upload-sftp/id%20with%2Fslash")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(Body(state).Value<string>("name"), Is.Empty); Assert.That(Body(state)["tags"], Is.Empty); Assert.That(Body(state)["password"], Is.Null); Assert.That(Body(state)["collection"], Is.Null);
            await client.UpdateUploadSftpAsync("context", "endpoint", new UploadSftpOptions { Password = "" }); Assert.That(Body(state).Value<string>("password"), Is.Empty); Assert.That(Body(state).Count, Is.EqualTo(1));
        }

        [TestCase("name"), TestCase("password"), TestCase("options"), TestCase("destinations"), TestCase("blank-location"), TestCase("null-tag")]
        public void InvalidCreateFailsBeforeAuthorizationWithoutCredentialDiagnostics(string problem)
        {
            var options = Options(); if (problem == "name") options.Name = null; if (problem == "password") options.Password = null;
            if (problem == "destinations") { options.CollectionId = "collection"; options.FolderId = "folder"; } if (problem == "blank-location") options.FolderId = " "; if (problem == "null-tag") options.Tags = new string[] { null };
            var state = new State(); var error = Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).CreateUploadSftpAsync("context", problem == "options" ? null : options)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(error.ToString(), Does.Not.Contain("isolated-import-secret"));
        }

        [TestCase(false), TestCase(true)]
        public async Task SelectedAndOwnListRetainConnectionIdentityAndUnknownVersusEmptyTags(bool sync)
        {
            var data = Record(); data["tags"] = new JArray("ä", ""); var state = new State { Json = data.ToString() }; var client = new Client(state);
            var result = sync ? client.GetUploadSftp("context", "endpoint") : await client.GetUploadSftpAsync("context", "endpoint");
            Assert.That(result.Id, Is.EqualTo("endpoint")); Assert.That(result.Connection.Password, Is.EqualTo("isolated-import-secret")); Assert.That(result.CollectionId, Is.Null);
            Assert.That(((IList<string>)result.Tags).IsReadOnly, Is.True); Assert.That(result.ToString(), Does.Not.Contain(result.Connection.Password));
            state.Json = new JObject { ["upload-sftps"] = new JArray(Record("second"), data) }.ToString();
            var page = sync ? client.GetUploadSftps("context") : await client.GetUploadSftpsAsync("context");
            Assert.That(page.Select(item => item.Id), Is.EqualTo(new[] { "second", "endpoint" })); Assert.That(page[0].Tags, Is.Null); Assert.That(((IList<UploadSftp>)page).IsReadOnly, Is.True);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString), Is.Empty);
        }

        [TestCase("id"), TestCase("hostname"), TestCase("username"), TestCase("password"), TestCase("server-public-key"), TestCase("port")]
        public void CreationRequiresCompleteConnectionDataWithOriginalCause(string field)
        {
            var data = Record(); data.Remove(field); var state = new State { Status = HttpStatusCode.Created, Json = data.ToString() };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateUploadSftpAsync("context", Options())));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(error.ToString(), Does.Not.Contain("isolated-import-secret")); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("0"), TestCase("65536"), TestCase("-1"), TestCase("1.5"), TestCase("\"22\""), TestCase("null"), TestCase("9223372036854775808")]
        public void PortShapeAndTcpRangeAreExplicit(string portJson)
        {
            var data = Record(); data["port"] = JToken.Parse(portJson);
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetUploadSftpAsync("context", "endpoint")));
        }

        [TestCase("identity"), TestCase("name"), TestCase("tags"), TestCase("null-tag"), TestCase("destinations")]
        public void InvalidMetadataDoesNotExposeCredentials(string problem)
        {
            var data = Record(); if (problem == "identity") data["id"] = "different"; if (problem == "name") data["name"] = null;
            if (problem == "tags") data["tags"] = "csv"; if (problem == "null-tag") data["tags"] = new JArray(JValue.CreateNull()); if (problem == "destinations") { data["collection"] = "one"; data["folder"] = "two"; }
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetUploadSftpAsync("context", "endpoint")));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(error.ToString(), Does.Not.Contain("isolated-import-secret"));
        }

        [TestCase("absent"), TestCase("shape"), TestCase("entry"), TestCase("duplicate-guid")]
        public void InvalidOwnListsAndRepeatedCanonicalIdentityFail(string problem)
        {
            var data = new JObject(); if (problem == "shape") data["upload-sftps"] = "invalid"; if (problem == "entry") data["upload-sftps"] = new JArray(1);
            if (problem == "duplicate-guid") data["upload-sftps"] = new JArray(Record("01234567-89ab-cdef-0123-456789abcdef"), Record("{01234567-89AB-CDEF-0123-456789ABCDEF}"));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetUploadSftpsAsync("context")));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void CreatorRestrictionsAndUncertainWritesRetainOriginalCause(int status)
        {
            var cause = new InvalidOperationException("isolated cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).UpdateUploadSftpAsync("context", "endpoint", new UploadSftpOptions { Tags = new string[0] })));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task RemovalIsSelectedBodylessAndPreservesImportedDocumentsContract(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (sync) client.RemoveUploadSftp("context", "endpoint"); else await client.RemoveUploadSftpAsync("context", "endpoint");
            Assert.That(state.Request.Method, Is.EqualTo(Method.Delete)); Assert.That(state.Request.Resource, Is.EqualTo("v2/upload-sftp/endpoint")); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase("create"), TestCase("get"), TestCase("list"), TestCase("update"), TestCase("remove")]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeIo(string operation)
        {
            Func<Client, CancellationToken, Task> invoke = (client, token) => operation == "create" ? (Task)client.CreateUploadSftpAsync("context", Options(), token) :
                operation == "get" ? client.GetUploadSftpAsync("context", "endpoint", token) : operation == "list" ? client.GetUploadSftpsAsync("context", token) :
                operation == "update" ? client.UpdateUploadSftpAsync("context", "endpoint", new UploadSftpOptions(), token) : client.RemoveUploadSftpAsync("context", "endpoint", token);
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = invoke(new Client(state), cancellation.Token); await state.Entered.Task; cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await invoke(new Client(state), cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }
        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":\"created\"}"), TestCase("{\"id\":\"created\"} {}")]
        public void MalformedCreationEnvelopeRetainsInnerCauseAndNoReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateUploadSftpAsync("context", Options())));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(1), TestCase(65535)]
        public async Task TcpPortBoundariesRemainExact(int port)
        {
            var data = Record(); data["port"] = port;
            Assert.That((await new Client(new State { Json = data.ToString() }).GetUploadSftpAsync("context", "endpoint")).Connection.Port, Is.EqualTo(port));
        }

        [Test]
        public async Task CanonicalSelectionRetainsOriginalIdentityAndEmptyOwnList()
        {
            var original = "{01234567-89AB-CDEF-0123-456789ABCDEF}"; var state = new State { Json = Record(original).ToString() }; var client = new Client(state);
            Assert.That((await client.GetUploadSftpAsync("context", "01234567-89ab-cdef-0123-456789abcdef")).Id, Is.EqualTo(original));
            state.Json = "{\"upload-sftps\":[]}"; Assert.That(await client.GetUploadSftpsAsync("context"), Is.Empty);
        }

        [TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectedIdsFailBeforeAuthorization(string id)
        {
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).RemoveUploadSftpAsync("context", id)));
            Assert.That(state.Authorizations, Is.Zero);
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
                        var response = status == 503 ? "{}" : operation == "create" ? Record("created").ToString() : operation == "get" ? Record().ToString() : operation == "list" ? new JObject { ["upload-sftps"] = new JArray(Record()) }.ToString() : "";
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
                    var client = factory.UploadSftps; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "create") Assert.That(await client.CreateUploadSftpAsync("context", Options(), cancellation.Token), Is.Not.Null);
                        else if (operation == "get") Assert.That((await client.GetUploadSftpAsync("context", "endpoint", cancellation.Token)).Id, Is.EqualTo("endpoint"));
                        else if (operation == "list") Assert.That((await client.GetUploadSftpsAsync("context", cancellation.Token)).Count, Is.EqualTo(1));
                        else if (operation == "update") await client.UpdateUploadSftpAsync("context", "endpoint", new UploadSftpOptions { Tags = new string[0], Password = "" }, cancellation.Token);
                        else await client.RemoveUploadSftpAsync("context", "endpoint", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "create" ? "POST /bridge/v2/upload-sftps" : operation == "list" ? "GET /bridge/v2/upload-sftps" :
                        (operation == "get" ? "GET" : operation == "update" ? "PUT" : "DELETE") + " /bridge/v2/upload-sftp/endpoint";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    if (operation == "create") Assert.That(JObject.Parse(sent).Value<string>("name"), Is.EqualTo("ä importer"));
                    else if (operation == "update") Assert.That(JObject.Parse(sent).Properties().Select(property => property.Name), Is.EqualTo(new[] { "password", "tags" }));
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
        private sealed class Client : UploadSftpsRestClient
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
            public string UserAgent => "isolated-upload-sftp-tests";
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
