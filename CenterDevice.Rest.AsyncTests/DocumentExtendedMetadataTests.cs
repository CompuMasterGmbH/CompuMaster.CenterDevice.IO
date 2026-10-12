using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class DocumentExtendedMetadataTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OriginalTypedSectionsPathsAndDatesAreCopiedWithoutChangingShares(bool sync)
        {
            var set = new JObject { ["user.dates.original"] = "2099-12-31T23:59:00+02:00", ["user.numbers.amount"] = "001.2500", ["user.strings.tags"] = new JArray("a", "a"), ["photo"] = new JObject { ["value"] = "value", ["dates"] = new JObject { ["created"] = "2099-12-31" }, ["numbers"] = new JObject { ["number"] = "001.2500" }, ["details"] = new JObject { ["people"] = new JArray("a", "a") } } };
            var update = new DocumentMetadataUpdateRequest { Set = set, Unset = new[] { "old", "user.strings.old", "user.strings.old" } }; set["user.numbers.amount"] = "changed"; update.Set["user.numbers.amount"] = "changed";
            var state = new State { BeforeAuthorize = () => update["set"]["user.numbers.amount"] = "changed" }; if (sync) new Client(state).UpdateExtendedMetadata("context", "part/with?query", update); else await new Client(state).UpdateExtendedMetadataAsync("context", "part/with?query", update);
            var body = Body(state); Assert.That(state.Request.Resource, Is.EqualTo("v2/document/part%2Fwith%3Fquery")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(body.Value<string>("action"), Is.EqualTo("set-extended-metadata")); Assert.That(body["params"]["set"].Value<string>("user.dates.original"), Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(body["params"]["set"].Value<string>("user.numbers.amount"), Is.EqualTo("001.2500")); Assert.That(body["params"]["set"]["photo"]["numbers"].Value<string>("number"), Is.EqualTo("001.2500")); Assert.That(body["params"]["unset"].Values<string>(), Is.EqualTo(new[] { "old", "user.strings.old", "user.strings.old" })); Assert.That(((JObject)body["params"]).Property("share"), Is.Null); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
        }
        [TestCase("user", "{\"value\":[],\"dates\":{},\"numbers\":{},\"strings\":{}}"), TestCase("user.value", "\"\""), TestCase("user.strings", "{\"empty\":[]}"), TestCase("user.dates", "{\"original\":\"2099-12-31T23:59:00+02:00\"}"), TestCase("user.numbers", "{\"original\":\"001.2500\"}"), TestCase("user.strings.empty", "[]"), TestCase("Common.value", "\"case-sensitive\"")]
        public async Task WholeNamespaceSectionAndIndividualFieldShapesRemainExplicit(string path, string json)
        { var state = new State(); await new Client(state).UpdateExtendedMetadataAsync("context", "selected", new DocumentMetadataUpdateRequest { Set = new JObject { [path] = OriginalJson(json) } }); Assert.That(((JObject)Body(state)["params"]["set"]).Property(path), Is.Not.Null); }
        [Test]
        public async Task DeprecatedNamespacesAndOverlappingSelectionsDoNotInventPrecedence()
        {
#pragma warning disable CS0618
            var update = new DocumentMetadataUpdateRequest { Set = new JObject { ["user.value"] = "selected" }, Unset = new[] { "user.value" }, Metadata = new JObject { ["photo"] = new JObject { ["value"] = "legacy" } } };
#pragma warning restore CS0618
            update["future"] = false; var state = new State(); await new Client(state).UpdateExtendedMetadataAsync("context", "selected", update); Assert.That(Body(state)["params"]["metadata"]["photo"].Value<string>("value"), Is.EqualTo("legacy")); Assert.That(Body(state)["params"].Value<bool>("future"), Is.False); Assert.That(Body(state)["params"]["unset"].Values<string>(), Is.EqualTo(new[] { "user.value" }));
        }
        [Test]
        public async Task TypedNullOmissionDoesNotImplicitlyClearMetadata()
        { var update = new DocumentMetadataUpdateRequest { Set = new JObject(), Unset = new[] { "user.value" } }; update.Set = null; update.Unset = null; var state = new State(); await new Client(state).UpdateExtendedMetadataAsync("context", "selected", update); Assert.That(((JObject)Body(state)["params"]).Properties(), Is.Empty); }
        [TestCase("{\"set\":null}"), TestCase("{\"unset\":null}"), TestCase("{\"metadata\":null}"), TestCase("{\"set\":{\"user\":\"invalid\"}}"), TestCase("{\"set\":{\"user.value\":null}}"), TestCase("{\"set\":{\"user.value\":[false]}}"), TestCase("{\"set\":{\"user.dates.original\":5}}"), TestCase("{\"set\":{\"user.numbers.original\":1.25}}"), TestCase("{\"set\":{\"user.strings.original\":{}}}"), TestCase("{\"set\":{\"user.strings\":{\"a.b\":\"invalid\"}}}"), TestCase("{\"set\":{\"user.numbers\":{\"number\":[]}}}"), TestCase("{\"set\":{\"user.$invalid\":{}}}"), TestCase("{\"unset\":[\"user..field\"]}"), TestCase("{\"unset\":[\"user.value.nested\"]}"), TestCase("{\"unset\":[\"user.custom.field.extra\"]}"), TestCase("{\"unset\":[0]}"), TestCase("{\"metadata\":{\"user.strings\":{}}}")]
        public void UnsupportedKnownStructuresDoNotAuthorizeOrLoseTheCallerSelection(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", "selected", new DocumentMetadataUpdateRequest(JObject.Parse(json))))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("{\"set\":{\"common\":{}}}"), TestCase("{\"set\":{\"common.value\":\"no\"}}"), TestCase("{\"unset\":[\"common.dates.created\"]}"), TestCase("{\"metadata\":{\"common\":{}}}")]
        public void ReadOnlyCommonNamespaceCannotBeWritten(string json)
        { var state = new State(); Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", "selected", new DocumentMetadataUpdateRequest(JObject.Parse(json))))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectedDocumentDoesNotAuthorize(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", id, new DocumentMetadataUpdateRequest()))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(200), TestCase(403), TestCase(409), TestCase(500)]
        public void ServerFailuresDoNotInferTemplatesOrPermissionsOrReplay(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", "selected", new DocumentMetadataUpdateRequest { Set = new JObject { ["user.value"] = "value" } }))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [Test]
        public void NullUpdateDoesNotAuthorize()
        { var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", "selected", null))); Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public void PreCancellationDoesNotAuthorize()
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).UpdateExtendedMetadataAsync("context", "selected", new DocumentMetadataUpdateRequest(), source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttp()
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var task = new Client(state).UpdateExtendedMetadataAsync("context", "selected", new DocumentMetadataUpdateRequest(), source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        private static JToken OriginalJson(string json) { using (var source = new StringReader(json)) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None }) return JToken.ReadFrom(reader); }
        private static JObject Body(State state) { using (var source = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value)) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-document-metadata-update-tests"; }
        private sealed class Client : DocumentRestClient { private readonly State state; internal Client(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
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
