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
    public class UserListingDetailsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OmittedSelectionsPreserveServerDefaultsAndUnknownMetadata(bool sync)
        {
            var state = new State(); var client = new Client(state);
            var result = sync ? client.GetUsersDetails("context") : await client.GetUsersDetailsAsync("context");
            Assert.That(state.Request.Resource, Is.EqualTo("v2/users")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Parameters.Where(p => p.Type == ParameterType.QueryString), Is.Empty);
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(result.Single().Id, Is.EqualTo("selected")); Assert.That(result.Single().TechnicalUser, Is.Null);
            Assert.That(result.Single().Role, Is.Null); Assert.That(result.Single().FirstName, Is.Null);
            Assert.That(((ICollection<UserListingDetails>)result).IsReadOnly, Is.True);
        }

        [TestCase(null, null), TestCase(false, false), TestCase(true, false), TestCase(false, true), TestCase(true, true)]
        public async Task NullableFlagsAndFutureStatusesRemainOriginal(bool? deleted, bool? all)
        {
            var state = new State(); await new Client(state).GetUsersDetailsAsync("context", new[] { "active", "future status", "active" }, deleted, all);
            var query = state.Request.Parameters.Where(p => p.Type == ParameterType.QueryString).ToDictionary(p => p.Name, p => p.Value.ToString());
            Assert.That(query["status"], Is.EqualTo("active,future status,active"));
            Assert.That(query.ContainsKey("include-deleted"), Is.EqualTo(deleted.HasValue)); Assert.That(query.ContainsKey("all"), Is.EqualTo(all.HasValue));
            if (deleted.HasValue) Assert.That(query["include-deleted"], Is.EqualTo(deleted.Value ? "true" : "false"));
            if (all.HasValue) Assert.That(query["all"], Is.EqualTo(all.Value ? "true" : "false"));
        }

        [Test]
        public async Task StatusSelectionIsSnapshottedBeforeAuthorization()
        {
            var statuses = new[] { "active" }; var state = new State { BeforeAuthorize = () => statuses[0] = "blocked" };
            await new Client(state).GetUsersDetailsAsync("context", statuses);
            Assert.That(state.Request.Parameters.Single(p => p.Type == ParameterType.QueryString).Value, Is.EqualTo("active"));
        }

        [TestCase("empty"), TestCase("null"), TestCase("blank"), TestCase("comma")]
        public void InvalidSuppliedSelectionsFailBeforeAuthorization(string kind)
        {
            var state = new State(); var statuses = kind == "empty" ? Array.Empty<string>() : new[] { kind == "null" ? null : kind == "blank" ? " " : "active,blocked" };
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetUsersDetailsAsync("context", statuses)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("true", true), TestCase("false", false), TestCase("null", null)]
        public async Task HyphenatedMetadataAndExplicitTechnicalValuesAreNotInferred(string jsonBoolean, bool? expected)
        {
            var state = new State { Json = "{\"users\":[{\"id\":\"selected\",\"first-name\":\" First \",\"last-name\":\"\",\"email\":null,\"role\":\"future-role\",\"status\":\"deleted\",\"technical-user\":" + jsonBoolean + "}]}" };
            var item = (await new Client(state).GetUsersDetailsAsync("context", new[] { "active" })).Single();
            Assert.That(item.FirstName, Is.EqualTo(" First ")); Assert.That(item.LastName, Is.Empty); Assert.That(item.Email, Is.Null);
            Assert.That(item.Role, Is.EqualTo("future-role")); Assert.That(item.Status, Is.EqualTo("deleted")); Assert.That(item.TechnicalUser, Is.EqualTo(expected));
        }

        [Test]
        public async Task ServerOrderAndRepeatedEntriesRemainUnfiltered()
        {
            var state = new State { Json = "{\"users\":[{\"id\":\"second\",\"status\":\"blocked\"},{\"id\":\"first\"},{\"id\":\"second\"}]}" };
            Assert.That((await new Client(state).GetUsersDetailsAsync("context", new[] { "active" })).Select(u => u.Id), Is.EqualTo(new[] { "second", "first", "second" }));
        }

        [Test]
        public async Task ExplicitEmptyUserArrayReturnsAnEmptyList()
        { Assert.That(await new Client(new State { Json = "{\"users\":[]}" }).GetUsersDetailsAsync("context"), Is.Empty); }

        [TestCase("{}"), TestCase("{\"users\":null}"), TestCase("{\"users\":{}}"), TestCase("{\"users\":[null]}")]
        [TestCase("{\"users\":[{}]}"), TestCase("{\"users\":[{\"id\":2}]}"), TestCase("{\"users\":[{\"id\":\" \"}]}")]
        [TestCase("{\"users\":[{\"id\":\"x\",\"first-name\":2}]}"), TestCase("{\"users\":[{\"id\":\"x\",\"technical-user\":\"true\"}]}")]
        [TestCase("{\"users\":[],\"users\":[]}"), TestCase("{\"users\":[{\"id\":\"x\",\"id\":\"y\"}]}"), TestCase("{\"users\":[]} {}")]
        public void MalformedSuccessfulMetadataRetainsAnInnerDiagnostic(string json)
        {
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = json }).GetUsersDetailsAsync("context")));
            Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase(204), TestCase(403), TestCase(404), TestCase(500)]
        public void UnexpectedStatusesDoNotBecomeEmptySuccessfulLists(int status)
        {
            var state = new State { Status = (HttpStatusCode)status, Json = "{\"users\":[]}" };
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetUsersDetailsAsync("context")));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public void PreCancellationDoesNotAuthorizeOrSend()
        {
            var state = new State(); using (var source = new CancellationTokenSource())
            { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetUsersDetailsAsync("context", cancellationToken: source.Token))); }
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public async Task ActiveCancellationUsesNativeAuthorizationAndHttp()
        {
            var state = new State { Pending = true }; using (var source = new CancellationTokenSource())
            {
                var pending = new Client(state).GetUsersDetailsAsync("context", cancellationToken: source.Token); await state.Entered.Task;
                Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); Assert.That(state.Token, Is.EqualTo(source.Token)); source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
            }
        }

        [TestCase(false), TestCase(true)]
        public async Task FactorySendsEncodedBodylessListingWithOriginalResponseMetadata(bool sync)
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
                            if (await stream.ReadAsync(one, 0, 1, source.Token) == 0) throw new EndOfStreamException(); bytes.Add(one[0]);
                            if (bytes.Count >= 4 && Encoding.ASCII.GetString(bytes.Skip(bytes.Count - 4).ToArray()) == "\r\n\r\n") break;
                        }
                        headers = Encoding.ASCII.GetString(bytes.ToArray()); var payload = Encoding.UTF8.GetBytes("{\"users\":[{\"id\":\"selected\",\"first-name\":\"Fixture\",\"technical-user\":true}]}");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token); await stream.WriteAsync(payload, 0, payload.Length, source.Token);
                    }
                });
                try
                {
                    var client = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/")).Users; client.DisableOfflineModeSimulation();
                    var result = sync ? client.GetUsersDetails("context", new[] { "future&include-deleted=false" }, true, false, source.Token) : await client.GetUsersDetailsAsync("context", new[] { "future&include-deleted=false" }, true, false, source.Token);
                    await server; Assert.That(result.Single().FirstName, Is.EqualTo("Fixture")); Assert.That(result.Single().TechnicalUser, Is.True);
                    var target = headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0].Split(' ')[1]; var query = target.Split('?')[1].Split('&').Select(p => p.Split(new[] { '=' }, 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));
                    Assert.That(target.Split('?')[0], Is.EqualTo("/bridge/v2/users")); Assert.That(query.Count, Is.EqualTo(3));
                    Assert.That(query["status"], Is.EqualTo("future&include-deleted=false")); Assert.That(query["include-deleted"], Is.EqualTo("true")); Assert.That(query["all"], Is.EqualTo("false"));
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None).Where(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)).All(line => line.EndsWith(" 0")), Is.True);
                }
                finally
                { source.Cancel(); listener.Stop(); try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { } }
            }
        }

        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-user-listing-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        { internal Factory(Config config) : base(new State(), config, null, "v2/") { } protected override string UploadLinkBaseUrl => "https://unused.invalid/"; }
        private sealed class Client : UsersRestClient
        {
            private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Context, Json = "{\"users\":[{\"id\":\"selected\"}]}";
            internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token)
            { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
