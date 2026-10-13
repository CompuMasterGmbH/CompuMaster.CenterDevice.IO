using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Link;
using CenterDevice.Rest.Clients.OAuth;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using RestSharp;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class UploadLinkTagArrayRegressionTests
    {
        [TestCase(false), TestCase(true)]
        public async Task HistoricalCreateFormsSendTheDeclaredTagArrayRatherThanAJsonString(bool synchronous)
        {
            var state = new State(); var client = new CreationClient(state); var tags = new List<string> { "Original", "original", " tag " };
            if (synchronous) client.CreateCollectionLink("context", "collection", "name", tags, null, null, "", false);
            else await client.CreateCollectionLinkAsync("context", "collection", "name", tags, null, null, "", false);
            AssertBody(state, tags);
        }
        [Test]
        public async Task HistoricalUpdateSendsTheDeclaredTagArrayWithoutChangingOtherChoices()
        { var state = new State(); var tags = new List<string> { "Original", "original", " tag " }; await new SelectedClient(state).UpdateLinkAsync("context", "selected", "collection", "name", tags, null, null, "", false); AssertBody(state, tags); }
        private static void AssertBody(State state, List<string> tags)
        { var body = JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value); Assert.That(body["tags"], Is.TypeOf<JArray>()); Assert.That(body["tags"].Values<string>(), Is.EqualTo(tags)); Assert.That(body.Value<string>("collection"), Is.EqualTo("collection")); Assert.That(body.Value<string>("name"), Is.EqualTo("name")); Assert.That(body.Value<string>("password"), Is.Empty); Assert.That(body.Value<bool>("email-creator-on-upload"), Is.False); Assert.That(body["expiry-date"], Is.Null); Assert.That(body["max-documents"], Is.Null); }
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/"; public string UserAgent => "isolated-upload-link-regression"; }
        private sealed class CreationClient : UploadLinksRestClient
        {
            private readonly State state; internal CreationClient(State state) : base(state, new Config(), null, "v2/", "https://unused.invalid/upload/") { this.state = state; }
            protected override RestResponse<T> Execute<T>(OAuthInfo info, RestRequest request) { state.Request = request; return new RestResponse<T>(request) { StatusCode = HttpStatusCode.Created, Data = new T() }; }
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) { state.Request = request; return Task.FromResult(new RestResponse<T>(request) { StatusCode = HttpStatusCode.Created, Data = new T() }); }
        }
        private sealed class SelectedClient : UploadLinkRestClient
        { private readonly State state; internal SelectedClient(State state) : base(state, new Config(), null, "v2/", "https://unused.invalid/upload/") { this.state = state; } protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) { state.Request = request; return Task.FromResult(new RestResponse<T>(request) { StatusCode = HttpStatusCode.NoContent, Data = new T() }); } }
        private sealed class State : IAsyncOAuthInfoProvider
        { internal RestRequest Request; public OAuthInfo GetOAuthInfo(string userId) => new OAuthInfo { UserId = userId, access_token = "fixture" }; public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) => Task.FromResult(GetOAuthInfo(userId)); }
    }
}
