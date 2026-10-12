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
    public class TenantDetailsContractsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task SelectedMetadataRetainsNullableStateAndOriginalLogoEncoding(bool sync)
        {
            var state = new State { Json = "{\"id\":\"selected\",\"name\":\" Tenant \",\"status\":\"future\",\"cancelled\":false,\"logo\":{\"mimetype\":\"image/png\",\"encoding\":\"future\",\"data\":\"original\"},\"future\":\"2026-10-13\"}" };
            var client = new TenantClient(state); var result = sync ? client.GetTenantDetails("context", "selected") : await client.GetTenantDetailsAsync("context", "selected");
            Assert.That(result.Name, Is.EqualTo(" Tenant ")); Assert.That(result.Status, Is.EqualTo("future")); Assert.That(result.Cancelled, Is.False);
            Assert.That(result.Logo.MimeType, Is.EqualTo("image/png")); Assert.That(result.Logo.Encoding, Is.EqualTo("future")); Assert.That(result.Logo.Data, Is.EqualTo("original"));
            var raw = result.GetRawDetails(); Assert.That(raw["future"].Type, Is.EqualTo(JTokenType.String)); raw["name"] = "changed"; Assert.That(result.GetRawDetails().Value<string>("name"), Is.EqualTo(" Tenant "));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/tenant/selected")); Assert.That(state.Request.Parameters.Where(p => p.Type == ParameterType.QueryString), Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase("{}"), TestCase("{\"cancelled\":null,\"logo\":null}")]
        public async Task MissingSelectedMetadataStaysUnknown(string json)
        {
            var result = await new TenantClient(new State { Json = json }).GetTenantDetailsAsync("context", "selected", new[] { "name" });
            Assert.That(result.Id, Is.Null); Assert.That(result.Status, Is.Null); Assert.That(result.Cancelled, Is.Null); Assert.That(result.Logo, Is.Null);
        }

        [TestCase("current", "resolved"), TestCase("550e8400-e29b-41d4-a716-446655440000", "550E8400-E29B-41D4-A716-446655440000")]
        public async Task CurrentAliasAndCanonicalGuidEquivalenceRetainTheServerIdentity(string selected, string returned)
        { Assert.That((await new TenantClient(new State { Json = "{\"id\":\"" + returned + "\"}" }).GetTenantDetailsAsync("context", selected)).Id, Is.EqualTo(returned)); }

        [TestCase(false), TestCase(true)]
        public async Task LoginListingPreservesTenantAndUserIdentityWithoutSwitchingContext(bool sync)
        {
            var state = new State { Json = "{\"default-tenant-id\":\"other\",\"tenants\":[{\"id\":\"tenant\",\"name\":\"Tenant\",\"status\":\"future\",\"expired\":false,\"cancelled\":true,\"user\":{\"id\":\"tenant-user\",\"email\":\"fixture@example.test\",\"first-name\":\" First \",\"last-name\":\"\",\"role\":\"future-role\",\"status\":\"invited\"}}]}" };
            var client = new TenantsClient(state); var result = sync ? client.GetLoginTenantsDetails("context") : await client.GetLoginTenantsDetailsAsync("context");
            Assert.That(result.DefaultTenantId, Is.EqualTo("other")); var entry = result.Tenants.Single(); Assert.That(entry.Id, Is.EqualTo("tenant")); Assert.That(entry.Name, Is.EqualTo("Tenant")); Assert.That(entry.Status, Is.EqualTo("future")); Assert.That(entry.Expired, Is.False); Assert.That(entry.Cancelled, Is.True);
            Assert.That(entry.User.Id, Is.EqualTo("tenant-user")); Assert.That(entry.User.Email, Is.EqualTo("fixture@example.test")); Assert.That(entry.User.FirstName, Is.EqualTo(" First ")); Assert.That(entry.User.LastName, Is.Empty); Assert.That(entry.User.Role, Is.EqualTo("future-role")); Assert.That(entry.User.Status, Is.EqualTo("invited"));
            Assert.That(((ICollection<TenantLoginEntryDetails>)result.Tenants).IsReadOnly, Is.True); entry.GetRawDetails()["expired"] = true; Assert.That(entry.Expired, Is.False);
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1)); Assert.That(state.Request.Resource, Is.EqualTo("v2/tenants"));
        }

        [TestCase("{}", null), TestCase("{\"tenants\":null}", null), TestCase("{\"tenants\":[]}", 0)]
        public async Task LoginProjectionsDistinguishUnknownAndEmptyTenantLists(string json, int? count)
        { var result = await new TenantsClient(new State { Json = json }).GetLoginTenantsDetailsAsync("context"); Assert.That(result.Tenants?.Count, Is.EqualTo(count)); Assert.That(result.DefaultTenantId, Is.Null); }

        [Test]
        public async Task LoginListingRetainsOrderAndUnknownEntryFields()
        {
            var result = await new TenantsClient(new State { Json = "{\"tenants\":[{\"id\":\"second\"},{\"id\":\"first\",\"future\":2}]}" }).GetLoginTenantsDetailsAsync("context");
            Assert.That(result.Tenants.Select(t => t.Id), Is.EqualTo(new[] { "second", "first" })); Assert.That(result.Tenants[0].Expired, Is.Null); Assert.That(result.Tenants[0].Cancelled, Is.Null); Assert.That(result.Tenants[0].User, Is.Null); Assert.That(result.Tenants[1].GetRawDetails()["future"].Value<int>(), Is.EqualTo(2));
        }

        [TestCase("activated"), TestCase("available"), TestCase("not-available"), TestCase("future")]
        public async Task FeatureStatusesRetainCallerVisibleStringsWithoutPermissionInference(string status)
        {
            var result = await new FeaturesClient(new State { Json = "{\"archiving\":\"" + status + "\",\"auditing\":null,\"future-feature\":\"future-state\"}" }).GetTenantFeaturesDetailsAsync("context", "current");
            Assert.That(result.ArchivingStatus, Is.EqualTo(status)); Assert.That(result.AuditingStatus, Is.Null); Assert.That(result.Statuses["future-feature"], Is.EqualTo("future-state")); Assert.That(result.Statuses.ContainsKey("auditing"), Is.True);
        }

        [Test]
        public async Task MissingFeatureMetadataIsUnknownAndMapIsImmutable()
        {
            var result = await new FeaturesClient(new State()).GetTenantFeaturesDetailsAsync("context", "current");
            Assert.That(result.ArchivingStatus, Is.Null); Assert.That(result.Statuses.ContainsKey("archiving"), Is.False); Assert.That(((IDictionary<string, string>)result.Statuses).IsReadOnly, Is.True);
        }

        [TestCase(false), TestCase(true)]
        public async Task ProjectionSelectionsAreCopiedBeforeAuthorization(bool login)
        {
            var fields = new[] { "future&field", "name" }; var state = new State { BeforeAuthorize = () => fields[0] = "changed" };
            if (login) await new TenantsClient(state).GetLoginTenantsDetailsAsync("context", fields); else await new TenantClient(state).GetTenantDetailsAsync("context", "part/with?query", fields);
            var query = state.Request.Parameters.Single(p => p.Type == ParameterType.QueryString); Assert.That(query.Name, Is.EqualTo("data")); Assert.That(query.Value, Is.EqualTo("future&field,name"));
            if (!login) Assert.That(state.Request.Resource, Is.EqualTo("v2/tenant/part%2Fwith%3Fquery"));
        }

        [TestCase("selected", "{\"id\":\"other\"}"), TestCase("selected", "{\"id\":2}"), TestCase("selected", "{\"cancelled\":0}"), TestCase("selected", "{\"logo\":[]}"), TestCase("selected", "{\"logo\":{\"data\":2}}")]
        [TestCase("login", "{\"tenants\":{}}"), TestCase("login", "{\"tenants\":[{}]}"), TestCase("login", "{\"tenants\":[{\"id\":\"x\",\"expired\":\"false\"}]}"), TestCase("login", "{\"tenants\":[{\"id\":\"x\",\"user\":{}}]}"), TestCase("login", "{\"default-tenant-id\":2}")]
        [TestCase("features", "{\"archiving\":false}"), TestCase("features", "{\"archiving\":\"available\",\"archiving\":\"activated\"}")]
        [TestCase("selected", "{} {}"), TestCase("login", "[]"), TestCase("features", "{} {}")]
        public void MalformedSuccessPreservesAnInnerDiagnostic(string endpoint, string json)
        { Assert.That(Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await Invoke(new State { Json = json }, endpoint))).InnerException, Is.Not.Null); }

        [TestCase("selected", 204), TestCase("selected", 403), TestCase("login", 204), TestCase("login", 500), TestCase("features", 204), TestCase("features", 403)]
        public void UnexpectedStatusesDoNotBecomeEmptyMetadata(string endpoint, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, endpoint))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void UnusableSelectedIdentitiesFailBeforeAuthorization(string id)
        {
            var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new TenantClient(state).GetTenantDetailsAsync("context", id)));
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new FeaturesClient(state).GetTenantFeaturesDetailsAsync("context", id))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("empty"), TestCase("null"), TestCase("blank"), TestCase("comma")]
        public void UnusableDataSelectionsFailBeforeAuthorization(string kind)
        {
            var fields = kind == "empty" ? Array.Empty<string>() : new[] { kind == "null" ? null : kind == "blank" ? " " : "id,name" }; var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new TenantClient(state).GetTenantDetailsAsync("context", "current", fields)));
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new TenantsClient(state).GetLoginTenantsDetailsAsync("context", fields))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("selected"), TestCase("login"), TestCase("features")]
        public void PreCancellationDoesNotAuthorizeOrSend(string endpoint)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, endpoint, source.Token))); } Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase("selected"), TestCase("login"), TestCase("features")]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp(string endpoint)
        {
            var state = new State { Pending = true }; using (var source = new CancellationTokenSource())
            { var pending = Invoke(state, endpoint, source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); }
        }

        [TestCase("selected", false), TestCase("selected", true), TestCase("login", false), TestCase("login", true), TestCase("features", false), TestCase("features", true)]
        public async Task FactoryGetsMetadataThroughPrivateTcpLoopback(string endpoint, bool sync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null; var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync()) using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
                        while (bytes.Count < 32768)
                        { if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]); if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break; }
                        headers = Encoding.ASCII.GetString(bytes.ToArray()); var payload = Encoding.UTF8.GetBytes(endpoint == "selected" ? "{\"name\":\"Fixture\",\"cancelled\":false}" : endpoint == "login" ? "{\"default-tenant-id\":\"preferred\"}" : "{\"archiving\":\"available\"}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n"); await stream.WriteAsync(reply, 0, reply.Length, source.Token); await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    if (endpoint == "selected") { var client = factory.Tenant; client.DisableOfflineModeSimulation(); var result = sync ? client.GetTenantDetails("context", "current", new[] { "name", "cancelled" }, source.Token) : await client.GetTenantDetailsAsync("context", "current", new[] { "name", "cancelled" }, source.Token); Assert.That(result.Cancelled, Is.False); }
                    else if (endpoint == "login") { var client = factory.Tenants; client.DisableOfflineModeSimulation(); var result = sync ? client.GetLoginTenantsDetails("context", new[] { "default-tenant-id" }, source.Token) : await client.GetLoginTenantsDetailsAsync("context", new[] { "default-tenant-id" }, source.Token); Assert.That(result.DefaultTenantId, Is.EqualTo("preferred")); Assert.That(result.Tenants, Is.Null); }
                    else { var client = factory.TenantFeatures; client.DisableOfflineModeSimulation(); var result = sync ? client.GetTenantFeaturesDetails("context", "current", source.Token) : await client.GetTenantFeaturesDetailsAsync("context", "current", source.Token); Assert.That(result.ArchivingStatus, Is.EqualTo("available")); }
                    await server; var target = headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ')[1];
                    Assert.That(target.Split('?')[0], Is.EqualTo(endpoint == "selected" ? "/bridge/v2/tenant/current" : endpoint == "login" ? "/bridge/v2/tenants" : "/bridge/v2/tenant/current/features"));
                    if (endpoint != "features") Assert.That(Uri.UnescapeDataString(target.Split('?')[1]), Is.EqualTo(endpoint == "selected" ? "data=name,cancelled" : "data=default-tenant-id"));
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).All(line => line.EndsWith(" 0")), Is.True);
                }
                finally { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private static async Task<object> Invoke(State state, string endpoint, CancellationToken token = default(CancellationToken))
        { if (endpoint == "selected") return await new TenantClient(state).GetTenantDetailsAsync("context", "selected", cancellationToken: token); if (endpoint == "login") return await new TenantsClient(state).GetLoginTenantsDetailsAsync("context", cancellationToken: token); return await new FeaturesClient(state).GetTenantFeaturesDetailsAsync("context", "selected", token); }
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-tenant-details-tests"; }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class TenantClient : TenantRestClient
        { private readonly State state; internal TenantClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class TenantsClient : TenantsRestClient
        { private readonly State state; internal TenantsClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class FeaturesClient : TenantFeaturesRestClient
        { private readonly State state; internal FeaturesClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
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
