using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class FolderQueryConfigurationTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OriginalFiltersFlagsAndPathsAreCopiedWithoutLocalNormalization(bool sync)
        {
            var fields = new[] { "id", "has-subfolders" }; var ids = new[] { "a", "a" }; var options = new FolderListingOptions { Collection = "collection", Parent = "none", Document = "document", Group = "group", Path = "original\\literal/with,=%+", Ids = ids, Fields = fields, OnlySharedFolders = false, OnlyTopMost = true, IncludePublicCollections = false };
            var state = new State { BeforeAuthorize = () => { options.Path = "changed"; options.IncludePublicCollections = true; fields[0] = "name"; ids[0] = "changed"; } };
            var result = sync ? new FoldersClient(state).GetConfiguredFolders("context", options) : await new FoldersClient(state).GetConfiguredFoldersAsync("context", options);
            var query = state.Request.Parameters.OfType<QueryParameter>().ToDictionary(p => p.Name, p => (string)p.Value);
            Assert.That(query["collection"], Is.EqualTo("collection")); Assert.That(query["parent"], Is.EqualTo("none")); Assert.That(query["document"], Is.EqualTo("document")); Assert.That(query["group"], Is.EqualTo("group")); Assert.That(query["path"], Is.EqualTo("original\\literal/with,=%+")); Assert.That(query["ids"], Is.EqualTo("a,a")); Assert.That(query["fields"], Is.EqualTo("id,has-subfolders")); Assert.That(query["only-shared-folders"], Is.EqualTo("false")); Assert.That(query["only-top-most"], Is.EqualTo("true")); Assert.That(query["include-public-collections"], Is.EqualTo("false")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(result.Folders[0].HasSubFolders, Is.False); Assert.That(result.Folders[1].HasSubFolders, Is.Null);
        }
        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task NullableFlagsKeepOmittedFalseAndTrueDistinct(bool? flag)
        { var state = new State(); await new FoldersClient(state).GetConfiguredFoldersAsync("context", new FolderListingOptions { Group = "g", OnlySharedFolders = flag, OnlyTopMost = flag, IncludePublicCollections = flag }); var query = state.Request.Parameters.OfType<QueryParameter>().Where(p => p.Name != "group").ToArray(); Assert.That(query.Length, Is.EqualTo(flag.HasValue ? 3 : 0)); foreach(var p in query) Assert.That(p.Value, Is.EqualTo(flag.Value ? "true" : "false")); }
        [Test]
        public async Task TextOnlyAllowsFieldsAndFullPathRequiresExplicitSharingFlags()
        { var state = new State(); await new FoldersClient(state).GetConfiguredFoldersAsync("context", new FolderListingOptions { Text = " original ", Fields = new[] { "id" } }); Assert.That(state.Request.Parameters.OfType<QueryParameter>().Select(p => p.Name), Is.EquivalentTo(new[] { "text", "fields" })); await new FoldersClient(state).GetConfiguredFoldersAsync("context", new FolderListingOptions { OnlySharedFolders = true, OnlyTopMost = true, Fields = new[] { "full-path" } }); Assert.That(state.Request.Parameters.OfType<QueryParameter>().Single(p => p.Name == "fields").Value, Is.EqualTo("full-path")); }
        [TestCase("text-group"), TestCase("text-false-flag"), TestCase("ids-empty"), TestCase("fields-comma"), TestCase("inherited"), TestCase("full-path-default"), TestCase("full-path-false")]
        public void UnsupportedOrMalformedQueriesDoNotAuthorize(string kind)
        {
            var options = new FolderListingOptions { Group = "g" }; if(kind == "text-group") options.Text = "query"; if(kind == "text-false-flag") options = new FolderListingOptions { Text = "query", IncludePublicCollections = false }; if(kind == "ids-empty") options.Ids = new string[0]; if(kind == "fields-comma") options.Fields = new[] { "id,name" }; if(kind == "inherited") options.Fields = new[] { "inherited-sharings" }; if(kind.StartsWith("full-path")) { options.Fields = new[] { "full-path" }; options.OnlySharedFolders = kind.EndsWith("false") ? (bool?)false : null; options.OnlyTopMost = true; }
            var state = new State(); var error = Assert.CatchAsync<Exception>((Func<Task>)(async () => await new FoldersClient(state).GetConfiguredFoldersAsync("context", options))); Assert.That(error, kind == "inherited" || kind.StartsWith("full-path") ? Is.TypeOf<NotSupportedException>() : Is.TypeOf<ArgumentException>()); Assert.That(state.Authorizations, Is.Zero);
        }
        [TestCase("both", false), TestCase("both", true), TestCase("parent", false), TestCase("parent", true), TestCase("top", false), TestCase("top", true)]
        public async Task CreationPreservesBothProvidedLocationSelectionsWithoutAddingDefaultParent(string location, bool sync)
        {
            var collection = location == "parent" ? null : "collection"; var parent = location == "top" ? "none" : "parent"; var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" };
            var result = sync ? new FoldersClient(state).CreateSelectedFolder("context", " original ", collection, parent) : await new FoldersClient(state).CreateSelectedFolderAsync("context", " original ", collection, parent);
            var body = Body(state); Assert.That(body.Value<string>("name"), Is.EqualTo(" original ")); Assert.That(body.Value<string>("parent"), Is.EqualTo(parent)); Assert.That(body.Value<string>("collection"), Is.EqualTo(collection)); Assert.That(result.Id, Is.EqualTo("created")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post));
        }
        [Test]
        public async Task CollectionOnlyCreationOmitsParentAndRetainsAnExplicitEmptyName()
        { var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" }; await new FoldersClient(state).CreateSelectedFolderAsync("context", "", "collection"); Assert.That(Body(state).Property("parent"), Is.Null); Assert.That(Body(state).Value<string>("name"), Is.Empty); }
        [TestCase("parent", null, false), TestCase("parent", "collection", false), TestCase("none", null, false), TestCase("none", "collection", false), TestCase("parent", "collection", true)]
        public async Task MovePreservesBothChoicesOrOmissionWithoutLookingUpTheParent(string parent, string collection, bool sync)
        { var state = new State { Status = HttpStatusCode.NoContent }; if(sync) new FolderClient(state).MoveFolderTo("context", "part/with?query", parent, collection); else await new FolderClient(state).MoveFolderToAsync("context", "part/with?query", parent, collection); Assert.That(state.Request.Resource, Is.EqualTo("v2/folder/part%2Fwith%3Fquery")); Assert.That(state.Request.Method, Is.EqualTo(Method.Put)); Assert.That(Body(state).Value<string>("parent"), Is.EqualTo(parent)); Assert.That(Body(state).Value<string>("collection"), Is.EqualTo(collection)); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(200, "{}"), TestCase(200, "{\"folders\":null}")]
        public void MalformedFolderSuccessDoesNotBecomeAnEmptyListing(int status, string json)
        { var state = new State { Status = (HttpStatusCode)status, Json = json }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new FoldersClient(state).GetConfiguredFoldersAsync("context", new FolderListingOptions { Group = "g" }))); }
        [Test]
        public async Task ConfirmedNoContentProducesAnExplicitEmptyFolderList()
        { var state = new State { Status = HttpStatusCode.NoContent, Json = null }; var result = await new FoldersClient(state).GetConfiguredFoldersAsync("context", new FolderListingOptions { Group = "g" }); Assert.That(result.Folders, Is.Empty); }
        [TestCase("query", 403), TestCase("create", 409), TestCase("move", 403)]
        public void ErrorsDoNotInferPermissionsOrReplay(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status, Json = "{}" }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase("query"), TestCase("create"), TestCase("move")]
        public void PreCancellationDoesNotAuthorize(string operation)
        { var state = new State(); using(var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("query"), TestCase("create"), TestCase("move")]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttp(string operation)
        { var state = new State { Pending = true }; using(var source = new CancellationTokenSource()) { var task = Invoke(state, operation, source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        private static async Task Invoke(State state, string operation, CancellationToken token = default(CancellationToken)) { if(operation == "query") await new FoldersClient(state).GetConfiguredFoldersAsync("context", new FolderListingOptions { Group = "g" }, token); else if(operation == "create") await new FoldersClient(state).CreateSelectedFolderAsync("context", "name", "collection", "parent", token); else await new FolderClient(state).MoveFolderToAsync("context", "selected", "parent", "collection", token); }
        private static JObject Body(State state) => JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-folder-configuration-tests"; }
        private sealed class FoldersClient : FoldersRestClient { private readonly State state; internal FoldersClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send<T>(request, token, client); }
        private sealed class FolderClient : FolderRestClient { private readonly State state; internal FolderClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Json = "{\"folders\":[{\"id\":\"false\",\"has-subfolders\":false},{\"id\":\"unknown\"}]}", Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if(Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json, ContentType = "application/json" }; }
            internal async Task<RestResponse<T>> Send<T>(RestRequest request, CancellationToken token, RestClient client) { var response = await Send(request, token); return new RestResponse<T>(request) { StatusCode = Status, Content = Json, Data = string.IsNullOrEmpty(Json) ? default(T) : client.Serializers.DeserializeContent<T>(response) }; }
        }
    }
}
