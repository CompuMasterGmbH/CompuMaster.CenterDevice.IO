using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.User;
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
    public class UserInvitationClientTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OriginalSelectionSnapshotsBeforeAuthorizationAndReturnsImmutableCreatedUsers(bool sync)
        {
            var addresses = new List<string> { "one@example.test" }; int enumerations = 0;
            IEnumerable<string> Values() { enumerations++; foreach (var address in addresses) yield return address; }
            var state = new State { BeforeAuthorize = () => addresses.Clear() };
            var client = new Client(state);
            var result = sync ? client.InviteUsers("context", Values()) : await client.InviteUsersAsync("context", Values());
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo("invite"));
            Assert.That(Body(state)["params"]["emails"].Values<string>(), Is.EqualTo(new[] { "one@example.test" }));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/users")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(enumerations, Is.EqualTo(1));
            Assert.That(result.Invited.Single().UserId, Is.EqualTo("created")); Assert.That(result.AllSucceeded, Is.True);
            Assert.That(((IList<UserInvitation>)result.Invited).IsReadOnly, Is.True);
        }

        [TestCase(1), TestCase(2), TestCase(3), TestCase(100), TestCase(9000000000L)]
        public async Task ManufacturerAndFutureFailureCodesNeverCountAsInvitationSuccess(long code)
        {
            var state = new State { Json = "{\"invited\":[],\"not-invited\":[{\"email\":\"bad-address\",\"code\":" + code + "}]}" };
            var result = await new Client(state).InviteUsersAsync("context", new[] { "bad-address" });
            Assert.That(result.AllSucceeded, Is.False); Assert.That(result.NotInvited.Single().Code, Is.EqualTo(code));
            Assert.That(result.NotInvited.Single().Email, Is.EqualTo("bad-address"));
        }

        [Test]
        public async Task OriginalDuplicatesAndInvalidEmptySyntaxRemainExplicitForServerOutcomes()
        {
            var state = new State { Json = "{\"invited\":[],\"not-invited\":[{\"email\":\"\",\"code\":1},{\"email\":\"\",\"code\":1}]}" };
            var result = await new Client(state).InviteUsersAsync("context", new[] { "", "" });
            Assert.That(Body(state)["params"]["emails"].Values<string>(), Is.EqualTo(new[] { "", "" }));
            Assert.That(result.NotInvited.Count, Is.EqualTo(2)); Assert.That(result.AllSucceeded, Is.False);
        }

        [Test]
        public async Task MixedSuccessAndFailureRetainExactServerSpellingAndOrder()
        {
            var state = new State { Json = "{\"invited\":[{\"email\":\"ONE@example.test\",\"user-id\":\"created\"}],\"not-invited\":[{\"email\":\"bad-address\",\"code\":1}]}" };
            var result = await new Client(state).InviteUsersAsync("context", new[] { "one@example.test", "bad-address" });
            Assert.That(result.Invited.Single().Email, Is.EqualTo("ONE@example.test")); Assert.That(result.AllSucceeded, Is.False);
            Assert.That(result.NotInvited.Single().Code, Is.EqualTo(1));
        }

        [TestCase("{}"), TestCase("null"), TestCase("[]"), TestCase("{\"invited\":null,\"not-invited\":[]}")]
        [TestCase("{\"invited\":[],\"not-invited\":null}"), TestCase("{\"invited\":[],\"not-invited\":[]}")]
        [TestCase("{\"invited\":[{\"email\":\"foreign\",\"user-id\":\"created\"}],\"not-invited\":[]}")]
        [TestCase("{\"invited\":[{\"email\":\"one@example.test\",\"user-id\":null}],\"not-invited\":[]}")]
        [TestCase("{\"invited\":[{\"email\":null,\"user-id\":\"created\"}],\"not-invited\":[]}")]
        [TestCase("{\"invited\":[1],\"not-invited\":[]}")]
        [TestCase("{\"invited\":[],\"not-invited\":[{\"email\":\"one@example.test\",\"code\":\"1\"}]}")]
        [TestCase("{\"invited\":[],\"not-invited\":[{\"email\":\"one@example.test\",\"code\":1.5}]}")]
        [TestCase("{\"invited\":[],\"not-invited\":[{\"email\":\"one@example.test\",\"code\":9223372036854775808}]}")]
        [TestCase("{\"invited\":[],\"invited\":[{\"email\":\"one@example.test\",\"user-id\":\"created\"}],\"not-invited\":[]}")]
        [TestCase("{\"invited\":[{\"email\":\"one@example.test\",\"user-id\":\"created\"}],\"not-invited\":[]} {}")]
        public void MalformedForeignIncompleteAndOverflowResultsCannotEstablishSuccess(string json)
        {
            var state = new State { Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).InviteUsersAsync("context", new[] { "one@example.test" })));
            Assert.That(error.InnerException, Is.Not.Null); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public void PartialMissingSelectionCannotEstablishCompleteSuccess()
        {
            var state = new State();
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).InviteUsersAsync("context", new[] { "one@example.test", "unreported@example.test" })));
        }

        [TestCase("null"), TestCase("empty"), TestCase("null-item")]
        public void UnusableSelectionsRejectBeforeAuthorization(string problem)
        {
            var state = new State(); var emails = problem == "null" ? null : problem == "empty" ? new string[0] : new[] { (string)null };
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).InviteUsersAsync("context", emails)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(500, typeof(InternalServerErrorException))]
        public void OriginalPermissionAndServerErrorsRemainWithoutReplay(int status, Type type)
        {
            var cause = new IOException("isolated cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.ThrowsAsync(type, (Func<Task>)(async () => await new Client(state).InviteUsersAsync("context", new[] { "one@example.test" })));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task PreAndActiveCancellationReachAuthorizationAndHttpWithoutReplay()
        {
            var state = new State { Pending = true };
            IEnumerable<string> Values() { yield return Throw(); }
            string Throw() => throw new AssertionException("Pre-canceled selection was enumerated.");
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).InviteUsersAsync("context", Values(), source.Token)));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var source = new CancellationTokenSource())
            {
                var pending = new Client(state).InviteUsersAsync("context", new[] { "one@example.test" }, source.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token));
                source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(200), TestCase(503)]
        public async Task ActualFactoryUsesUsersRouteAndNoUncertainInvitationReplay(int status)
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
                        var responsePayload = Encoding.UTF8.GetBytes(status == 200 ? "{\"invited\":[{\"email\":\"one@example.test\",\"user-id\":\"created\"}],\"not-invited\":[]}" : "{}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 200 ? " OK" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + responsePayload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                        await stream.WriteAsync(responsePayload, 0, responsePayload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.Users; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () => Assert.That((await client.InviteUsersAsync("context", new[] { "one@example.test" }, source.Token)).AllSucceeded, Is.True);
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("POST /bridge/v2/users HTTP/1.1"));
                    Assert.That(JObject.Parse(sent).Value<string>("action"), Is.EqualTo("invite"));
                    Assert.That(JObject.Parse(sent)["params"]["emails"].Values<string>(), Is.EqualTo(new[] { "one@example.test" }));
                    Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static JObject Body(State state)
        {
            var value = state.Request.Parameters.OfType<BodyParameter>().Single().Value;
            return value is string text ? JObject.Parse(text) : JObject.FromObject(value);
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-user-invitation-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(Config config) : base(new State(), config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class Client : UsersRestClient
        {
            private readonly State state;
            internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal string Context;
            internal string Json = "{\"invited\":[{\"email\":\"one@example.test\",\"user-id\":\"created\"}],\"not-invited\":[]}";
            internal Exception Error;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token)
            {
                Calls++; Request = request; Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = Status, Content = Json, ErrorException = Error };
            }
        }
    }
}
