using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Tenant;
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
    public class TenantSettingsContractsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OriginalReadValuesAndEditabilityDoNotBecomeInferredActionPermissions(bool sync)
        {
            var state = new State { Json = "{\"unarchiving\":{\"value\":{\"roles\":[\"admin\",\"future\"],\"users\":[]},\"enabled\":false},\"collection-sharing\":{\"value\":{\"roles\":[\"internal\"]}},\"future-setting\":{\"enabled\":null,\"future\":\"2026-10-13\"}}" };
            var client = new Client(state); var result = sync ? client.GetTenantSettingsDetails("context", "current") : await client.GetTenantSettingsDetailsAsync("context", "current");
            Assert.That(result.Settings["unarchiving"].Name, Is.EqualTo("unarchiving")); Assert.That(result.Settings["unarchiving"].Enabled, Is.False);
            Assert.That(result.Settings["unarchiving"].Value.Roles, Is.EqualTo(new[] { "admin", "future" })); Assert.That(result.Settings["unarchiving"].Value.Users, Is.Empty);
            Assert.That(result.Settings["collection-sharing"].Enabled, Is.Null); Assert.That(result.Settings["collection-sharing"].Value.Users, Is.Null); Assert.That(result.Settings["future-setting"].Value, Is.Null);
            Assert.That(((IDictionary<string, TenantSettingDetails>)result.Settings).IsReadOnly, Is.True); var raw = result.GetRawSettings(); raw["unarchiving"]["enabled"] = true;
            Assert.That(result.Settings["unarchiving"].Enabled, Is.False); Assert.That(result.Settings["future-setting"].GetRawDetails()["future"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/tenant/current/settings")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase(false), TestCase(true)]
        public async Task AllTypedSettingSelectionsSendOriginalCompleteWireFields(bool sync)
        {
            var selection = new TenantPermissionSelection { Users = new[] { "selected-user" }, Roles = new[] { "admin", "future-role" } };
            var settings = new TenantSettingsUpdateRequest { Auditing = selection, Archiving = selection, Unarchiving = selection, DocumentDeletion = selection, TransferOwnership = selection, CollectionSharing = selection, EnforceMfa = selection };
            var expected = JObject.Parse("{\"auditing\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]},\"archiving\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]},\"unarchiving\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]},\"document-deletion\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]},\"transfer-ownership\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]},\"collection-sharing\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]},\"enforce-mfa\":{\"users\":[\"selected-user\"],\"roles\":[\"admin\",\"future-role\"]}}");
            var state = new State { Status = HttpStatusCode.NoContent, BeforeAuthorize = () => settings["auditing"]["roles"][0] = "mutated" }; var client = new Client(state);
            if (sync) client.UpdateTenantSettings("context", "part/with?query", settings); else await client.UpdateTenantSettingsAsync("context", "part/with?query", settings);
            Assert.That(JToken.DeepEquals(Body(state), expected), Is.True); Assert.That(state.Request.Resource, Is.EqualTo("v2/tenant/part%2Fwith%3Fquery/settings")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put)); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase("auditing"), TestCase("archiving"), TestCase("unarchiving"), TestCase("document-deletion"), TestCase("transfer-ownership"), TestCase("collection-sharing"), TestCase("enforce-mfa")]
        public async Task ExplicitEmptySelectionsDoNotInsertOtherSettings(string setting)
        {
            var state = new State { Status = HttpStatusCode.NoContent }; var request = new TenantSettingsUpdateRequest { [setting] = new JObject { ["users"] = new JArray(), ["roles"] = new JArray() } };
            await new Client(state).UpdateTenantSettingsAsync("context", "current", request);
            Assert.That(Body(state).Properties().Select(p => p.Name), Is.EqualTo(new[] { setting })); Assert.That(Body(state)[setting]["users"], Is.Empty); Assert.That(Body(state)[setting]["roles"], Is.Empty);
        }

        [Test]
        public async Task TypedOmissionCopiesAndFutureRawSelectionsRemainOriginal()
        {
            var users = new[] { "original" }; var selection = new TenantPermissionSelection { Users = users, Roles = Array.Empty<string>() };
            var request = new TenantSettingsUpdateRequest { Archiving = selection, Auditing = selection }; users[0] = "mutated"; selection.Users = new[] { "changed" }; request.Auditing = null;
            request["future-setting"] = new JObject { ["future"] = JValue.CreateNull() }; var state = new State { Status = HttpStatusCode.NoContent };
            await new Client(state).UpdateTenantSettingsAsync("context", "current", request);
            Assert.That(Body(state).Property("auditing"), Is.Null); Assert.That(Body(state)["archiving"]["users"].Values<string>(), Is.EqualTo(new[] { "original" })); Assert.That(Body(state)["future-setting"]["future"].Type, Is.EqualTo(JTokenType.Null));
        }

        [TestCase("{}"), TestCase("{\"future\":{\"value\":null,\"enabled\":null}}")]
        public async Task UnknownReadMetadataIsNotReplacedWithEmptyPermissions(string json)
        { var result = await new Client(new State { Json = json }).GetTenantSettingsDetailsAsync("context", "current"); if (result.Settings.Count > 0) { Assert.That(result.Settings["future"].Value, Is.Null); Assert.That(result.Settings["future"].Enabled, Is.Null); } }

        [TestCase("{\"auditing\":{}}"), TestCase("{\"auditing\":null}"), TestCase("{\"auditing\":{\"users\":[],\"roles\":null}}"), TestCase("{\"auditing\":{\"roles\":[]}}")]
        [TestCase("{\"auditing\":{\"users\":[null],\"roles\":[]}}"), TestCase("{\"auditing\":{\"users\":[],\"roles\":[1]}}"), TestCase("{\"auditing\":{\"users\":[],\"roles\":[],\"enabled\":true}}")]
        [TestCase("{\"auditing\":{\"users\":[],\"roles\":[],\"value\":{}}}")]
        public void IncompleteOrReadOnlyKnownUpdatesFailBeforeAuthorization(string json)
        {
            var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateTenantSettingsAsync("context", "current", new TenantSettingsUpdateRequest(JObject.Parse(json)))));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("[]"), TestCase("{} {}"), TestCase("{\"x\":{},\"x\":{}}"), TestCase("{\"x\":null}"), TestCase("{\"x\":{\"enabled\":1}}"), TestCase("{\"x\":{\"value\":[]}}"), TestCase("{\"x\":{\"value\":{\"users\":[null]}}}")]
        public void MalformedSuccessfulSettingsPreserveAnInnerDiagnostic(string json)
        { Assert.That(Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = json }).GetTenantSettingsDetailsAsync("context", "current"))).InnerException, Is.Not.Null); }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectedTenantFailsBeforeReadingOrUpdating(string id)
        { var state = new State(); var client = new Client(state); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await client.GetTenantSettingsDetailsAsync("context", id))); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await client.UpdateTenantSettingsAsync("context", id, new TenantSettingsUpdateRequest()))); Assert.That(state.Authorizations, Is.Zero); }

        [Test]
        public void NullUpdateDoesNotBecomeAnImplicitReset()
        { var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => await new Client(state).UpdateTenantSettingsAsync("context", "current", (TenantSettingsUpdateRequest)null))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase(false, 204), TestCase(false, 403), TestCase(true, 200), TestCase(true, 403), TestCase(true, 500)]
        public void UnexpectedStatusesDoNotBecomeSuccessfulPermissionOperations(bool write, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, write))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase(false), TestCase(true)]
        public void PreCancellationDoesNotAuthorizeOrSend(bool write)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, write, source.Token))); } Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase(false), TestCase(true)]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp(bool write)
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var pending = Invoke(state, write, source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); } }

        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task FactoryUsesBodylessGetOrCompletePutOverPrivateTcpLoopback(bool write, bool sync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null, sent = null; var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768) { if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        headers = Encoding.ASCII.GetString(bytes.ToArray());
                        if (write)
                        {
                            var size = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1]); var body = new byte[size]; var count = 0;
                            while (count < size) { var read = await stream.ReadAsync(body, count, size - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; } sent = Encoding.UTF8.GetString(body);
                        }
                        var payload = write ? Array.Empty<byte>() : Encoding.UTF8.GetBytes("{\"collection-sharing\":{\"value\":{\"roles\":[\"external\"]},\"enabled\":false}}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + (write ? "204 No Content" : "200 OK") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n"); await stream.WriteAsync(reply, 0, reply.Length, source.Token); if (payload.Length > 0) await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var client = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).TenantSettings; client.DisableOfflineModeSimulation();
                    if (write) { var settings = new TenantSettingsUpdateRequest { Unarchiving = new TenantPermissionSelection { Users = Array.Empty<string>(), Roles = Array.Empty<string>() } }; if (sync) client.UpdateTenantSettings("context", "current", settings, source.Token); else await client.UpdateTenantSettingsAsync("context", "current", settings, source.Token); }
                    else { var result = sync ? client.GetTenantSettingsDetails("context", "current", source.Token) : await client.GetTenantSettingsDetailsAsync("context", "current", source.Token); Assert.That(result.Settings["collection-sharing"].Enabled, Is.False); Assert.That(result.Settings["collection-sharing"].Value.Users, Is.Null); }
                    await server; var line = headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0]; Assert.That(line, Is.EqualTo((write ? "PUT" : "GET") + " /bridge/v2/tenant/current/settings HTTP/1.1"));
                    if (write) Assert.That(JToken.DeepEquals(JObject.Parse(sent), JObject.Parse("{\"unarchiving\":{\"users\":[],\"roles\":[]}}")), Is.True);
                }
                finally { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private static async Task Invoke(State state, bool write, CancellationToken token = default(CancellationToken))
        { if (write) await new Client(state).UpdateTenantSettingsAsync("context", "current", new TenantSettingsUpdateRequest(), token); else await new Client(state).GetTenantSettingsDetailsAsync("context", "current", token); }
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-tenant-settings-tests"; }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class Client : TenantSettingsRestClient
        { private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Context, Json = "{}"; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
