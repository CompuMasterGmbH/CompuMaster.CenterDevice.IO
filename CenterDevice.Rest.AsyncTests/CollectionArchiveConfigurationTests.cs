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
    public class CollectionArchiveConfigurationTests
    {
        [TestCase("date", false), TestCase("date", true), TestCase("period", false), TestCase("period", true)]
        [TestCase("clear-expiry", false), TestCase("clear-expiry", true), TestCase("source", false), TestCase("source", true)]
        [TestCase("clear-source", false), TestCase("clear-source", true), TestCase("freeze", false), TestCase("freeze", true), TestCase("unfreeze", false), TestCase("unfreeze", true)]
        public async Task OriginalArchiveActionsAndExplicitParameterShapesRemainUnchanged(string operation, bool sync)
        {
            var state = new State(); await Invoke(state, operation, sync, "part/with?query"); var body = Body(state);
            Assert.That(body.Value<string>("action"), Is.EqualTo(Action(operation))); Assert.That(state.Request.Resource, Is.EqualTo("v2/collection/part%2Fwith%3Fquery")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
            var parameters = (JObject)body["params"];
            if (operation.StartsWith("clear")) Assert.That(parameters.Properties(), Is.Empty);
            else if (operation == "date") { Assert.That(parameters.Value<string>("date"), Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(parameters.Value<string>("action"), Is.EqualTo("no-action")); Assert.That(parameters.Property("period"), Is.Null); }
            else if (operation == "period") { Assert.That(parameters["period"].Value<string>("unit"), Is.EqualTo("years")); Assert.That(parameters["period"].Value<long>("amount"), Is.EqualTo(4294967297L)); Assert.That(((JObject)parameters["period"]).Property("calendar"), Is.Null); }
            else if (operation == "source") Assert.That(parameters.Value<string>("key"), Is.EqualTo("user.dates.original field"));
            else Assert.That(parameters.Value<bool>("freeze"), Is.EqualTo(operation == "freeze"));
        }

        [TestCase("no-action"), TestCase("remove-from-collection"), TestCase("move-to-trash"), TestCase("delete"), TestCase("future-action")]
        public async Task ExpiryActionsAreExplicitAndNotNormalizedOrSelectedByTheClient(string action)
        {
            var state = new State(); await new Client(state).SetArchiveExpiryAsync("context", "selected", new CollectionArchiveExpiryRequest { Date = "2099-12-31", Action = action });
            Assert.That(Body(state)["params"].Value<string>("action"), Is.EqualTo(action)); Assert.That(Body(state)["params"].Value<string>("date"), Is.EqualTo("2099-12-31"));
        }

        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task CalendarChoiceDistinguishesOmissionAndExplicitFalseOrTrue(bool? calendar)
        {
            var state = new State(); await new Client(state).SetArchiveExpiryAsync("context", "selected", new CollectionArchiveExpiryRequest { Period = new CollectionArchiveExpiryPeriod { Unit = "months", Amount = 1, Calendar = calendar }, Action = "no-action" });
            var period = (JObject)Body(state)["params"]["period"]; Assert.That(period.Property("calendar") != null, Is.EqualTo(calendar.HasValue)); if (calendar.HasValue) Assert.That(period.Value<bool>("calendar"), Is.EqualTo(calendar.Value));
        }

        [Test]
        public async Task ExpiryAndNestedPeriodAreCopiedBeforeAuthorization()
        {
            var period = new CollectionArchiveExpiryPeriod { Unit = "years", Amount = 1, Calendar = false }; var expiry = new CollectionArchiveExpiryRequest { Period = period, Action = "no-action" };
            period.Amount = 2; var state = new State { BeforeAuthorize = () => { expiry["period"]["amount"] = 3; expiry.Action = "delete"; } };
            await new Client(state).SetArchiveExpiryAsync("context", "selected", expiry); Assert.That(Body(state)["params"]["period"].Value<int>("amount"), Is.EqualTo(1)); Assert.That(Body(state)["params"].Value<string>("action"), Is.EqualTo("no-action"));
        }

        [Test]
        public async Task RawLargePositiveIntegersAndFutureFieldsAreNotNarrowedOrConverted()
        {
            var request = new CollectionArchiveExpiryRequest(JObject.Parse("{\"period\":{\"unit\":\"future-unit\",\"amount\":9223372036854775808},\"action\":\"no-action\",\"future\":\"2099-12-31\"}")); var state = new State();
            await new Client(state).SetArchiveExpiryAsync("context", "selected", request); Assert.That(Body(state)["params"]["period"]["amount"].ToString(), Is.EqualTo("9223372036854775808")); Assert.That(Body(state)["params"]["future"].Type, Is.EqualTo(JTokenType.String));
        }

        [TestCase("{}"), TestCase("{\"date\":\"2099-12-31\"}"), TestCase("{\"date\":null,\"action\":\"no-action\"}"), TestCase("{\"date\":\" \",\"action\":\"no-action\"}")]
        [TestCase("{\"date\":\"2099-12-31\",\"period\":{},\"action\":\"no-action\"}"), TestCase("{\"period\":null,\"action\":\"no-action\"}"), TestCase("{\"period\":{\"unit\":\"years\"},\"action\":\"no-action\"}")]
        [TestCase("{\"period\":{\"unit\":\"years\",\"amount\":0},\"action\":\"no-action\"}"), TestCase("{\"period\":{\"unit\":\"years\",\"amount\":-1},\"action\":\"no-action\"}"), TestCase("{\"period\":{\"unit\":\"years\",\"amount\":1.5},\"action\":\"no-action\"}")]
        [TestCase("{\"period\":{\"unit\":\"years\",\"amount\":1,\"calendar\":null},\"action\":\"no-action\"}"), TestCase("{\"date\":\"2099-12-31\",\"action\":0}")]
        public void InvalidOrAmbiguousExpiryCannotImplicitlyRemoveOrGuessAnAction(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).SetArchiveExpiryAsync("context", "selected", new CollectionArchiveExpiryRequest(JObject.Parse(json))))); Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [Test]
        public void NullExpiryIsNotAnImplicitRemoval()
        { var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => await new Client(state).SetArchiveExpiryAsync("context", "selected", null))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase(null), TestCase(""), TestCase(" ")]
        public void UnusableSourceKeysAreNotImplicitRemoval(string key)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).SetArchivedDateSourceAsync("context", "selected", key))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase("date"), TestCase("period"), TestCase("clear-expiry"), TestCase("source"), TestCase("clear-source"), TestCase("freeze"), TestCase("unfreeze")]
        public void InvalidSelectedCollectionFailsBeforeAuthorization(string operation)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await Invoke(state, operation, id: ".."))); Assert.That(state.Authorizations, Is.Zero); }

        [TestCase("date", 200), TestCase("period", 403), TestCase("clear-expiry", 409), TestCase("source", 403), TestCase("clear-source", 500), TestCase("freeze", 403), TestCase("unfreeze", 403)]
        public void UnexpectedStatusesDoNotRepairPermissionsOrReplayArchiveChanges(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation))); Assert.That(state.Calls, Is.EqualTo(1)); }

        [TestCase("date"), TestCase("period"), TestCase("clear-expiry"), TestCase("source"), TestCase("clear-source"), TestCase("freeze"), TestCase("unfreeze")]
        public void PreCancellationDoesNotAuthorizeOrSend(string operation)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, token: source.Token))); } Assert.That(state.Authorizations, Is.Zero); Assert.That(state.Calls, Is.Zero); }

        [TestCase("date"), TestCase("period"), TestCase("clear-expiry"), TestCase("source"), TestCase("clear-source"), TestCase("freeze"), TestCase("unfreeze")]
        public async Task ActiveCancellationReachesNativeAuthorizationAndHttp(string operation)
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var pending = Invoke(state, operation, token: source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await pending)); } }

        [TestCase("date"), TestCase("period"), TestCase("clear-expiry"), TestCase("source"), TestCase("clear-source"), TestCase("freeze"), TestCase("unfreeze")]
        public async Task FactorySendsArchiveConfigurationOverPrivateTcpLoopback(string operation)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
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

        private static string Action(string operation) => operation == "source" || operation == "clear-source" ? "set-archived-date-source" : operation == "freeze" || operation == "unfreeze" ? "freeze-archive" : "set-expiry";
        private static Task Invoke(State state, string operation, bool sync = false, string id = "selected", CancellationToken token = default(CancellationToken)) => InvokeClient(new Client(state), operation, sync, id, token);
        private static async Task InvokeClient(CollectionRestClient client, string operation, bool sync, string id, CancellationToken token)
        {
            var expiry = operation == "period" ? new CollectionArchiveExpiryRequest { Period = new CollectionArchiveExpiryPeriod { Unit = "years", Amount = 4294967297L }, Action = "no-action" } : new CollectionArchiveExpiryRequest { Date = "2099-12-31T23:59:00+02:00", Action = "no-action" };
            if (operation == "date" || operation == "period") { if (sync) client.SetArchiveExpiry("context", id, expiry, token); else await client.SetArchiveExpiryAsync("context", id, expiry, token); }
            else if (operation == "clear-expiry") { if (sync) client.RemoveArchiveExpiry("context", id, token); else await client.RemoveArchiveExpiryAsync("context", id, token); }
            else if (operation == "source") { if (sync) client.SetArchivedDateSource("context", id, "user.dates.original field", token); else await client.SetArchivedDateSourceAsync("context", id, "user.dates.original field", token); }
            else if (operation == "clear-source") { if (sync) client.RemoveArchivedDateSource("context", id, token); else await client.RemoveArchivedDateSourceAsync("context", id, token); }
            else { if (sync) client.SetArchiveFrozen("context", id, operation == "freeze", token); else await client.SetArchiveFrozenAsync("context", id, operation == "freeze", token); }
        }
        private static JObject Body(State state)
        {
            using (var source = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value))
            using (var reader = new Newtonsoft.Json.JsonTextReader(source) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
                return JObject.Load(reader);
        }
        private sealed class Config : IRestClientConfiguration
        { internal Config(string address = "https://unused.invalid/bridge/") { BaseAddress = address; } public string BaseAddress { get; } public string UserAgent => "isolated-archive-config-tests"; }
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
