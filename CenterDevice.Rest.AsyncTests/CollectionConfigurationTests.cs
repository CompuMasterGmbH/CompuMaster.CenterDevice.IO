using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class CollectionConfigurationTests
    {
        [TestCase(false), TestCase(true)]
        public async Task AllOriginalCreationChoicesAndPartialFailuresArePreserved(bool sync)
        {
            var documents = new[] { "doc", "doc" }; var request = new CollectionCreationRequest { Name = " original ", Unique = false, Public = false, InheritedSharings = true, FullPath = false, Auditing = false, Archived = false, CheckForDuplicate = true, Documents = documents, Users = new[] { "u" }, Groups = new[] { "g" }, ExtendedMetadata = new JObject { ["date"] = "2099-12-31T23:59:00+02:00", ["array"] = new JArray("a", "a") } };
            documents[0] = "changed"; var state = new State { Content = "{\"id\":\"created\",\"failed-documents\":[\"doc\"],\"failed-users\":[\"u\"],\"failed-groups\":[\"g\"]}", BeforeAuthorize = () => { request.Name = "changed"; request.Public = true; request["extended-metadata"]["date"] = "changed"; } };
            var result = sync ? new CollectionsClient(state).CreateConfiguredCollection("context", request) : await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", request);
            var body = Body(state); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/collections")); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(body.Value<string>("name"), Is.EqualTo(" original ")); Assert.That(body.Value<bool>("unique"), Is.False); Assert.That(body.Value<bool>("public"), Is.False); Assert.That(body.Value<bool>("inherited-sharings"), Is.True); Assert.That(body.Value<bool>("full-path"), Is.False); Assert.That(body.Value<bool>("auditing"), Is.False); Assert.That(body.Value<bool>("archived"), Is.False); Assert.That(body.Value<bool>("check-for-duplicate"), Is.True);
            Assert.That(body["documents"].Values<string>(), Is.EqualTo(new[] { "doc", "doc" })); Assert.That(body["users"].Values<string>(), Is.EqualTo(new[] { "u" })); Assert.That(body["groups"].Values<string>(), Is.EqualTo(new[] { "g" })); Assert.That(body["extended-metadata"].Value<string>("date"), Is.EqualTo("2099-12-31T23:59:00+02:00"));
            Assert.That(result.Id, Is.EqualTo("created")); Assert.That(result.FailedDocuments, Is.EqualTo(new[] { "doc" })); Assert.That(result.FailedUsers, Is.EqualTo(new[] { "u" })); Assert.That(result.FailedGroups, Is.EqualTo(new[] { "g" }));
        }
        [Test]
        public async Task NameOnlyCreationKeepsDefaultsOmittedAndEmptyNameServerAuthoritative()
        { var state = new State(); var result = await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", new CollectionCreationRequest { Name = "" }); Assert.That(Body(state).Properties().Count(), Is.EqualTo(1)); Assert.That(Body(state).Value<string>("name"), Is.Empty); Assert.That(result.FailedDocuments, Is.Null); }
        [Test]
        public async Task IntelligentFiltersPreserveNestedNamedQueriesAndOriginalDatesWithoutNormalization()
        {
            var filters = new JObject { ["text"] = " original ", ["folders"] = new JObject { ["ids"] = new JArray("folder"), ["deep"] = false }, ["named"] = new JArray(new JObject { ["name"] = "future", ["params"] = new JObject { ["nested"] = true } }), ["time-from"] = "2099-12-31T23:59:00+02:00", ["size-from"] = 0, ["future"] = false };
            var request = new CollectionCreationRequest { Name = "intelligent", FilterParameters = filters }; filters["text"] = "changed"; request.FilterParameters["text"] = "changed";
            var state = new State(); await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", request); var body = Body(state);
            Assert.That(body.Property("public"), Is.Null); Assert.That(body.Property("documents"), Is.Null); Assert.That(body["filter-params"].Value<string>("text"), Is.EqualTo(" original ")); Assert.That(body["filter-params"]["named"][0]["params"].Value<bool>("nested"), Is.True); Assert.That(body["filter-params"].Value<string>("time-from"), Is.EqualTo("2099-12-31T23:59:00+02:00"));
        }
        [TestCase(false), TestCase(true)]
        public async Task UpdatesCopyExplicitFalseAndFiltersWithoutChangingOmittedSettings(bool sync)
        {
            var request = new CollectionUpdateRequest { Name = " new ", Unique = false, Public = false, InheritedSharings = false, FullPath = true, FilterParameters = new JObject { ["time-span"] = 60, ["future"] = "original" } };
            var state = new State { Status = HttpStatusCode.NoContent, BeforeAuthorize = () => request.Name = "changed" };
            if (sync) new CollectionClient(state).UpdateConfiguration("context", "part/with?query", request); else await new CollectionClient(state).UpdateConfigurationAsync("context", "part/with?query", request);
            var body = Body(state); Assert.That(state.Request.Method, Is.EqualTo(Method.Put)); Assert.That(state.Request.Resource, Is.EqualTo("v2/collection/part%2Fwith%3Fquery")); Assert.That(body.Value<string>("name"), Is.EqualTo(" new ")); Assert.That(body.Value<bool>("unique"), Is.False); Assert.That(body.Value<bool>("public"), Is.False); Assert.That(body.Value<bool>("inherited-sharings"), Is.False); Assert.That(body.Value<bool>("full-path"), Is.True); Assert.That(body["filter-params"].Value<int>("time-span"), Is.EqualTo(60)); Assert.That(body.Property("auditing"), Is.Null); Assert.That(body.Property("archived"), Is.Null);
        }
        [Test]
        public async Task EmptyUpdateAndTypedNullOmitChangesRatherThanFetchingCurrentState()
        { var request = new CollectionUpdateRequest { Name = "old", Public = true }; request.Name = null; request.Public = null; var state = new State { Status = HttpStatusCode.NoContent }; await new CollectionClient(state).UpdateConfigurationAsync("context", "selected", request); Assert.That(Body(state).Properties(), Is.Empty); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase("{}"), TestCase("{\"name\":null}"), TestCase("{\"name\":5}"), TestCase("{\"name\":\"new\",\"public\":null}"), TestCase("{\"name\":\"new\",\"unique\":0}"), TestCase("{\"name\":\"new\",\"documents\":[null]}"), TestCase("{\"name\":\"new\",\"users\":false}"), TestCase("{\"name\":\"new\",\"groups\":[\" \"]}"), TestCase("{\"name\":\"new\",\"filter-params\":null}"), TestCase("{\"name\":\"new\",\"filter-params\":{},\"public\":false}"), TestCase("{\"name\":\"new\",\"filter-params\":{},\"documents\":[]}"), TestCase("{\"name\":\"new\",\"extended-metadata\":{\"key\":null}}"), TestCase("{\"name\":\"new\",\"extended-metadata\":{\"key\":[0]}}")]
        public void InvalidKnownCreationStructureDoesNotAuthorize(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", new CollectionCreationRequest(JObject.Parse(json))))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(""), TestCase("{}"), TestCase("[]"), TestCase("{\"id\":null}"), TestCase("{\"id\":5}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":\"created\",\"failed-users\":[null]}"), TestCase("{\"id\":\"created\",\"failed-groups\":false}"), TestCase("{\"id\":\"created\",\"id\":\"other\"}"), TestCase("{\"id\":\"created\"}{}")]
        public void MalformedCreationSuccessRetainsItsInnerDiagnostic(string json)
        { var state = new State { Content = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", new CollectionCreationRequest { Name = "new" }))); Assert.That(error.InnerException, Is.Not.Null); }
        [TestCase("create", 403), TestCase("create", 409), TestCase("create", 500), TestCase("update", 200), TestCase("update", 403), TestCase("update", 409)]
        public void ServerFailuresDoNotInsertDefaultsOrReplayWrites(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase("create"), TestCase("update")]
        public void NullConfigurationDoesNotAuthorize(string operation)
        { var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => { if (operation == "create") await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", null); else await new CollectionClient(state).UpdateConfigurationAsync("context", "selected", null); })); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("create"), TestCase("update")]
        public void PreCancellationDoesNotAuthorize(string operation)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("create"), TestCase("update")]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttp(string operation)
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var task = Invoke(state, operation, source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        private static async Task Invoke(State state, string operation, CancellationToken token = default(CancellationToken)) { if (operation == "create") await new CollectionsClient(state).CreateConfiguredCollectionAsync("context", new CollectionCreationRequest { Name = "new" }, token); else await new CollectionClient(state).UpdateConfigurationAsync("context", "selected", new CollectionUpdateRequest { Name = "new" }, token); }
        private static JObject Body(State state) { using (var source = new StringReader((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value)) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private sealed class Config : IRestClientConfiguration
        { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-collection-configuration-tests"; }
        private sealed class CollectionsClient : CollectionsRestClient
        { private readonly State state; internal CollectionsClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class CollectionClient : CollectionRestClient
        { private readonly State state; internal CollectionClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.Created; internal string Content = "{\"id\":\"created\"}", Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Content }; }
        }
    }
}
