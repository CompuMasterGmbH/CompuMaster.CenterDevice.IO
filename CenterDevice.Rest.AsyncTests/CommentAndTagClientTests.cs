using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Comments;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Clients.Tags;
using CenterDevice.Rest.Exceptions;
using Newtonsoft.Json.Linq;
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
    public class CommentAndTagClientTests
    {
        private const string Detail = "{\"id\":\"comment/ö ?\",\"author\":\"actual-author\",\"creation-date\":\"2025-04-05T06:07:08.1234567+05:30\",\"edit-date\":null,\"text\":\" <b>Original</b>\\r\\nText \",\"future\":{\"date\":\"2025-01-02\",\"choice\":false}}";
        [TestCase(0, false), TestCase(0, true), TestCase(1, false), TestCase(1, true), TestCase(2, false), TestCase(2, true), TestCase(3, false), TestCase(3, true), TestCase(4, false), TestCase(4, true), TestCase(5, false), TestCase(5, true)]
        public async Task AllFormsUseExactSelectedRoutesBodiesStatusesAndAuthenticationContext(int operation, bool synchronous)
        {
            var state = new State { Status = operation == 0 ? HttpStatusCode.Created : operation == 1 || operation == 2 ? HttpStatusCode.NoContent : HttpStatusCode.OK, Json = operation == 0 ? "{\"id\":\"created\"}" : operation == 4 ? "{\"comments\":[" + Detail + "]}" : operation == 5 ? "{\"tags\":[\"Tag\",\"tag\",\"Tag\"]}" : Detail };
            var client = new Client(state);
            if (operation == 0) { var result = synchronous ? client.AddComment("context", "document/ö ?", " <b>Original</b>\r\nText ") : await client.AddCommentAsync("context", "document/ö ?", " <b>Original</b>\r\nText "); Assert.That(result.Id, Is.EqualTo("created")); Assert.That(result.Author, Is.Null); Assert.That(result.CreationDate, Is.Null); Assert.That(result.Text, Is.Null); }
            else if (operation == 1) { if (synchronous) client.UpdateComment("context", "document/ö ?", "comment/ö ?", " updated "); else await client.UpdateCommentAsync("context", "document/ö ?", "comment/ö ?", " updated "); }
            else if (operation == 2) { if (synchronous) client.RemoveComment("context", "document/ö ?", "comment/ö ?"); else await client.RemoveCommentAsync("context", "document/ö ?", "comment/ö ?"); }
            else if (operation == 3) { var result = synchronous ? client.GetComment("context", "document/ö ?", "comment/ö ?") : await client.GetCommentAsync("context", "document/ö ?", "comment/ö ?"); AssertDetail(result); }
            else if (operation == 4) { var result = synchronous ? client.GetComments("context", "document/ö ?") : await client.GetCommentsAsync("context", "document/ö ?"); AssertDetail(result.Comments.Single()); Assert.That(result.RawJson, Is.EqualTo(state.Json)); result.GetRawDetails()["comments"] = new JArray(); Assert.That(result.Comments.Count, Is.EqualTo(1)); }
            else
            {
#pragma warning disable CS0618 // Explicit manufacturer-deprecated endpoint verification.
                var tags = new TagClient(state); var result = synchronous ? tags.GetTags("context") : await tags.GetTagsAsync("context");
#pragma warning restore CS0618
                Assert.That(result.Tags, Is.EqualTo(new[] { "Tag", "tag", "Tag" })); Assert.That(result.RawJson, Is.EqualTo(state.Json)); result.GetRawDetails()["tags"] = new JArray(); Assert.That(result.Tags.Count, Is.EqualTo(3));
            }
            Assert.That(state.Request.Method, Is.EqualTo(operation == 0 ? Method.Post : operation == 1 ? Method.Put : operation == 2 ? Method.Delete : Method.Get));
            var expected = operation == 5 ? "v2/tags" : "v2/document/document%2F%C3%B6%20%3F/" + (operation == 0 || operation == 4 ? "comments" : "comment/comment%2F%C3%B6%20%3F"); Assert.That(state.Request.Resource, Is.EqualTo(expected).IgnoreCase);
            var body = state.Request.Parameters.OfType<BodyParameter>().SingleOrDefault(); if (operation < 2) Assert.That(JObject.Parse((string)body.Value).Value<string>("text"), Is.EqualTo(operation == 0 ? " <b>Original</b>\r\nText " : " updated ")); else Assert.That(body, Is.Null);
            Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Request.Parameters.Where(p => p.Name == "Accept").Single().Value, Is.EqualTo("application/json")); Assert.That(state.Calls, Is.EqualTo(1)); Assert.That(state.Authorizations, Is.EqualTo(1));
        }
        private static void AssertDetail(DocumentComment result)
        { Assert.That(result.Author, Is.EqualTo("actual-author")); Assert.That(result.Text, Is.EqualTo(" <b>Original</b>\r\nText ")); Assert.That(result.CreationDate, Is.EqualTo("2025-04-05T06:07:08.1234567+05:30")); Assert.That(result.EditDate, Is.Null); Assert.That(result.GetRawDetails()["future"].Value<string>("date"), Is.EqualTo("2025-01-02")); result.GetRawDetails()["text"] = "changed"; Assert.That(result.Text, Is.EqualTo(" <b>Original</b>\r\nText ")); }
        [TestCase(""), TestCase(" "), TestCase("<ul><li>Original</li></ul>")]
        public async Task OriginalExplicitTextIsSentForServerValidation(string text)
        { var state = new State { Status = HttpStatusCode.Created, Json = "{\"id\":\"created\"}" }; await new Client(state).AddCommentAsync("context", "selected", text); Assert.That(JObject.Parse((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value).Value<string>("text"), Is.EqualTo(text)); }
        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidDocumentSelectionDoesNotAuthorize(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).AddCommentAsync("context", id, "text"))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(null), TestCase(""), TestCase("."), TestCase("..")]
        public void InvalidCommentSelectionDoesNotAuthorize(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).RemoveCommentAsync("context", "selected", id))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(false), TestCase(true)]
        public void NullTextIsNotAnImplicitClearOrEmptyComment(bool update)
        { var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => { if (update) await new Client(state).UpdateCommentAsync("context", "selected", "comment", null); else await new Client(state).AddCommentAsync("context", "selected", null); })); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("{}"), TestCase("{\"id\":null}"), TestCase("{\"id\":5}"), TestCase("{\"id\":\"\"}"), TestCase("{\"id\":\"one\",\"id\":\"two\"}"), TestCase("{\"id\":\"one\"}{}")]
        public void MalformedCreationCannotInferAUsableComment(string json)
        { var state = new State { Status = HttpStatusCode.Created, Json = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).AddCommentAsync("context", "selected", "text"))); Assert.That(error.InnerException, Is.Not.Null); }
        [TestCase("{\"id\":\"other\",\"author\":\"author\",\"text\":\"text\"}"), TestCase("{\"id\":\"selected\",\"text\":\"text\"}"), TestCase("{\"id\":\"selected\",\"author\":false,\"text\":\"text\"}"), TestCase("{\"id\":\"selected\",\"author\":\"author\",\"text\":null}"), TestCase("{\"id\":\"selected\",\"author\":\"author\",\"text\":\"text\",\"creation-date\":5}")]
        public void MalformedSelectedMetadataOrAnotherIdentityRetainsInnerDiagnostics(string json)
        { var state = new State { Json = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetCommentAsync("context", "document", "selected"))); Assert.That(error.InnerException, Is.Not.Null); }
        [Test]
        public async Task CanonicalUuidResponseDoesNotChangeTheOriginalSelectedRoute()
        { var id = Guid.NewGuid(); var selected = id.ToString("B").ToUpperInvariant(); var state = new State { Json = new JObject { ["id"] = id.ToString("D"), ["author"] = "author", ["text"] = "text" }.ToString() }; var result = await new Client(state).GetCommentAsync("context", "document", selected); Assert.That(result.Id, Is.EqualTo(id.ToString("D"))); Assert.That(state.Request.Resource, Does.EndWith(Uri.EscapeDataString(selected))); }
        [TestCase("{}"), TestCase("{\"comments\":null}"), TestCase("{\"comments\":[null]}"), TestCase("{\"comments\":[{\"id\":\"id\",\"author\":\"author\",\"text\":\"text\"},{\"id\":\"id\",\"author\":\"author\",\"text\":\"text\"}]}")]
        public void InvalidListDoesNotBecomeAnEmptyOrDuplicateCommentInventory(string json)
        { var state = new State { Json = json }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetCommentsAsync("context", "document"))); }
        [TestCase(false), TestCase(true)]
        public async Task KnownEmptyInventoriesRemainExplicit(bool tags)
        {
            var state = new State { Json = tags ? "{\"tags\":[],\"future\":false}" : "{\"comments\":[],\"future\":false}" };
#pragma warning disable CS0618
            if (tags) Assert.That((await new TagClient(state).GetTagsAsync("context")).Tags, Is.Empty); else Assert.That((await new Client(state).GetCommentsAsync("context", "document")).Comments, Is.Empty);
#pragma warning restore CS0618
        }
        [TestCase("{}"), TestCase("{\"tags\":null}"), TestCase("{\"tags\":[1]}"), TestCase("{\"tags\":[],\"tags\":[]}"), TestCase("{\"tags\":[]}{}")]
        public void InvalidTagInventoryRetainsInnerDiagnostics(string json)
        {
            var state = new State { Json = json };
#pragma warning disable CS0618
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new TagClient(state).GetTagsAsync("context")));
#pragma warning restore CS0618
            Assert.That(error.InnerException, Is.Not.Null);
        }
        [TestCase(403), TestCase(404), TestCase(409), TestCase(500)]
        public void ServerErrorsDoNotInferVisibilityCreatorRightsOrReplay(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).AddCommentAsync("context", "document", "text"))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public async Task NativeCancellationReachesAuthorizationAndActiveIo(int operation)
        {
            var state = new State { Pending = true }; using (var cancellation = new CancellationTokenSource()) { var task = Invoke(state, operation, cancellation.Token); await state.Entered.Task; Assert.That(state.AuthorizationToken, Is.EqualTo(cancellation.Token)); Assert.That(state.Token, Is.EqualTo(cancellation.Token)); cancellation.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); Assert.That(state.Calls, Is.EqualTo(1)); }
        }
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public void PreCancellationDoesNotAuthorize(int operation)
        { var state = new State(); using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, cancellation.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        private static async Task Invoke(State state, int operation, CancellationToken token)
        {
            var client = new Client(state);
            if (operation == 0) await client.AddCommentAsync("context", "document", "text", token); else if (operation == 1) await client.UpdateCommentAsync("context", "document", "comment", "text", token); else if (operation == 2) await client.RemoveCommentAsync("context", "document", "comment", token); else if (operation == 3) await client.GetCommentAsync("context", "document", "comment", token); else if (operation == 4) await client.GetCommentsAsync("context", "document", token);
#pragma warning disable CS0618
            else await new TagClient(state).GetTagsAsync("context", token);
#pragma warning restore CS0618
        }
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-comment-and-tag-fixture"; }
        private sealed class Client : CommentsRestClient { private readonly State state; internal Client(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class TagClient : TagsRestClient { private readonly State state; internal TagClient(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Json = "{}", Context; internal int Calls, Authorizations; internal bool Pending; internal CancellationToken Token, AuthorizationToken; internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Context = userId; Authorizations++; AuthorizationToken = token; return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Request = request; Token = token; Calls++; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
