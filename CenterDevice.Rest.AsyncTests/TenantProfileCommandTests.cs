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
    public class TenantProfileCommandTests
    {
        private static OAuthInfo Authorization() => new OAuthInfo { UserId = "context", access_token = "isolated-token" };

        [TestCase(false), TestCase(true)]
        public async Task ProfileUpdateSnapshotsNameAndNestedImageWithoutChangingAuthentication(bool sync)
        {
            var input = new TenantUpdateRequest { Name = "ä tenant", LogoData = "AQID" };
            var state = new State { BeforeSend = () => { input.Name = "changed"; input.LogoData = "BAUG"; } };
            var authorization = Authorization();
            var client = new TenantClient(state);
            if (sync) client.UpdateTenant(authorization, "id with/slash", input); else await client.UpdateTenantAsync(authorization, "id with/slash", input);
            Assert.That(state.Authorization, Is.SameAs(authorization));
            Assert.That(authorization.UserId, Is.EqualTo("context"));
            Assert.That(Body(state).Value<string>("name"), Is.EqualTo("ä tenant"));
            Assert.That(Body(state)["logo"].Value<string>("data"), Is.EqualTo("AQID"));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/tenant/id%20with%2Fslash"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(null), TestCase("")]
        public async Task NullNamePreservesFieldAndEmptyNameRemainsExplicit(string name)
        {
            var state = new State();
            await new TenantClient(state).UpdateTenantAsync(Authorization(), "current", new TenantUpdateRequest { Name = name, LogoData = "AQID" });
            Assert.That(Body(state)["name"]?.Value<string>(), Is.EqualTo(name));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/tenant/current"));
        }

        [Test]
        public async Task NullLogoNeverTurnsNameUpdateIntoLogoRemoval()
        {
            var state = new State();
            await new TenantClient(state).UpdateTenantAsync(Authorization(), "selected", new TenantUpdateRequest { Name = "name" });
            Assert.That(Body(state).Properties().Select(property => property.Name), Is.EqualTo(new[] { "name" }));
        }

        [TestCase(false), TestCase(true)]
        public async Task ExplicitRemovalSendsAnEmptyLogoObjectAndNoName(bool sync)
        {
            var state = new State(); var client = new TenantClient(state);
            if (sync) client.RemoveTenantLogo(Authorization(), "selected"); else await client.RemoveTenantLogoAsync(Authorization(), "selected");
            Assert.That(Body(state).Properties().Select(property => property.Name), Is.EqualTo(new[] { "logo" }));
            Assert.That(Body(state)["logo"], Is.TypeOf<JObject>().And.Empty);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
        }

        [TestCase(false), TestCase(true)]
        public async Task DefaultSelectionIsAnExplicitLoginCommandWithoutSwitchingClientAuthorization(bool sync)
        {
            var state = new State(); var authorization = Authorization(); var client = new TenantsClient(state);
            if (sync) client.SetDefaultTenant(authorization, "explicit tenant"); else await client.SetDefaultTenantAsync(authorization, "explicit tenant");
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo("set-default-tenant"));
            Assert.That(Body(state)["params"].Value<string>("tenant"), Is.EqualTo("explicit tenant"));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/tenants"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Authorization, Is.SameAs(authorization)); Assert.That(authorization.UserId, Is.EqualTo("context"));
        }

        [TestCase("null"), TestCase("empty"), TestCase("invalid-base64"), TestCase("empty-logo"), TestCase("oversized-logo")]
        public void InvalidUpdateSelectionsFailBeforeHttp(string problem)
        {
            var input = problem == "null" ? null : new TenantUpdateRequest
            {
                LogoData = problem == "invalid-base64" ? "?" : problem == "empty-logo" ? "" : problem == "oversized-logo" ? Convert.ToBase64String(new byte[5 * 1024 * 1024 + 1]) : null
            };
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new TenantClient(state).UpdateTenantAsync(Authorization(), "selected", input)));
            Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public async Task ExactFiveMiBLogoLimitIsAcceptedAndWireSpellingIsPreserved()
        {
            var state = new State(); var logo = Convert.ToBase64String(new byte[5 * 1024 * 1024]);
            await new TenantClient(state).UpdateTenantAsync(Authorization(), "selected", new TenantUpdateRequest { LogoData = logo });
            Assert.That(Body(state)["logo"].Value<string>("data"), Is.EqualTo(logo));
        }

        [TestCase("update", null), TestCase("update", ""), TestCase("update", "."), TestCase("update", "..")]
        [TestCase("remove", null), TestCase("remove", " "), TestCase("default", null), TestCase("default", " ")]
        public void InvalidTenantSelectionsFailBeforeHttp(string operation, string id)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await Invoke(state, operation, Authorization(), id)));
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("update"), TestCase("remove"), TestCase("default")]
        public void NullAuthorizationNeverStartsHttp(string operation)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => await Invoke(state, operation, null)));
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("update"), TestCase("remove"), TestCase("default")]
        public async Task NativeCancellationIsPrecheckedAndForwardedWithoutReplay(string operation)
        {
            var state = new State { Pending = true };
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, Authorization(), token: source.Token)));
                Assert.That(state.Calls, Is.Zero);
            }
            using (var source = new CancellationTokenSource())
            {
                var pending = Invoke(state, operation, Authorization(), token: source.Token);
                await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token));
                source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
                Assert.That(state.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void OriginalHttpFailureCauseIsRetainedWithoutReplay(int status, Type type)
        {
            var cause = new IOException("isolated original cause");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.ThrowsAsync(type, (Func<Task>)(async () => await Invoke(state, "update", Authorization())));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("update"), TestCase("remove"), TestCase("default")]
        public void Http200CannotBeMisreportedAsConfirmed204Mutation(string operation)
        {
            var state = new State { Status = HttpStatusCode.OK };
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation, Authorization())));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("update", 204), TestCase("remove", 204), TestCase("default", 204), TestCase("default", 503)]
        public async Task ActualFactoryWirePreservesPrefixNestedProfileAndExplicitDefaultWithoutReplay(string operation, int status)
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
                        int length = int.Parse(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).Split(':')[1].Trim());
                        var payload = new byte[length]; int count = 0;
                        while (count < length) { var read = await stream.ReadAsync(payload, count, length - count, source.Token); if (read == 0) throw new EndOfStreamException(); count += read; }
                        sent = Encoding.UTF8.GetString(payload);
                        var responseBody = status == 503 ? "{}" : "";
                        var responsePayload = Encoding.UTF8.GetBytes(responseBody);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + responsePayload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                        if (responsePayload.Length > 0) await stream.WriteAsync(responsePayload, 0, responsePayload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "default")
                        {
                            var client = factory.Tenants; client.DisableOfflineModeSimulation();
                            await client.SetDefaultTenantAsync(Authorization(), "selected", source.Token);
                        }
                        else
                        {
                            var client = factory.Tenant; client.DisableOfflineModeSimulation();
                            if (operation == "remove") await client.RemoveTenantLogoAsync(Authorization(), "selected", source.Token);
                            else await client.UpdateTenantAsync(Authorization(), "selected", new TenantUpdateRequest { Name = "ä tenant", LogoData = "AQID" }, source.Token);
                        }
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(operation == "default" ? "POST /bridge/v2/tenants HTTP/1.1" : "PUT /bridge/v2/tenant/selected HTTP/1.1"));
                    var data = JObject.Parse(sent);
                    if (operation == "default") Assert.That(data["params"].Value<string>("tenant"), Is.EqualTo("selected"));
                    else if (operation == "remove") Assert.That(data["logo"], Is.TypeOf<JObject>().And.Empty);
                    else { Assert.That(data.Value<string>("name"), Is.EqualTo("ä tenant")); Assert.That(data["logo"].Value<string>("data"), Is.EqualTo("AQID")); }
                    Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static Task Invoke(State state, string operation, OAuthInfo authorization, string id = "selected", CancellationToken token = default(CancellationToken))
            => operation == "update" ? new TenantClient(state).UpdateTenantAsync(authorization, id, new TenantUpdateRequest { Name = "name" }, token)
                : operation == "remove" ? new TenantClient(state).RemoveTenantLogoAsync(authorization, id, token)
                : new TenantsClient(state).SetDefaultTenantAsync(authorization, id, token);
        private static JObject Body(State state)
        {
            var value = state.Request.Parameters.OfType<BodyParameter>().Single().Value;
            return value is string text ? JObject.Parse(text) : JObject.FromObject(value);
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-tenant-profile-tests";
        }
        private sealed class Auth : IOAuthInfoProvider
        {
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Provided authorization must be reused.");
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(Config config) : base(new Auth(), config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class TenantClient : TenantRestClient
        {
            private readonly State state;
            internal TenantClient(State state) : base(new Auth(), new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(info, request, token);
        }
        private sealed class TenantsClient : TenantsRestClient
        {
            private readonly State state;
            internal TenantsClient(State state) : base(new Auth(), new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(info, request, token);
        }
        private sealed class State
        {
            internal RestRequest Request;
            internal OAuthInfo Authorization;
            internal HttpStatusCode Status = HttpStatusCode.NoContent;
            internal Exception Error;
            internal Action BeforeSend;
            internal bool Pending;
            internal int Calls;
            internal CancellationToken Token;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal async Task<RestResponse> Send(OAuthInfo info, RestRequest request, CancellationToken token)
            {
                Calls++; BeforeSend?.Invoke(); Authorization = info; Request = request; Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = Status, Content = "{}", ErrorException = Error };
            }
        }
    }
}
