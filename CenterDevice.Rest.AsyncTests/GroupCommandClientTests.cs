using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Groups;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class GroupCommandClientTests
    {
        [TestCase(false), TestCase(true)]
        public async Task CreateSnapshotsNestedPrivateAdminsAndPreservesDefaultOmissions(bool sync)
        {
            var input = new GroupCreateRequest { Name = "private", Type = "private", Color = 0, Users = new List<string>(),
                Admins = new List<GroupAdministrator> { new GroupAdministrator { UserId = "manager", Privilege = "group-management" } } };
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}", BeforeAuthorize = () => { input.Admins[0].Privilege = "group-membership"; input.Name = "changed"; } };
            var client = new GroupsClient(state);
            var result = sync ? client.CreateGroup("context", input) : await client.CreateGroupAsync("context", input);
            Assert.That(result.CreatedGroupId, Is.EqualTo("created"));
            Assert.That(result.AllSucceeded, Is.True);
            Assert.That(Body(state).Value<string>("name"), Is.EqualTo("private"));
            Assert.That(Body(state).Value<int>("color"), Is.Zero);
            Assert.That(Body(state)["admins"][0].Value<string>("privilege"), Is.EqualTo("group-management"));
            Assert.That(Body(state)["users"], Is.TypeOf<JArray>().And.Empty);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/groups"));
            Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(((IList<string>)result.FailedUsers).IsReadOnly, Is.True);
        }

        [TestCase(false), TestCase(true)]
        public async Task PartialCreationRetainsLocationIdentityAndExactFailures(bool sync)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"failed-users\":[\"second\"]}", Location = "https://unused.invalid/bridge/v2/group/created" };
            var client = new GroupsClient(state);
            var input = new GroupCreateRequest { Name = "public", Users = new List<string> { "first", "second" } };
            var result = sync ? client.CreateGroup("context", input) : await client.CreateGroupAsync("context", input);
            Assert.That(result.CreatedGroupId, Is.EqualTo("created"));
            Assert.That(result.AllSucceeded, Is.False);
            Assert.That(result.FailedUsers, Is.EqualTo(new[] { "second" }));
            Assert.That(Body(state)["type"], Is.Null);
            Assert.That(Body(state)["color"], Is.Null);
            Assert.That(Body(state)["admins"], Is.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("https://other.invalid/bridge/v2/group/created")]
        [TestCase("https://unused.invalid/bridge/v2/document/created")]
        [TestCase("https://unused.invalid/bridge/v2/group/")]
        [TestCase("https://unused.invalid/bridge/v2/group/other")]
        [TestCase("https://unused.invalid/bridge/v2/group/created?x=1")]
        [TestCase("https://unused.invalid/bridge/v2/group/created#fragment")]
        [TestCase("https://user@unused.invalid/bridge/v2/group/created")]
        [TestCase("https://unused.invalid/bridge/v2/group/a%2Fcreated")]
        public void CreationRejectsForeignOrMismatchedLocationWithoutReplaying(string location)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}", Location = location };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new GroupsClient(state).CreateGroupAsync("context", new GroupCreateRequest { Name = "name" }); }));
            Assert.That(error.InnerException, Is.Not.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("group/created"), TestCase("/bridge/v2/group/created")]
        public async Task PartialCreationResolvesRelativeLocationAgainstTheActualCreationEndpoint(string location)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = "{\"failed-users\":[\"selected\"]}", Location = location };
            var result = await new GroupsClient(state).CreateGroupAsync("context", new GroupCreateRequest { Name = "name", Users = new List<string> { "selected" } });
            Assert.That(result.CreatedGroupId, Is.EqualTo("created"));
        }

        [TestCase("add", false), TestCase("add", true), TestCase("remove", false), TestCase("remove", true)]
        public async Task SelectedMembershipCommandsSnapshotAndReportPartialFailure(string operation, bool sync)
        {
            var users = new List<string> { "one", "two" };
            var state = new State { Status = HttpStatusCode.OK, Json = "{\"failed-users\":[\"two\"]}", BeforeAuthorize = () => users.Clear() };
            var client = new GroupClient(state);
            GroupCommandResult result;
            if (operation == "add") result = sync ? client.AddUsers("context", "id with/slash", users) : await client.AddUsersAsync("context", "id with/slash", users);
            else result = sync ? client.RemoveUsers("context", "id with/slash", users) : await client.RemoveUsersAsync("context", "id with/slash", users);
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo(operation == "add" ? "add-users" : "remove-users"));
            Assert.That(Body(state)["params"]["users"].Values<string>(), Is.EqualTo(new[] { "one", "two" }));
            Assert.That(state.Request.Resource, Is.EqualTo("v2/group/id%20with%2Fslash"));
            Assert.That(result.FailedUsers, Is.EqualTo(new[] { "two" }));
            Assert.That(result.CreatedGroupId, Is.Null);
        }

        [TestCase(false), TestCase(true)]
        public async Task UpdateClearsExplicitEmptyMembershipButPreservesOmittedNameColor(bool sync)
        {
            var state = new State();
            var input = new GroupUpdateRequest { Users = new List<string>() };
            var client = new GroupClient(state);
            var result = sync ? client.UpdateGroup("context", "group", input) : await client.UpdateGroupAsync("context", "group", input);
            Assert.That(result.AllSucceeded, Is.True);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Put));
            Assert.That(Body(state).Properties().Select(property => property.Name), Is.EqualTo(new[] { "users" }));
            Assert.That(Body(state)["users"], Is.TypeOf<JArray>().And.Empty);
        }

        [TestCase("add", false), TestCase("add", true), TestCase("privileges", false), TestCase("privileges", true), TestCase("remove", false), TestCase("remove", true)]
        public async Task AdministratorCommandsKeepSeparateWireShapesAndSnapshotPrivileges(string operation, bool sync)
        {
            var input = new List<GroupAdministrator> { new GroupAdministrator { UserId = "admin", Privilege = "group-management" } };
            var state = new State { BeforeAuthorize = () => input[0].Privilege = "group-membership" };
            var client = new GroupClient(state);
            if (operation == "add") { if (sync) client.AddAdmins("context", "group", input); else await client.AddAdminsAsync("context", "group", input); }
            else if (operation == "privileges") { if (sync) client.UpdateAdminPrivileges("context", "group", input); else await client.UpdateAdminPrivilegesAsync("context", "group", input); }
            else { if (sync) client.RemoveAdmins("context", "group", new[] { "admin" }); else await client.RemoveAdminsAsync("context", "group", new[] { "admin" }); }
            Assert.That(Body(state).Value<string>("action"), Is.EqualTo(operation == "add" ? "add-admins" : operation == "privileges" ? "update-admin-privileges" : "remove-admins"));
            if (operation == "remove") Assert.That(Body(state)["params"]["admins"].Values<string>(), Is.EqualTo(new[] { "admin" }));
            else { Assert.That(Body(state)["params"]["admins"][0].Value<string>("user-id"), Is.EqualTo("admin")); Assert.That(Body(state)["params"]["admins"][0].Value<string>("privilege"), Is.EqualTo("group-management")); }
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task RemoveGroupUsesBodylessDeleteWithoutDeletingDocuments(bool sync)
        {
            var state = new State(); var client = new GroupClient(state);
            if (sync) client.RemoveGroup("context", "group"); else await client.RemoveGroupAsync("context", "group");
            Assert.That(state.Request.Method, Is.EqualTo(Method.Delete));
            Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/group/group"));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("{}"), TestCase("null"), TestCase("[]"), TestCase("{\"failed-users\":[]}"), TestCase("{\"failed-users\":[\"foreign\"]}"), TestCase("{\"failed-users\":[null]}"), TestCase("{\"failed-users\":[1]}"), TestCase("{\"failed-users\":[\"one\",\"one\"]}"), TestCase("{\"failed-users\":[\"one\"]} {}")]
        public void PartialFailuresRejectInvalidRepeatedOrForeignIdentities(string json)
        {
            var state = new State { Status = HttpStatusCode.OK, Json = json };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new GroupClient(state).AddUsersAsync("context", "group", new[] { "one" }); }));
            Assert.That(error.InnerException, Is.Not.Null);
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("{}"), TestCase("{\"id\":null}"), TestCase("{\"id\":1}"), TestCase("{\"id\":\" \"}"), TestCase("{\"failed-users\":[\"selected\"]}"), TestCase("{\"id\":\"created\",\"failed-users\":[\"foreign\"]}")]
        public void CreationWithoutConfirmedIdentityOrWithForeignFailuresCannotBeClaimedSuccessful(string json)
        {
            var state = new State { Status = HttpStatusCode.Created, Json = json };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await new GroupsClient(state).CreateGroupAsync("context", new GroupCreateRequest { Name = "name", Users = new List<string> { "selected" } }); }));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("name"), TestCase("type"), TestCase("private-admin"), TestCase("public-admin"), TestCase("null-user")]
        public void InvalidCreateOptionsRejectBeforeAuthorization(string problem)
        {
            var input = new GroupCreateRequest { Name = problem == "name" ? " " : "name", Type = problem == "type" ? "unknown" : problem == "private-admin" ? "private" : null,
                Admins = problem == "public-admin" ? new List<GroupAdministrator> { new GroupAdministrator { UserId = "user", Privilege = "group-management" } } : null,
                Users = problem == "null-user" ? new List<string> { null } : null };
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new GroupsClient(state).CreateGroupAsync("context", input); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase("null"), TestCase("empty"), TestCase("blank"), TestCase("group")]
        public void InvalidSelectionsRejectBeforeAuthorization(string problem)
        {
            var users = problem == "null" ? null : problem == "empty" ? new string[0] : new[] { problem == "blank" ? " " : "user" };
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new GroupClient(state).RemoveUsersAsync("context", problem == "group" ? " " : "group", users); }));
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("null"), TestCase("empty"), TestCase("blank"), TestCase("privilege"), TestCase("conflict")]
        public void InvalidAdministratorSelectionsRejectBeforeAuthorization(string problem)
        {
            var admins = problem == "null" ? null : problem == "empty" ? new GroupAdministrator[0] : new[] { new GroupAdministrator { UserId = problem == "blank" ? " " : "admin", Privilege = problem == "privilege" ? "owner" : "group-management" } };
            if (problem == "conflict") admins = new[] { admins[0], new GroupAdministrator { UserId = "admin", Privilege = "group-membership" } };
            var state = new State();
            Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => { await new GroupClient(state).AddAdminsAsync("context", "group", admins); }));
            Assert.That(state.Authorizations, Is.Zero);
        }

        [TestCase(403), TestCase(404), TestCase(503)]
        public void ServerPrivilegeStateAndUncertainFailuresRetainCauseWithoutReplay(int status)
        {
            var cause = new InvalidOperationException("isolated failure");
            var state = new State { Status = (HttpStatusCode)status, Error = cause };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await new GroupClient(state).RemoveAdminsAsync("context", "group", new[] { "admin" }); }));
            Assert.That(error.InnerException, Is.SameAs(cause));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task CancellationReachesAuthorizationAndNativeHttp()
        {
            var state = new State { Pending = true };
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await new GroupsClient(state).CreateGroupAsync("context", new GroupCreateRequest { Name = "name" }, token.Token); }));
                Assert.That(state.Authorizations, Is.Zero);
            }
            using (var token = new CancellationTokenSource())
            {
                var pending = new GroupClient(state).RemoveGroupAsync("context", "group", token.Token);
                await state.Entered.Task;
                Assert.That(state.Token, Is.EqualTo(token.Token));
                Assert.That(state.AuthorizationToken, Is.EqualTo(token.Token));
                token.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await pending; }));
            }
        }

        [TestCase("create", 201), TestCase("create-partial", 201), TestCase("create", 503), TestCase("update", 200), TestCase("delete", 503), TestCase("admins", 204)]
        public async Task ActualPublicFactoryRetainsNestedJsonAndNeverReplaysUncertainMutations(string operation, int status)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                string header = null, sent = null;
                var server = Task.Run(async () =>
                {
                    using (var socket = await listener.AcceptTcpClientAsync())
                    using (var stream = socket.GetStream())
                    {
                        var bytes = new List<byte>(); var one = new byte[1];
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
                        var response = status == 503 ? "{}" : operation == "create" ? "{\"id\":\"created\"}" : operation == "create-partial" || operation == "update" ? "{\"failed-users\":[\"selected\"]}" : "";
                        var payload = Encoding.UTF8.GetBytes(response);
                        var reply = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + (status == 201 ? " Created" : status == 200 ? " OK" : status == 204 ? " No Content" : " Service Unavailable") +
                            (operation == "create-partial" ? "\r\nLocation: group/created" : "") + "\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(reply, 0, reply.Length, cancellation.Token);
                        if (payload.Length != 0) await stream.WriteAsync(payload, 0, payload.Length, cancellation.Token);
                    }
                });
                try
                {
                    var factory = new Factory(new State(), new Config("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/bridge/"));
                    Func<Task> invoke = async () =>
                    {
                        if (operation.StartsWith("create"))
                        {
                            var client = factory.Groups; client.DisableOfflineModeSimulation();
                            var result = await client.CreateGroupAsync("context", new GroupCreateRequest { Name = "ä group", Users = new List<string> { "selected" } }, cancellation.Token);
                            Assert.That(result.CreatedGroupId, Is.EqualTo("created"));
                            Assert.That(result.AllSucceeded, Is.EqualTo(operation == "create"));
                        }
                        else
                        {
                            var client = factory.Group; client.DisableOfflineModeSimulation();
                            if (operation == "update") Assert.That((await client.UpdateGroupAsync("context", "selected-group", new GroupUpdateRequest { Users = new List<string> { "selected" }, Type = "private" }, cancellation.Token)).FailedUsers, Is.EqualTo(new[] { "selected" }));
                            else if (operation == "delete") await client.RemoveGroupAsync("context", "selected-group", cancellation.Token);
                            else await client.AddAdminsAsync("context", "selected-group", new[] { new GroupAdministrator { UserId = "selected", Privilege = "group-membership" } }, cancellation.Token);
                        }
                    };
                    if (status == 503) Assert.CatchAsync<RestClientException>(invoke); else await invoke();
                    await server;
                    Assert.That(header.Split(new[] { "\r\n" }, StringSplitOptions.None)[0], Is.EqualTo((operation.StartsWith("create") ? "POST /bridge/v2/groups" : operation == "update" ? "PUT /bridge/v2/group/selected-group" : operation == "delete" ? "DELETE /bridge/v2/group/selected-group" : "POST /bridge/v2/group/selected-group") + " HTTP/1.1"));
                    if (operation == "delete") Assert.That(sent, Is.Empty);
                    else if (operation == "admins") Assert.That(JObject.Parse(sent)["params"]["admins"][0].Value<string>("privilege"), Is.EqualTo("group-membership"));
                    else if (operation == "update") Assert.That(JObject.Parse(sent).Value<string>("type"), Is.EqualTo("private"));
                    else Assert.That(JObject.Parse(sent).Value<string>("name"), Is.EqualTo("ä group"));
                    Assert.That(listener.Pending(), Is.False);
                }
                finally
                {
                    cancellation.Cancel(); listener.Stop();
                    try { await server; } catch (SocketException) { } catch (ObjectDisposedException) { } catch (OperationCanceledException) { }
                }
            }
        }

        private sealed class Factory : CenterDeviceClientBase
        {
            internal Factory(State state, Config config) : base(state, config, null, "v2/") { }
            protected override string UploadLinkBaseUrl => "https://unused.invalid/";
        }

        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class GroupClient : GroupRestClient
        {
            private readonly State state;
            internal GroupClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class GroupsClient : GroupsRestClient
        {
            private readonly State state;
            internal GroupsClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token);
        }
        private sealed class Config : IRestClientConfiguration
        {
            internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; }
            public string BaseAddress { get; }
            public string UserAgent => "isolated-group-command-tests";
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal string Json, Context, Location;
            internal RestRequest Request;
            internal HttpStatusCode Status = HttpStatusCode.NoContent;
            internal Exception Error;
            internal Action BeforeAuthorize;
            internal bool Pending;
            internal int Calls, Authorizations;
            internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token)
            {
                Calls++; Request = request; Token = token; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse(request) { StatusCode = Status, Content = Json, ErrorException = Error,
                    Headers = Location == null ? null : new[] { new HeaderParameter("Location", Location) } };
            }
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken))
            {
                Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
        }
    }
}
