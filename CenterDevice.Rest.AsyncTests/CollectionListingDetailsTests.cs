using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
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
    public class CollectionListingDetailsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task OriginalFiltersAndTwiceEncodedMetadataAreSnapshotted(bool sync)
        {
            var fields = new[] { "extended-metadata", "has-folders" }; var ids = new[] { "a", "a" }; var metadata = new[] { new KeyValuePair<string, string>("key,=", "value,+%="), new KeyValuePair<string, string>("key,=", "") };
            var options = new CollectionListingOptions { IncludePublic = false, Name = " name,+%=/ ", Group = " group ", Fields = fields, Ids = ids, ExtendedMetadata = metadata };
            var state = new State { BeforeAuthorize = () => { options.IncludePublic = true; options.Name = "changed"; fields[0] = "name"; ids[0] = "changed"; metadata[0] = new KeyValuePair<string, string>("changed", "changed"); } };
            var result = sync ? new Client(state).GetCollectionDetails("context", options) : await new Client(state).GetCollectionDetailsAsync("context", options);
            var query = state.Request.Parameters.OfType<QueryParameter>().ToDictionary(value => value.Name, value => (string)value.Value);
            Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(state.Request.Resource, Is.EqualTo("v2/collections")); Assert.That(state.Context, Is.EqualTo("context"));
            Assert.That(query["include-public"], Is.EqualTo("false")); Assert.That(query["name"], Is.EqualTo(" name,+%=/ ")); Assert.That(query["group"], Is.EqualTo(" group ")); Assert.That(query["ids"], Is.EqualTo("a,a")); Assert.That(query["fields"], Is.EqualTo("extended-metadata,has-folders"));
            Assert.That(query["extended-metadata"], Is.EqualTo("key%2C%3D=value%2C%2B%25%3D,key%2C%3D="));
            var uri = new RestClient(new RestClientOptions("https://unused.invalid/bridge/")).BuildUri(state.Request);
            Assert.That(uri.AbsoluteUri, Does.Contain("extended-metadata=key%252C%253D%3Dvalue%252C%252B%2525%253D%2Ckey%252C%253D%3D").IgnoreCase); Assert.That(result, Is.Empty);
        }
        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task PublicSelectionPreservesOmittedFalseAndTrue(bool? includePublic)
        { var state = new State(); await new Client(state).GetCollectionDetailsAsync("context", new CollectionListingOptions { IncludePublic = includePublic }); var parameter = state.Request.Parameters.OfType<QueryParameter>().SingleOrDefault(); Assert.That(parameter != null, Is.EqualTo(includePublic.HasValue)); if (parameter != null) Assert.That(parameter.Value, Is.EqualTo(includePublic.Value ? "true" : "false")); }
        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task DeprecatedAllChoiceRemainsExplicitWithoutInferringPrecedence(bool? all)
        {
#pragma warning disable CS0618
            var state = new State(); await new Client(state).GetCollectionDetailsAsync("context", new CollectionListingOptions { All = all, IncludePublic = false });
#pragma warning restore CS0618
            var parameter = state.Request.Parameters.OfType<QueryParameter>().SingleOrDefault(value => value.Name == "all"); Assert.That(parameter != null, Is.EqualTo(all.HasValue)); if (parameter != null) Assert.That(parameter.Value, Is.EqualTo(all.Value ? "true" : "false"));
        }
        [Test]
        public async Task NoOptionsPreserveAllServerDefaults()
        { var state = new State(); await new Client(state).GetCollectionDetailsAsync("context"); Assert.That(state.Request.Parameters.OfType<QueryParameter>(), Is.Empty); }
        [TestCase(200), TestCase(204)]
        public async Task ConfirmedEmptyListingIsImmutable(int status)
        { var state = new State { Status = (HttpStatusCode)status, Content = status == 204 ? null : "{\"collections\":[]}" }; var result = await new Client(state).GetCollectionDetailsAsync("context"); Assert.That(result, Is.Empty); Assert.Throws<NotSupportedException>((Action)(() => ((IList<CollectionDetails>)result).Clear())); }
        [Test]
        public async Task ProjectionsDatesAndHiddenCountsPreserveUnknownVersusKnownValues()
        {
            var state = new State { Content = "{\"collections\":[{\"has-folders\":false,\"users\":{\"visible\":[],\"not-visible-count\":0},\"extended-metadata\":{\"date\":\"2099-12-31\"}},{\"id\":\"second\",\"public\":null,\"owner\":\"not-visible\",\"future\":true}]}" };
            var result = await new Client(state).GetCollectionDetailsAsync("context"); Assert.That(result.Count, Is.EqualTo(2)); Assert.That(result[0].Id, Is.Null); Assert.That(result[0].HasFolders, Is.False); Assert.That(result[0].Users.NotVisibleCount, Is.EqualTo("0")); Assert.That(result[0].GetExtendedMetadata()["date"].Type.ToString(), Is.EqualTo("String")); Assert.That(result[1].HasFolders, Is.Null); Assert.That(result[1].Owner, Is.EqualTo("not-visible")); Assert.That(result[1].Public, Is.Null); Assert.That(result[1].GetRawDetails().Value<bool>("future"), Is.True);
        }
        [TestCase("ids-empty"), TestCase("ids-comma"), TestCase("fields-empty"), TestCase("fields-blank"), TestCase("group"), TestCase("metadata-empty"), TestCase("metadata-key"), TestCase("metadata-null")]
        public void UnusableSelectionsFailBeforeAuthorization(string kind)
        {
            var options = new CollectionListingOptions();
            if (kind == "ids-empty") options.Ids = new string[0]; if (kind == "ids-comma") options.Ids = new[] { "a,b" }; if (kind == "fields-empty") options.Fields = new string[0]; if (kind == "fields-blank") options.Fields = new[] { " " }; if (kind == "group") options.Group = " ";
            if (kind == "metadata-empty") options.ExtendedMetadata = new KeyValuePair<string, string>[0]; if (kind == "metadata-key") options.ExtendedMetadata = new[] { new KeyValuePair<string, string>(" ", "value") }; if (kind == "metadata-null") options.ExtendedMetadata = new[] { new KeyValuePair<string, string>("key", null) };
            var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", options))); Assert.That(state.Authorizations, Is.Zero);
        }
        [TestCase(""), TestCase("{}"), TestCase("[]"), TestCase("{\"collections\":null}"), TestCase("{\"collections\":[null]}"), TestCase("{\"collections\":[{\"id\":\" \"}]}"), TestCase("{\"collections\":[{\"has-folders\":0}]}"), TestCase("{\"collections\":[],\"collections\":[]}"), TestCase("{\"collections\":[]}{}")]
        public void MalformedSuccessCannotBecomeAnEmptyListing(string content)
        { var state = new State { Content = content }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context"))); Assert.That(error.InnerException, Is.Not.Null); }
        [TestCase(403), TestCase(404), TestCase(500)]
        public void ServerErrorsDoNotInferVisibilityOrReplay(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context"))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [Test]
        public void PreCancellationDoesNotAuthorize()
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetCollectionDetailsAsync("context", cancellationToken: source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttp()
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var task = new Client(state).GetCollectionDetailsAsync("context", cancellationToken: source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        private sealed class Config : IRestClientConfiguration
        { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-collection-listing-tests"; }
        private sealed class Client : CollectionsRestClient
        { private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Content = "{\"collections\":[]}", Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Content }; }
        }
    }
}
