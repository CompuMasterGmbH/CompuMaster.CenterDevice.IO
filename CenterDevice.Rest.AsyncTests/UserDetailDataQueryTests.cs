using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.User;
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
    public class UserDetailDataQueryTests
    {
        [TestCase(false, "groups"), TestCase(true, "groups"), TestCase(false, "collections"), TestCase(true, "collections")]
        [TestCase(false, "picture"), TestCase(true, "picture"), TestCase(false, "id,first-name,last-name,email"), TestCase(true, "id,first-name,last-name,email")]
        [TestCase(false, "all"), TestCase(true, "all")]
        public async Task ExistingFieldGetterUsesTheManufacturerDataQueryAndPreservesSelections(bool sync, string field)
        {
            var state = new State(); var client = new Client(state);
            var result = sync ? client.GetUserDataField("context", "selected", field) : await client.GetUserDataFieldAsync("context", "selected", field);
            var query = state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString).ToArray();
            Assert.That(query.Length, Is.EqualTo(1));
            Assert.That(query[0].Name, Is.EqualTo("data"));
            Assert.That(query[0].Value, Is.EqualTo(field));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/user/selected"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(result, Is.TypeOf<FullUserData>());
        }

        [Test]
        public void NativePreCancellationDoesNotAuthorizeOrStartTheRequest()
        {
            var state = new State();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetUserDataFieldAsync("context", "selected", "groups", source.Token)));
            }
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public async Task NativeActiveCancellationStillReachesAuthorizationAndHttp()
        {
            var state = new State { Pending = true };
            using (var source = new CancellationTokenSource())
            {
                var pending = new Client(state).GetUserDataFieldAsync("context", "selected", "groups", source.Token);
                await state.Entered.Task;
                Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); Assert.That(state.Token, Is.EqualTo(source.Token));
                source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
            }
        }

        [TestCase(false), TestCase(true)]
        public async Task ActualFactorySendsBodylessDataProjectionOnTheExistingGetter(bool sync)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string headers = null;
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
                        var payload = Encoding.UTF8.GetBytes("{\"email\":\"fixture@example.test\"}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token); await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.User; client.DisableOfflineModeSimulation();
                    var result = sync ? client.GetUserDataField("context", "selected", "email") : await client.GetUserDataFieldAsync("context", "selected", "email", source.Token);
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("GET /bridge/v2/user/selected?data=email HTTP/1.1"));
                    Assert.That(result.Email, Is.EqualTo("fixture@example.test"));
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).All(line => line.EndsWith(" 0")), Is.True);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-user-detail-query-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(Config config) : base(new State(), config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Client : UserRestClient
        {
            private readonly State state;
            internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send<T>(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request;
            internal string Context;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) { Authorizations++; Context = userId; return new OAuthInfo { UserId = userId, access_token = "isolated-token" }; }
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            { AuthorizationToken = token; return Task.FromResult(GetOAuthInfo(userId)); }
            internal async Task<RestResponse<T>> Send<T>(RestRequest request, CancellationToken token) where T : new()
            {
                Calls++; Request = request; Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse<T>(request) { StatusCode = HttpStatusCode.OK, Data = new T() };
            }
        }
    }
}
