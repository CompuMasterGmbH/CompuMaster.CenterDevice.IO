using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Folders;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using RestSharp;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class SharedGroupListingTests
    {
        [TestCase("collection", false, false), TestCase("collection", true, false)]
        [TestCase("group-folder", false, false), TestCase("group-folder", true, false)]
        [TestCase("text-folder", false, false), TestCase("text-folder", true, false)]
        [TestCase("collection", false, true), TestCase("collection", true, true)]
        [TestCase("group-folder", false, true), TestCase("group-folder", true, true)]
        [TestCase("text-folder", false, true), TestCase("text-folder", true, true)]
        public async Task FiltersAreEncodedAsOneQueryValueWithOnlyAllowedParameters(string operation, bool sync, bool includeFields)
        {
            var state = new State();
            var fields = includeFields ? new[] { "id", operation == "collection" ? "has-folders" : "has-subfolders" } : null;
            await Invoke(state, operation, sync, "a&group=other/ümlaut?", fields);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(state.Request.Resource, Is.EqualTo(operation == "collection" ? "v2/collections" : "v2/folders/"));
            Assert.That(state.UserId, Is.EqualTo("context"));
            var filter = operation == "text-folder" ? "text" : "group";
            Assert.That(state.Request.Parameters.Single(p => p.Name == filter).Value, Is.EqualTo("a&group=other/ümlaut?"));
            Assert.That(state.Request.Parameters.Select(p => p.Name), Is.EquivalentTo(includeFields ? new[] { filter, "fields" } : new[] { filter }));
            Assert.That(state.Uri.Query, Does.Contain("%26group%3Dother%2F").IgnoreCase);
            Assert.That(state.Uri.Fragment, Is.Empty);
            Assert.That(state.AuthorizationCalls, Is.EqualTo(1));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("collection"), TestCase("group-folder"), TestCase("text-folder")]
        public async Task FieldArraysAreSnapshottedBeforeAuthorizationCanMutateThem(string operation)
        {
            var state = new State();
            var fields = new[] { "id", "name" };
            state.BeforeAuthorize = () => fields[0] = "mutated";
            await Invoke(state, operation, fields: fields);
            Assert.That(state.Request.Parameters.Single(p => p.Name == "fields").Value, Is.EqualTo("id,name"));
        }

        [TestCase("collection", null), TestCase("group-folder", null), TestCase("text-folder", null)]
        [TestCase("collection", ""), TestCase("group-folder", ""), TestCase("text-folder", "")]
        [TestCase("collection", " "), TestCase("group-folder", " "), TestCase("text-folder", " ")]
        public void MissingFiltersFailBeforeAuthorization(string operation, string value)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await Invoke(state, operation, value: value); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("collection", null), TestCase("group-folder", null), TestCase("text-folder", null)]
        [TestCase("collection", "id,name"), TestCase("group-folder", "id,name"), TestCase("text-folder", "id,name")]
        public void MissingOrCombinedFieldNamesFailBeforeAuthorization(string operation, string field)
        {
            var state = new State();
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await Invoke(state, operation, fields: new[] { field }); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("group-folder", "full-path"), TestCase("text-folder", "full-path")]
        [TestCase("group-folder", "inherited-sharings"), TestCase("text-folder", "inherited-sharings")]
        public void UnsupportedBulkFolderFieldsFailClearlyBeforeAuthorization(string operation, string field)
        {
            var state = new State();
            Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => { await Invoke(state, operation, fields: new[] { field }); }));
            Assert.That(state.AuthorizationCalls, Is.Zero);
        }

        [TestCase("collection", false), TestCase("collection", true)]
        [TestCase("group-folder", false), TestCase("group-folder", true)]
        [TestCase("text-folder", false), TestCase("text-folder", true)]
        public async Task NoContentReturnsKnownEmptyLists(string operation, bool sync)
        {
            var state = new State { Status = HttpStatusCode.NoContent, Json = "" };
            var result = await Invoke(state, operation, sync);
            if (operation == "collection") Assert.That(((CollectionsResults)result).Collections, Is.Empty);
            else Assert.That(((FoldersResponse)result).Folders, Is.Empty);
        }

        [TestCase("collection"), TestCase("group-folder"), TestCase("text-folder")]
        public void SuccessfulResponseWithoutAListingIsNotFabricatedAsEmpty(string operation)
        {
            var state = new State { Json = "{}" };
            Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { await Invoke(state, operation); }));
        }

        [TestCase("collection"), TestCase("group-folder"), TestCase("text-folder")]
        public async Task ListingPreservesUnknownFalseAndTrueChildMetadata(string operation)
        {
            var state = new State();
            var result = await Invoke(state, operation);
            if (operation == "collection")
            {
                var collections = ((CollectionsResults)result).Collections;
                Assert.That(collections.Select(c => c.HasFolders), Is.EqualTo(new bool?[] { false, true, null }));
                Assert.That(collections[0].Owner, Is.EqualTo("not-visible"));
                Assert.That(collections[0].Groups.NotVisibleCount, Is.EqualTo(2));
            }
            else Assert.That(((FoldersResponse)result).Folders.Select(f => f.HasSubFolders), Is.EqualTo(new bool?[] { false, true, null }));
        }

        [TestCase("collection", 403), TestCase("group-folder", 403), TestCase("text-folder", 403)]
        [TestCase("collection", 404), TestCase("group-folder", 404), TestCase("text-folder", 404)]
        public void ServerVisibilityFailuresPreserveTheOriginalException(string operation, int status)
        {
            var original = new InvalidOperationException("isolated transport cause");
            var state = new State { Status = (HttpStatusCode)status, Error = original };
            var error = Assert.CatchAsync<RestClientException>((Func<Task>)(async () => { await Invoke(state, operation); }));
            Assert.That(error.InnerException, Is.SameAs(original));
            Assert.That(state.Calls, Is.EqualTo(1));
        }

        [TestCase("collection"), TestCase("group-folder"), TestCase("text-folder")]
        public void PreCancellationDoesNotAuthorizeOrSend(string operation)
        {
            var state = new State();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await Invoke(state, operation, token: cancellation.Token); }));
            }
            Assert.That(state.AuthorizationCalls, Is.Zero);
            Assert.That(state.Calls, Is.Zero);
        }

        [TestCase("collection"), TestCase("group-folder"), TestCase("text-folder")]
        public async Task ActiveCancellationReachesAuthorizationAndTransport(string operation)
        {
            var state = new State { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                var task = Invoke(state, operation, token: cancellation.Token);
                await state.Entered.Task;
                Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token));
                Assert.That(state.Token, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await task; }));
            }
        }

        private static async Task<object> Invoke(State state, string operation, bool sync = false, string value = "selected", string[] fields = null, CancellationToken token = default(CancellationToken))
        {
            if (operation == "collection")
            {
                var client = new CollectionsClient(state);
                return sync ? client.GetCollectionsSharedWithGroup("context", value, fields, token) : await client.GetCollectionsSharedWithGroupAsync("context", value, fields, token);
            }
            var folders = new FoldersClient(state);
            if (operation == "group-folder") return sync ? folders.GetFoldersSharedWithGroup("context", value, fields, token) : await folders.GetFoldersSharedWithGroupAsync("context", value, fields, token);
            return sync ? folders.SearchFoldersByName("context", value, fields, token) : await folders.SearchFoldersByNameAsync("context", value, fields, token);
        }
        private sealed class Config : IRestClientConfiguration
        {
            public string BaseAddress => "https://unused.test/bridge/";
            public string UserAgent => "shared-group-tests";
        }
        private sealed class CollectionsClient : CollectionsRestClient
        {
            private readonly State state;
            internal CollectionsClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Execute<T>(info, request, token, client);
        }
        private sealed class FoldersClient : FoldersRestClient
        {
            private readonly State state;
            internal FoldersClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; }
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Execute<T>(info, request, token, client);
        }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal int Calls, AuthorizationCalls;
            internal string UserId, Json;
            internal Uri Uri;
            internal RestRequest Request;
            internal CancellationToken Token, AuthorizationToken;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Exception Error;
            internal bool Pending;
            internal Action BeforeAuthorize;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native async authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                AuthorizationCalls++; AuthorizationToken = cancellationToken; BeforeAuthorize?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
            internal async Task<RestResponse<T>> Execute<T>(OAuthInfo info, RestRequest request, CancellationToken token, RestClient client)
            {
                Calls++; Request = request; UserId = info.UserId; Token = token; Uri = client.BuildUri(request); Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                var json = Json ?? (typeof(T) == typeof(CollectionsResults)
                    ? "{\"collections\":[{\"id\":\"zero\",\"has-folders\":false,\"owner\":\"not-visible\",\"groups\":{\"not-visible-count\":2}},{\"id\":\"children\",\"has-folders\":true},{\"id\":\"unknown\"}]}"
                    : "{\"folders\":[{\"id\":\"zero\",\"has-subfolders\":false},{\"id\":\"children\",\"has-subfolders\":true},{\"id\":\"unknown\"}]}");
                var response = new RestResponse(request) { StatusCode = Status, ErrorException = Error, ContentType = "application/json", Content = json };
                return new RestResponse<T>(request) { StatusCode = Status, ErrorException = Error, Data = string.IsNullOrEmpty(json) ? default(T) : client.Serializers.DeserializeContent<T>(response) };
            }
        }
    }
}
