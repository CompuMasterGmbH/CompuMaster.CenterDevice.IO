using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
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
    public class CollectionMetadataAuditingTests
    {
        [TestCase("metadata", false), TestCase("metadata", true), TestCase("enable", false), TestCase("enable", true)]
        [TestCase("in", false), TestCase("in", true), TestCase("out", false), TestCase("out", true)]
        [TestCase("reset", false), TestCase("reset", true), TestCase("resend", false), TestCase("resend", true), TestCase("settings", false), TestCase("settings", true)]
        public async Task OriginalActionsAndParameterShapesPreserveExplicitChoices(string operation, bool sync)
        {
            var state = new State(); await Invoke(state, operation, sync, id: "part/with?query"); var body = Body(state);
            Assert.That(body.Value<string>("action"), Is.EqualTo(Action(operation))); Assert.That(state.Request.Resource, Is.EqualTo("v2/collection/part%2Fwith%3Fquery")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
            if (operation == "enable" || operation == "in" || operation == "out") Assert.That(body.Property("params"), Is.Null);
            else if (operation == "reset" || operation == "resend") Assert.That(body["params"]["users"].Values<string>(), Is.EqualTo(new[] { "first", "second", "first" }));
            else if (operation == "metadata") Assert.That(JToken.DeepEquals(body["params"], JObject.Parse("{\"set\":{\"scalar\":\" original \",\"array\":[\"one\",\"two\"],\"empty\":[]},\"unset\":[\"missing\",\"missing\"]}")), Is.True);
            else Assert.That(JToken.DeepEquals(body["params"], JObject.Parse("{\"user-opted-in\":false,\"user-opted-out\":true,\"pending-responses\":\"future-period\"}")), Is.True);
        }

        [Test]
        public async Task MetadataObjectsAndUnsetNamesAreCopiedBeforeAuthorization()
        {
            var fields = new JObject { ["key"] = "original" }; var names = new[] { "missing" }; var update = new CollectionMetadataUpdateRequest { Set = fields, Unset = names };
            fields["key"] = "outside-mutation"; names[0] = "outside-mutation"; var state = new State { BeforeAuthorize = () => { update["set"]["key"] = "auth-mutation"; update["unset"][0] = "auth-mutation"; } };
            await new Client(state).UpdateExtendedMetadataAsync("context", "selected", update); Assert.That(Body(state)["params"]["set"].Value<string>("key"), Is.EqualTo("original")); Assert.That(Body(state)["params"]["unset"].Values<string>(), Is.EqualTo(new[] { "missing" }));
        }

        [TestCase("reset"), TestCase("resend")]
        public async Task AuditUserSelectionsAreSnapshottedBeforeAuthorization(string operation)
        {
            var users = new[] { "original" }; var state = new State { BeforeAuthorize = () => users[0] = "mutated" }; var client = new Client(state);
            if (operation == "reset") await client.ResetAuditingAsync("context", "selected", users); else await client.ResendAuditingEmailAsync("context", "selected", users);
            Assert.That(Body(state)["params"]["users"].Values<string>(), Is.EqualTo(new[] { "original" }));
        }

        [Test]
        public async Task TypedNotificationNullOmissionAndFalseAreNotDefaultsOrClearing()
        {
            var settings = new CollectionAuditingEmailSettings { UserOptedIn = true, UserOptedOut = false, PendingResponses = "first" };
            settings.UserOptedIn = null; settings.PendingResponses = null; var state = new State { BeforeAuthorize = () => settings.UserOptedOut = true };
            await new Client(state).UpdateAuditingEmailSettingsAsync("context", "selected", settings);
            Assert.That(((JObject)Body(state)["params"]).Properties().Select(p => p.Name), Is.EqualTo(new[] { "user-opted-out" })); Assert.That(Body(state)["params"].Value<bool>("user-opted-out"), Is.False);
        }

        [Test]
        public async Task EmptyExplicitMetadataAndUnknownParametersRemainUnchanged()
        {
            var update = new CollectionMetadataUpdateRequest { Set = new JObject(), Unset = Array.Empty<string>(), ["future"] = JValue.CreateNull() };
            var state = new State(); await new Client(state).UpdateExtendedMetadataAsync("context", "selected", update);
            Assert.That(((JObject)Body(state)["params"]["set"]).Properties(), Is.Empty); Assert.That(Body(state)["params"]["unset"], Is.Empty); Assert.That(Body(state)["params"]["future"].Type, Is.EqualTo(JTokenType.Null));
        }

        [TestCase("{\"set\":null}"), TestCase("{\"set\":[]}"), TestCase("{\"set\":{\"key\":1}}"), TestCase("{\"set\":{\"key\":null}}"), TestCase("{\"set\":{\"key\":[false]}}"), TestCase("{\"unset\":null}"), TestCase("{\"unset\":[null]}")]
        public void InvalidMetadataShapesFailBeforeAuthorization(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", "selected", new CollectionMetadataUpdateRequest(JObject.Parse(json))))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase("{\"user-opted-in\":0}"), TestCase("{\"user-opted-out\":null}"), TestCase("{\"pending-responses\":true}"), TestCase("{\"pending-responses\":null}")]
        public void InvalidKnownNotificationShapesFailBeforeAuthorization(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateAuditingEmailSettingsAsync("context", "selected", new CollectionAuditingEmailSettings(JObject.Parse(json))))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase("enable"), TestCase("in"), TestCase("out"), TestCase("reset"), TestCase("resend"), TestCase("settings"), TestCase("metadata")]
        public void InvalidSelectedCollectionFailsBeforeAuthorization(string operation)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await Invoke(state, operation, id: ".."))); Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase("empty"), TestCase("null"), TestCase("blank"), TestCase("null-entry")]
        public void UnusableAuditUserSelectionsFailBeforeAuthorization(string kind)
        {
            var state = new State(); var users = kind == "null" ? null : kind == "empty" ? Array.Empty<string>() : new[] { kind == "null-entry" ? null : " " }; Func<Task> reset = async () => await new Client(state).ResetAuditingAsync("context", "selected", users);
            if (kind == "null") Assert.ThrowsAsync<ArgumentNullException>(reset); else Assert.ThrowsAsync<ArgumentException>(reset); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("metadata"), TestCase("settings")]
        public void NullParameterObjectsAreNotImplicitResets(string operation)
        {
            var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => { var client = new Client(state); if (operation == "metadata") await client.UpdateExtendedMetadataAsync("context", "selected", null); else await client.UpdateAuditingEmailSettingsAsync("context", "selected", null); })); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("metadata", 200), TestCase("enable", 403), TestCase("in", 409), TestCase("out", 403), TestCase("reset", 403), TestCase("resend", 500), TestCase("settings", 200)]
        public void UnexpectedStatusesDoNotBecomeSuccessfulAuditStateOrReplay(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase("metadata"), TestCase("enable"), TestCase("in"), TestCase("out"), TestCase("reset"), TestCase("resend"), TestCase("settings")]
        public void PreCancellationDoesNotAuthorizeOrSend(string operation)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, token: source.Token))); } Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase("metadata"), TestCase("enable"), TestCase("in"), TestCase("out"), TestCase("reset"), TestCase("resend"), TestCase("settings")]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp(string operation)
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var pending = Invoke(state, operation, token: source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); } }

        [TestCase("metadata"), TestCase("enable"), TestCase("in"), TestCase("out"), TestCase("reset"), TestCase("resend"), TestCase("settings")]
        public async Task FactorySendsExplicitCollectionActionOverPrivateTcpLoopback(string operation)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null, sent = null; var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1]; while (bytes.Count < 32768) { if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        headers = Encoding.ASCII.GetString(bytes.ToArray()); var size = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]); var body = new byte[size]; var count = 0;
                        while (count < size) { var read = await stream.ReadAsync(body, count, size - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; } sent = Encoding.UTF8.GetString(body);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 204 No Content\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"); await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                    }
                });
                try
                {
                    var client = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Collection; client.DisableOfflineModeSimulation(); await InvokeClient(client, operation, false, "selected", source.Token);
                    await server; Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/collection/selected HTTP/1.1")); Assert.That(JObject.Parse(sent).Value<string>("action"), Is.EqualTo(Action(operation))); Assert.That(listener.Pending(), Is.False);
                }
                finally { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private static string Action(string operation) { switch (operation) { case "metadata": return "update-extended-metadata"; case "enable": return "enable-auditing"; case "in": return "opt-in"; case "out": return "opt-out"; case "reset": return "reset-auditing"; case "resend": return "resend-auditing-email"; default: return "update-auditing-email-settings"; } }
        private static Task Invoke(State state, string operation, bool sync = false, string id = "selected", CancellationToken token = default(CancellationToken)) => InvokeClient(new Client(state), operation, sync, id, token);
        private static async Task InvokeClient(CollectionRestClient client, string operation, bool sync, string id, CancellationToken token)
        {
            var metadata = new CollectionMetadataUpdateRequest { Set = JObject.Parse("{\"scalar\":\" original \",\"array\":[\"one\",\"two\"],\"empty\":[]}"), Unset = new[] { "missing", "missing" } };
            var settings = new CollectionAuditingEmailSettings { UserOptedIn = false, UserOptedOut = true, PendingResponses = "future-period" }; var users = new[] { "first", "second", "first" };
            switch (operation)
            {
                case "metadata": if (sync) client.UpdateExtendedMetadata("context", id, metadata, token); else await client.UpdateExtendedMetadataAsync("context", id, metadata, token); break;
                case "enable": if (sync) client.EnableAuditing("context", id, token); else await client.EnableAuditingAsync("context", id, token); break;
                case "in": if (sync) client.OptIn("context", id, token); else await client.OptInAsync("context", id, token); break;
                case "out": if (sync) client.OptOut("context", id, token); else await client.OptOutAsync("context", id, token); break;
                case "reset": if (sync) client.ResetAuditing("context", id, users, token); else await client.ResetAuditingAsync("context", id, users, token); break;
                case "resend": if (sync) client.ResendAuditingEmail("context", id, users, token); else await client.ResendAuditingEmailAsync("context", id, users, token); break;
                default: if (sync) client.UpdateAuditingEmailSettings("context", id, settings, token); else await client.UpdateAuditingEmailSettingsAsync("context", id, settings, token); break;
            }
        }
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-collection-audit-tests"; }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class Client : CollectionRestClient
        { private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.NoContent; internal string Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = "{}" }; }
        }
    }
}
