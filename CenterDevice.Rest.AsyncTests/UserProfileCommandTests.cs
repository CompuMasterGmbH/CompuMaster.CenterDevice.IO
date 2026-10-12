using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.User;
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
    public class UserProfileCommandTests
    {
        [TestCase(false), TestCase(true)]
        public async Task ProfileSnapshotsAllFieldsBeforeAuthorizationAndKeepsSelectedUserSeparate(bool sync)
        {
            var input = new UserUpdateRequest { FirstName = "ä first", LastName = "last", PictureData = "AQID" };
            var state = new State { BeforeAuthorize = () => { input.FirstName = "changed"; input.LastName = "changed"; input.PictureData = "BAUG"; } };
            var client = new UserClient(state);
            if (sync) client.UpdateUserDetails("context", "selected with/slash", input); else await client.UpdateUserDetailsAsync("context", "selected with/slash", input);
            Assert.That(Body(state).Value<string>("first-name"), Is.EqualTo("ä first"));
            Assert.That(Body(state).Value<string>("last-name"), Is.EqualTo("last"));
            Assert.That(Body(state)["picture"].Value<string>("data"), Is.EqualTo("AQID"));
            Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/user/selected%20with%2Fslash"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
        }

        [TestCase(null), TestCase("")]
        public async Task OmittedAndEmptyNamesRemainDifferentWithoutClearingPicture(string first)
        {
            var state = new State();
            await new UserClient(state).UpdateUserDetailsAsync("context", "selected", new UserUpdateRequest { FirstName = first, LastName = "name" });
            Assert.That(Body(state)["first-name"]?.Value<string>(), Is.EqualTo(first));
            Assert.That(Body(state)["picture"], Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public async Task ExplicitRemovalSendsAnEmptyPictureObjectWithoutChangingNames(bool sync)
        {
            var state = new State(); var client = new UserClient(state);
            if (sync) client.RemoveUserPicture("context", "selected"); else await client.RemoveUserPictureAsync("context", "selected");
            Assert.That(Body(state).Properties().Select(property => property.Name), Is.EqualTo(new[] { "picture" }));
            Assert.That(Body(state)["picture"], Is.TypeOf<JObject>().And.Empty);
        }

        [TestCase("null"), TestCase("empty"), TestCase("bad-base64"), TestCase("empty-picture"), TestCase("oversize")]
        public void InvalidUpdatesRejectBeforeAuthorization(string problem)
        {
            var input = problem == "null" ? null : new UserUpdateRequest { PictureData = problem == "bad-base64" ? "?" : problem == "empty-picture" ? "" : problem == "oversize" ? Convert.ToBase64String(new byte[5 * 1024 * 1024 + 1]) : null };
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new UserClient(state).UpdateUserDetailsAsync("context", "selected", input)));
            Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero);
        }

        [Test]
        public async Task ExactFiveMiBDecodedPictureLimitRemainsAccepted()
        {
            var state = new State(); var picture = Convert.ToBase64String(new byte[5 * 1024 * 1024]);
            await new UserClient(state).UpdateUserDetailsAsync("context", "selected", new UserUpdateRequest { PictureData = picture });
            Assert.That(Body(state)["picture"].Value<string>("data"), Is.EqualTo(picture));
        }

        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidUserSelectionRejectsBeforeAuthorization(string id)
        {
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new UserClient(state).RemoveUserPictureAsync("context", id)));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public void PreCancellationWinsBeforeInvalidRequestAndAuthorization(bool remove)
        {
            var state = new State();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, remove, source.Token)));
            }
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttpWithoutReplay(bool remove)
        {
            var state = new State { Pending = true };
            using (var source = new CancellationTokenSource())
            {
                var pending = Invoke(state, remove, source.Token); await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token));
                source.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending));
                Assert.That(state.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void ServerOwnProfileErrorsPreserveTheirCauseAndAreNotRetried(int status, Type type)
        {
            var cause = new IOException("isolated original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.ThrowsAsync(type, (Func<Task>)(async () => await Invoke(state, false)));
            Assert.That(error.InnerException, Is.SameAs(cause)); Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public void Http200IsNotConfirmedProfileMutation(bool remove)
        {
            var state = new State { Status = HttpStatusCode.OK };
            Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, remove)));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false, 204), TestCase(true, 204), TestCase(false, 503)]
        public async Task ActualFactorySendsExplicitUserAndNestedPictureWithoutWriteReplay(bool remove, int status)
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
                        var responsePayload = Encoding.UTF8.GetBytes(status == 503 ? "{}" : "");
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 204 ? " No Content" : " Service Unavailable") +
                            "\r\nContent-Type: application/json\r\nContent-Length: " + responsePayload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, source.Token);
                        if (responsePayload.Length > 0) await stream.WriteAsync(responsePayload, 0, responsePayload.Length, source.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    var client = factory.User; client.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (remove) await client.RemoveUserPictureAsync("context", "selected", source.Token);
                        else await client.UpdateUserDetailsAsync("context", "selected", new UserUpdateRequest { FirstName = "ä first", LastName = "", PictureData = "AQID" }, source.Token);
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(headers.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo("PUT /bridge/v2/user/selected HTTP/1.1"));
                    var data = JObject.Parse(sent);
                    if (remove) Assert.That(data["picture"], Is.TypeOf<JObject>().And.Empty);
                    else { Assert.That(data.Value<string>("first-name"), Is.EqualTo("ä first")); Assert.That(data.Value<string>("last-name"), Is.Empty); Assert.That(data["picture"].Value<string>("data"), Is.EqualTo("AQID")); }
                    Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    source.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private static Task Invoke(State state, bool remove, CancellationToken token = default(CancellationToken))
            => remove ? new UserClient(state).RemoveUserPictureAsync("context", "selected", token)
                : new UserClient(state).UpdateUserDetailsAsync("context", "selected", new UserUpdateRequest { FirstName = "name" }, token);
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-user-profile-tests";
        }
        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(Config config) : base(new State(), config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }
        private sealed class UserClient : UserRestClient
        {
            private readonly State state;
            internal UserClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.NoContent;
            internal Exception Error;
            internal string Context;
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
                return new RestResponse(request) { StatusCode = Status, Content = "{}", ErrorException = Error };
            }
        }
    }
}
