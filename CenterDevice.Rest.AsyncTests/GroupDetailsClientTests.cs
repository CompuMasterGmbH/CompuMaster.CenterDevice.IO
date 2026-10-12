using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Groups;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Model.Groups;
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
    public class GroupDetailsClientTests
    {
        private static JObject Record(string id = "group") => new JObject { ["id"] = id, ["name"] = "ä group", ["color"] = 16711680, ["type"] = "private", ["users"] = new JArray("member"),
            ["admins"] = new JArray(new JObject { ["user-id"] = "manager", ["privilege"] = "group-management" }) };

        [TestCase(false), TestCase(true)]
        public async Task SelectedImmutableDetailRetainsMembersAdminsTypeAndExactSelection(bool sync)
        {
            var state = new State { Json = Record().ToString() }; var client = new Client(state);
            var result = sync ? client.GetGroupDetails("context", "group") : await client.GetGroupDetailsAsync("context", "group");
            Assert.That(result.Id, Is.EqualTo("group")); Assert.That(result.Name, Is.EqualTo("ä group")); Assert.That(result.Color, Is.EqualTo(16711680)); Assert.That(result.Type, Is.EqualTo("private"));
            Assert.That(result.Users, Is.EqualTo(new[] { "member" })); Assert.That(result.Admins[0].UserId, Is.EqualTo("manager")); Assert.That(result.Admins[0].Privilege, Is.EqualTo("group-management"));
            Assert.That(((IList<string>)result.Users).IsReadOnly, Is.True); Assert.That(((IList<GroupAdministratorDetails>)result.Admins).IsReadOnly, Is.True);
            Assert.That(typeof(GroupDetails).GetProperty("Owner"), Is.Null); Assert.That(state.Request.Resource, Is.EqualTo("v2/group/group")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
        }

        [TestCase("absent"), TestCase("null"), TestCase("empty")]
        public async Task HiddenAndEmptyAdministratorAndMemberMetadataStayDistinct(string shape)
        {
            var data = new JObject { ["id"] = "group", ["name"] = "" };
            if (shape == "null") { data["admins"] = JValue.CreateNull(); data["users"] = JValue.CreateNull(); data["color"] = JValue.CreateNull(); data["type"] = JValue.CreateNull(); }
            if (shape == "empty") { data["admins"] = new JArray(); data["users"] = new JArray(); }
            var result = await new Client(new State { Json = data.ToString() }).GetGroupDetailsAsync("context", "group");
            Assert.That(result.Color, Is.Null); Assert.That(result.Type, Is.Null); Assert.That(result.Name, Is.Empty);
            if (shape == "empty") { Assert.That(result.Admins, Is.Empty); Assert.That(result.Users, Is.Empty); } else { Assert.That(result.Admins, Is.Null); Assert.That(result.Users, Is.Null); }
        }

        [Test]
        public async Task FutureTypeAndPrivilegeNamesAndSpecialAllUsersIdentityRemainOriginal()
        {
            var data = Record("ALL_USERS_tenant"); data["type"] = "future-type"; data["admins"][0]["privilege"] = "future-privilege";
            var result = await new Client(new State { Json = data.ToString() }).GetGroupDetailsAsync("context", "ALL_USERS_tenant");
            Assert.That(result.Type, Is.EqualTo("future-type")); Assert.That(result.Admins[0].Privilege, Is.EqualTo("future-privilege")); Assert.That(result.Id, Is.EqualTo("ALL_USERS_tenant"));
        }

        [TestCase(false, null), TestCase(true, null), TestCase(false, GroupsFilter.MembershipsOfCurrentUser), TestCase(true, GroupsFilter.AllVisibleGroupsForCurrentUser)]
        public async Task ListPreservesExplicitVersusOmittedFilterAndServerOrder(bool sync, GroupsFilter? filter)
        {
            var state = new State { Json = new JObject { ["groups"] = new JArray(Record("second"), Record()) }.ToString() }; var client = new ListClient(state);
            var result = sync ? client.GetGroupsDetails("context", filter) : await client.GetGroupsDetailsAsync("context", filter);
            Assert.That(result.Select(item => item.Id), Is.EqualTo(new[] { "second", "group" })); Assert.That(((IList<GroupDetails>)result).IsReadOnly, Is.True);
            var query = state.Request.Parameters.Where(parameter => parameter.Type == ParameterType.QueryString).ToArray(); Assert.That(query.Length, Is.EqualTo(filter.HasValue ? 1 : 0));
            if (filter.HasValue) Assert.That(query[0].Value, Is.EqualTo(filter == GroupsFilter.AllVisibleGroupsForCurrentUser ? "true" : "false"));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/groups")); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
        }

        [TestCase(false), TestCase(true)]
        public async Task ConfirmedNoContentAndExplicitEmptyListRemainEmpty(bool noContent)
        {
            var state = new State { Status = noContent ? HttpStatusCode.NoContent : HttpStatusCode.OK, Json = noContent ? null : "{\"groups\":[]}" };
            Assert.That(await new ListClient(state).GetGroupsDetailsAsync("context"), Is.Empty);
        }

        [TestCase("id"), TestCase("name"), TestCase("color"), TestCase("type"), TestCase("users"), TestCase("admins"), TestCase("admin-id"), TestCase("privilege"), TestCase("duplicate-member"), TestCase("duplicate-admin")]
        public void MalformedSelectedMetadataRetainsOriginalCause(string problem)
        {
            var data = Record(); if (problem == "id") data["id"] = "different"; else if (problem == "admin-id") data["admins"][0]["user-id"] = null;
            else if (problem == "privilege") data["admins"][0]["privilege"] = 1; else if (problem == "duplicate-member") data["users"] = new JArray("01234567-89ab-cdef-0123-456789abcdef", "{01234567-89AB-CDEF-0123-456789ABCDEF}");
            else if (problem == "duplicate-admin") ((JArray)data["admins"]).Add(data["admins"][0].DeepClone()); else data[problem] = problem == "color" ? (JToken)new JValue("red") : new JValue(1);
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetGroupDetailsAsync("context", "group")));
            Assert.That(error.InnerException, Is.Not.Null);
        }

        [TestCase("2147483648"), TestCase("1.5"), TestCase("\"1\"")]
        public void ColorIsAnOriginalInt32Code(string color)
        {
            var data = Record(); data["color"] = JToken.Parse(color);
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(new State { Json = data.ToString() }).GetGroupDetailsAsync("context", "group")));
        }

        [TestCase("{}"), TestCase("[]"), TestCase("null"), TestCase("{\"groups\":null}"), TestCase("{\"groups\":[1]}"), TestCase("{\"groups\":[]} {}")]
        public void InvalidListEnvelopeDoesNotBecomeEmpty(string json)
        { Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new ListClient(new State { Json = json }).GetGroupsDetailsAsync("context"))); }

        [Test]
        public async Task CanonicalSelectedIdentityRemainsOriginalAndRepeatedListIdentityFails()
        {
            var original = "{01234567-89AB-CDEF-0123-456789ABCDEF}"; var data = Record(original);
            Assert.That((await new Client(new State { Json = data.ToString() }).GetGroupDetailsAsync("context", "01234567-89ab-cdef-0123-456789abcdef")).Id, Is.EqualTo(original));
            var state = new State { Json = new JObject { ["groups"] = new JArray(data, Record("01234567-89ab-cdef-0123-456789abcdef")) }.ToString() };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new ListClient(state).GetGroupsDetailsAsync("context")));
        }

        [TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectionAndUnknownFilterRejectBeforeAuthorization(string id)
        {
            var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetGroupDetailsAsync("context", id)));
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new ListClient(state).GetGroupsDetailsAsync("context", (GroupsFilter)99))); Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void ServerVisibilityFailuresRetainOriginalCauseRatherThanEmptyMetadata(int status)
        {
            var cause = new InvalidOperationException("original cause"); var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new ListClient(state).GetGroupsDetailsAsync("context")));
            Assert.That(error.InnerException, Is.SameAs(cause));
        }

        [TestCase(false), TestCase(true)]
        public async Task PreAndActiveCancellationReachAuthorizationAndNativeIo(bool list)
        {
            Func<State, CancellationToken, Task> invoke = (state, token) => list ? (Task)new ListClient(state).GetGroupsDetailsAsync("context", cancellationToken: token) : new Client(state).GetGroupDetailsAsync("context", "group", token);
            using (var cancellation = new CancellationTokenSource())
            {
                var state = new State { Pending = true }; var pending = invoke(state, cancellation.Token); await state.Entered.Task; cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                state = new State(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await invoke(state, cancellation.Token))); Assert.That(state.Authorizations, Is.Zero);
            }
        }
        [TestCase("get", 200), TestCase("get", 503), TestCase("list", 200), TestCase("list", 204), TestCase("list", 503)]
        public async Task PublicFactoryVerifiesRealWireAndPreservesGetRetriesWithoutMutationReplay(string operation, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string header = null, sent = null; var requests = 0;
                var expectedRequests = status == 503 ? 3 : 1;
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
                        var response = status == 503 ? "{}" : operation == "create" ? "{\"id\":\"created\"}" : operation == "get" ? Record().ToString() : operation == "list" ? new JObject { ["groups"] = new JArray(Record()) }.ToString() : "";
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
                    var client = factory.Group; client.DisableOfflineModeSimulation(); var listClient = factory.Groups; listClient.DisableOfflineModeSimulation();
                    Func<Task> invoke = async () =>
                    {
                        if (operation == "get") Assert.That((await client.GetGroupDetailsAsync("context", "group", cancellation.Token)).Name, Is.EqualTo("ä group"));
                        else Assert.That((await listClient.GetGroupsDetailsAsync("context", GroupsFilter.AllVisibleGroupsForCurrentUser, cancellation.Token)).Count, Is.EqualTo(status == 204 ? 0 : 1));
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    var target = operation == "get" ? "GET /bridge/v2/group/group" : "GET /bridge/v2/groups?all=true";
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo(target + " HTTP/1.1"));
                    Assert.That(sent, Is.Empty);
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
        private sealed class Client : GroupRestClient
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
        private sealed class ListClient : GroupsRestClient
        {
            private readonly State state;
            internal ListClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
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
            public string UserAgent => "isolated-group-details-tests";
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
