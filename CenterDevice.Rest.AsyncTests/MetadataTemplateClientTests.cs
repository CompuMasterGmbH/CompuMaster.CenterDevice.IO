using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.MetadataTemplates;
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
    public class MetadataTemplateClientTests
    {
        private static JObject Record(string id = "template") => new JObject { ["id"] = id, ["name"] = "ä template", ["type"] = "user", ["owner"] = "original-owner", ["sharing-right"] = "viewer",
            ["creation-date"] = "2026-10-01T10:00:00Z", ["last-modified-date"] = "2026-10-12T12:30:00+02:00", ["fields"] = new JArray(new JObject { ["key"] = "user.strings.reference" }) };
        private static MetadataTemplateCreateRequest Options() => new MetadataTemplateCreateRequest { Name = "ä template", Fields = new[] { new MetadataTemplateField("user.numbers.amount", "1.2300"), new MetadataTemplateField("user.dates.date", "2026-10-01T10:00:00+02:00"), new MetadataTemplateField("user.strings.optional") },
            Sharings = new[] { new MetadataTemplateSharing("sharee", "viewer", "user") } };

        [TestCase(false), TestCase(true)]
        public async Task CreationPreservesCompleteNestedFieldsAndSharingsWithoutCoercingDefaults(bool sync)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" }; var client = new Client(state);
            var result = sync ? client.CreateMetadataTemplate("context", Options()) : await client.CreateMetadataTemplateAsync("context", Options());
            Assert.That(result, Is.EqualTo("created")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/metadata-templates"));
            var body = Body(state); Assert.That(body.Value<string>("type"), Is.EqualTo("user")); Assert.That(body.Value<string>("name"), Is.EqualTo("ä template"));
            Assert.That(body["fields"][0].Value<string>("default-value"), Is.EqualTo("1.2300")); Assert.That(body["fields"][0]["default-value"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(body["fields"][1].Value<string>("default-value"), Is.EqualTo("2026-10-01T10:00:00+02:00")); Assert.That(body["fields"][2]["default-value"], Is.Null);
            Assert.That(body["sharings"][0].Value<string>("id"), Is.EqualTo("sharee")); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [Test]
        public async Task CompleteSequencesSnapshotOnceBeforeAuthorizationAndKeepEmptyDefaultDistinct()
        {
            var fieldCount = 0; var sharingCount = 0;
            IEnumerable<MetadataTemplateField> Fields() { fieldCount++; yield return new MetadataTemplateField("user.strings.empty", ""); }
            IEnumerable<MetadataTemplateSharing> Sharings() { sharingCount++; yield return new MetadataTemplateSharing("group", "coowner", "group"); }
            var options = new MetadataTemplateCreateRequest { Name = "original", Fields = Fields(), Sharings = Sharings() };
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}", BeforeAuthorize = () => { options.Name = "changed"; options.Fields = null; options.Sharings = null; } };
            await new Client(state).CreateMetadataTemplateAsync("context", options);
            Assert.That(fieldCount, Is.EqualTo(1)); Assert.That(sharingCount, Is.EqualTo(1)); Assert.That(Body(state).Value<string>("name"), Is.EqualTo("original"));
            Assert.That(Body(state)["fields"][0].Value<string>("default-value"), Is.Empty); Assert.That(Body(state)["sharings"][0].Value<string>("type"), Is.EqualTo("group"));
        }

        [TestCase(false), TestCase(true)]
        public async Task UpdateReplacesCompleteArraysAndNeverSubstitutesHiddenSharings(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            var options = new MetadataTemplateUpdateRequest { Fields = new[] { new MetadataTemplateField("user.strings.only", "ä value") } };
            if (sync) client.UpdateMetadataTemplate("context", "id with/slash", options); else await client.UpdateMetadataTemplateAsync("context", "id with/slash", options);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/metadata-template/id%20with%2Fslash")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(Body(state).Properties().Select(item => item.Name), Is.EqualTo(new[] { "fields" })); Assert.That(Body(state)["fields"].Count(), Is.EqualTo(1));
            await client.UpdateMetadataTemplateAsync("context", "template", new MetadataTemplateUpdateRequest { Name = "", Type = "user", Fields = new MetadataTemplateField[0], Sharings = new MetadataTemplateSharing[0] });
            Assert.That(Body(state).Value<string>("name"), Is.Empty); Assert.That(Body(state)["fields"], Is.Empty); Assert.That(Body(state)["sharings"], Is.Empty);
        }

        [TestCase(null), TestCase(""), TestCase("user.strings."), TestCase("common.strings.reference"), TestCase("user.other.field"), TestCase("user.dates. ")]
        public void UnsupportedInputNamespaceKeysFail(string key)
        { Assert.Catch<ArgumentException>((TestDelegate)(() => new MetadataTemplateField(key))); }

        [TestCase("", "viewer", "user"), TestCase("id", "owner", "user"), TestCase("id", "viewer", "future")]
        public void UnsupportedSharingSelectionsFail(string id, string right, string type)
        { Assert.Catch<ArgumentException>((TestDelegate)(() => new MetadataTemplateSharing(id, right, type))); }

        [TestCase("name"), TestCase("fields"), TestCase("sharings"), TestCase("field-entry"), TestCase("sharing-entry"), TestCase("options")]
        public void InvalidCreationFailsBeforeAuthorization(string problem)
        {
            var options = Options(); if (problem == "name") options.Name = null; if (problem == "fields") options.Fields = null; if (problem == "sharings") options.Sharings = null;
            if (problem == "field-entry") options.Fields = new MetadataTemplateField[] { null }; if (problem == "sharing-entry") options.Sharings = new MetadataTemplateSharing[] { null };
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).CreateMetadataTemplateAsync("context", problem == "options" ? null : options)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [Test]
        public async Task ExplicitEmptyCreationArraysAndBlankNameStayExplicit()
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" };
            await new Client(state).CreateMetadataTemplateAsync("context", new MetadataTemplateCreateRequest { Name = "", Fields = new MetadataTemplateField[0], Sharings = new MetadataTemplateSharing[0] });
            Assert.That(Body(state)["fields"], Is.Empty); Assert.That(Body(state)["sharings"], Is.Empty); Assert.That(Body(state).Value<string>("name"), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task VisibleSelectedAndListMetadataRetainOwnerRightsDatesAndHiddenSharings(bool sync)
        {
            var state = new State { Json = Record().ToString() }; var client = new Client(state);
            var result = sync ? client.GetMetadataTemplate("context", "template") : await client.GetMetadataTemplateAsync("context", "template");
            Assert.That(result.OwnerId, Is.EqualTo("original-owner")); Assert.That(result.SharingRight, Is.EqualTo("viewer")); Assert.That(result.Sharings, Is.Null);
            Assert.That(result.CreationDate.Value.Offset, Is.EqualTo(TimeSpan.Zero)); Assert.That(result.LastModifiedDate.Value.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(((IList<MetadataTemplateField>)result.Fields).IsReadOnly, Is.True); Assert.That(result.Fields[0].DefaultValue, Is.Null);
            state.Json = new JObject { ["metadata-templates"] = new JArray(Record("second"), Record()) }.ToString();
            var page = sync ? client.GetMetadataTemplates("context") : await client.GetMetadataTemplatesAsync("context");
            Assert.That(page.Select(item => item.Id), Is.EqualTo(new[] { "second", "template" })); Assert.That(page[0].OwnerId, Is.EqualTo("original-owner")); Assert.That(((IList<MetadataTemplate>)page).IsReadOnly, Is.True);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task FutureNamesOffsetFreeDatesAndHiddenVersusEmptySharingsRemainOriginal(bool hidden)
        {
            var data = Record(); data["type"] = "future-type"; data["sharing-right"] = "future-right"; data["creation-date"] = "2026-10-01T10:00:00"; data["last-modified-date"] = "2026-10-12T12:30:00";
            data["fields"] = new JArray(new JObject { ["key"] = "future.section.key", ["default-value"] = "" }); data["sharings"] = hidden ? (JToken)JValue.CreateNull() : new JArray();
            var result = await new Client(new State { Json = data.ToString() }).GetMetadataTemplateAsync("context", "template");
            Assert.That(result.Type, Is.EqualTo("future-type")); Assert.That(result.SharingRight, Is.EqualTo("future-right")); Assert.That(result.CreationDate, Is.Null); Assert.That(result.LastModifiedDate, Is.Null);
            Assert.That(result.Fields[0].Key, Is.EqualTo("future.section.key")); Assert.That(result.Fields[0].DefaultValue, Is.Empty);
            if (hidden) Assert.That(result.Sharings, Is.Null); else Assert.That(result.Sharings, Is.Empty);
        }

        [Test]
        public async Task ExposedFutureSharingNamesAreRetainedWithImmutableRecords()
        {
            var data = Record(); data["sharings"] = new JArray(new JObject { ["id"] = "sharee", ["right"] = "future-right", ["type"] = "future-type" });
            var result = await new Client(new State { Json = data.ToString() }).GetMetadataTemplateAsync("context", "template");
            Assert.That(result.Sharings[0].Right, Is.EqualTo("future-right")); Assert.That(result.Sharings[0].Type, Is.EqualTo("future-type")); Assert.That(((IList<MetadataTemplateSharing>)result.Sharings).IsReadOnly, Is.True);
        }

        [TestCase("id"), TestCase("owner"), TestCase("sharing-right"), TestCase("fields"), TestCase("field-default"), TestCase("creation-date"), TestCase("last-modified-date"), TestCase("sharings")]
        public void MalformedMetadataRetainsInnerCause(string problem)
        {
            var data = Record(); if (problem == "id") data["id"] = "different"; else if (problem == "field-default") data["fields"][0]["default-value"] = 1;
            else if (problem == "creation-date" || problem == "last-modified-date") data[problem] = "not-a-date"; else data[problem] = 1;
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetMetadataTemplateAsync("context", "template")));
            Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"id\":null}"), TestCase("{\"id\":1}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":\"created\"} {}")]
        public void MalformedCreationDoesNotReplay(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).CreateMetadataTemplateAsync("context", Options()))); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("absent"), TestCase("shape"), TestCase("entry"), TestCase("duplicate-guid")]
        public void InvalidListsAndRepeatedCanonicalIdsFail(string problem)
        {
            var data = new JObject(); if (problem == "shape") data["metadata-templates"] = "invalid"; if (problem == "entry") data["metadata-templates"] = new JArray(1);
            if (problem == "duplicate-guid") data["metadata-templates"] = new JArray(Record("01234567-89ab-cdef-0123-456789abcdef"), Record("{01234567-89AB-CDEF-0123-456789ABCDEF}"));
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetMetadataTemplatesAsync("context")));
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void SharingRightsAndFeatureFailuresPreserveOriginalCause(int status)
        {
            var cause = new InvalidOperationException("original server cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).UpdateMetadataTemplateAsync("context", "template", new MetadataTemplateUpdateRequest { Fields = new MetadataTemplateField[0] })));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task RemovalIsSelectedBodyless(bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var client = new Client(state);
            if (sync) client.RemoveMetadataTemplate("context", "template"); else await client.RemoveMetadataTemplateAsync("context", "template");
            Assert.That(state.Request.Method, Is.EqualTo(Method.Delete)); Assert.That(state.Request.Resource, Is.EqualTo("v2/metadata-template/template")); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase("create"), TestCase("get"), TestCase("list"), TestCase("update"), TestCase("remove")]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeIo(string operation)
        {
            Func<Client, CancellationToken, Task> invoke = (client, token) => operation == "create" ? (Task)client.CreateMetadataTemplateAsync("context", Options(), token) :
                operation == "get" ? client.GetMetadataTemplateAsync("context", "template", token) : operation == "list" ? client.GetMetadataTemplatesAsync("context", token) :
                operation == "update" ? client.UpdateMetadataTemplateAsync("context", "template", new MetadataTemplateUpdateRequest(), token) : client.RemoveMetadataTemplateAsync("context", "template", token);
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = invoke(new Client(state), cancellation.Token); await state.Entered.Task; cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await invoke(new Client(state), cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }
        [TestCase("options"), TestCase("type"), TestCase("fields"), TestCase("sharings")]
        public void InvalidUpdateSelectionsFailBeforeAuthorization(string problem)
        {
            var selection = new MetadataTemplateUpdateRequest(); if (problem == "type") selection.Type = "future-type";
            if (problem == "fields") selection.Fields = new MetadataTemplateField[] { null }; if (problem == "sharings") selection.Sharings = new MetadataTemplateSharing[] { null };
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateMetadataTemplateAsync("context", "template", problem == "options" ? null : selection)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [Test]
        public async Task CanonicalSelectionPreservesOriginalIdentityAndEmptyListAndEmptyUpdate()
        {
            var original = "{01234567-89AB-CDEF-0123-456789ABCDEF}"; var state = new State { Json = Record(original).ToString() }; var client = new Client(state);
            Assert.That((await client.GetMetadataTemplateAsync("context", "01234567-89ab-cdef-0123-456789abcdef")).Id, Is.EqualTo(original));
            state.Json = "{\"metadata-templates\":[]}"; Assert.That(await client.GetMetadataTemplatesAsync("context"), Is.Empty);
            state.Status = HttpStatusCode.NoContent; await client.UpdateMetadataTemplateAsync("context", "template", new MetadataTemplateUpdateRequest()); Assert.That(Body(state).Count, Is.Zero);
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
                        var response = status == 503 ? "{}" : operation == "create" ? "{\"id\":\"created\"}" : operation == "get" ? Record().ToString() : operation == "list" ? new JObject { ["metadata-templates"] = new JArray(Record()) }.ToString() : "";
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
                    var client = factory.MetadataTemplates; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "create") Assert.That(await client.CreateMetadataTemplateAsync("context", Options(), cancellation.Token), Is.EqualTo("created"));
                        else if (operation == "get") Assert.That((await client.GetMetadataTemplateAsync("context", "template", cancellation.Token)).Id, Is.EqualTo("template"));
                        else if (operation == "list") Assert.That((await client.GetMetadataTemplatesAsync("context", cancellation.Token)).Count, Is.EqualTo(1));
                        else if (operation == "update") await client.UpdateMetadataTemplateAsync("context", "template", new MetadataTemplateUpdateRequest { Fields = new MetadataTemplateField[0], Sharings = new MetadataTemplateSharing[0] }, cancellation.Token);
                        else await client.RemoveMetadataTemplateAsync("context", "template", cancellation.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "create" ? "POST /bridge/v2/metadata-templates" : operation == "list" ? "GET /bridge/v2/metadata-templates" :
                        (operation == "get" ? "GET" : operation == "update" ? "PUT" : "DELETE") + " /bridge/v2/metadata-template/template";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    if (operation == "create") Assert.That(JObject.Parse(sent).Value<string>("name"), Is.EqualTo("ä template"));
                    else if (operation == "update") Assert.That(JObject.Parse(sent).Properties().Select(property => property.Name), Is.EqualTo(new[] { "fields", "sharings" }));
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
        private sealed class Client : MetadataTemplatesRestClient
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
            public string UserAgent => "isolated-metadata-template-tests";
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
