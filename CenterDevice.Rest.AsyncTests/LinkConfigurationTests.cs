using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Link;
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
    public class LinkConfigurationTests
    {
        private const string ServerWeb = "https://tenant.invalid/server-selected/custom?value=original";
        private static string Metadata(bool upload) => upload
            ? "{\"id\":\"selected/ö ?\",\"web\":\"" + ServerWeb + "\",\"creator\":\"actual-owner\",\"name\":\"Original\",\"creation-date\":\"2025-04-05T06:07:08+05:30\",\"expiry-date\":null,\"folder\":\"actual-folder\",\"tags\":[\"Tag\",\"tag\"],\"max-documents\":18446744073709551616,\"max-bytes\":2147483648,\"uploads\":0,\"uploaded-bytes\":null,\"password\":\"fixture-only\",\"email-creator-on-upload\":false,\"future\":{\"choice\":false}}"
            : "{\"id\":\"selected/ö ?\",\"web\":\"" + ServerWeb + "\",\"download\":\"https://tenant.invalid/original/download\",\"rest\":\"https://tenant.invalid/original/rest\",\"document-id\":\"actual-document\",\"folder\":null,\"downloads\":18446744073709551616,\"views\":0,\"access-control\":{\"expiry-date\":\"2025-04-05T06:07:08+05:30\",\"max-downloads\":100,\"password\":\"fixture-only\",\"view-only\":false,\"hide-date\":true,\"future\":false},\"future\":{\"choice\":false}}";
        [TestCase(0, false), TestCase(0, true), TestCase(1, false), TestCase(1, true), TestCase(2, false), TestCase(2, true), TestCase(3, false), TestCase(3, true), TestCase(4, false), TestCase(4, true), TestCase(5, false), TestCase(5, true), TestCase(6, false), TestCase(6, true), TestCase(7, false), TestCase(7, true), TestCase(8, false), TestCase(8, true), TestCase(9, false), TestCase(9, true), TestCase(10, false), TestCase(10, true)]
        public async Task AllFormsKeepExactVerbsSelectionsBodiesOutcomesAndContext(int operation, bool sync)
        {
            var state = new State { Status = operation < 3 || operation == 6 ? HttpStatusCode.Created : operation == 4 || operation == 5 || operation == 9 || operation == 10 ? HttpStatusCode.NoContent : HttpStatusCode.OK, Json = operation == 8 ? "{\"upload-links\":[" + Metadata(true) + "]}" : Metadata(operation >= 6) };
            var controls = Controls(); var options = Options(); state.Before = () => { controls.HideDate = false; options.Folder = "changed"; };
            if (operation < 3)
            {
                var client = new Plural(state); LinkDetails result;
                if (operation == 0) result = sync ? client.CreateConfiguredDocumentLink("context", "original/ö ?", controls) : await client.CreateConfiguredDocumentLinkAsync("context", "original/ö ?", controls);
                else if (operation == 1) result = sync ? client.CreateConfiguredFolderLink("context", "original/ö ?", controls) : await client.CreateConfiguredFolderLinkAsync("context", "original/ö ?", controls);
                else result = sync ? client.CreateConfiguredCollectionLink("context", "original/ö ?", controls) : await client.CreateConfiguredCollectionLinkAsync("context", "original/ö ?", controls);
                AssertLink(result); Assert.That(Body(state).Value<string>(operation == 0 ? "document-id" : operation == 1 ? "folder" : "collection"), Is.EqualTo("original/ö ?")); AssertControls(Body(state)["access-control"]);
            }
            else if (operation == 3) { var client = new Selected(state); var result = sync ? client.GetLinkDetails("context", "selected/ö ?") : await client.GetLinkDetailsAsync("context", "selected/ö ?"); AssertLink(result); }
            else if (operation == 4) { var client = new Selected(state); if (sync) client.ReplaceAccessControl("context", "selected/ö ?", controls); else await client.ReplaceAccessControlAsync("context", "selected/ö ?", controls); AssertControls(Body(state)["access-control"]); Assert.That(Body(state).Properties().Count(), Is.EqualTo(1)); }
            else if (operation == 5) { var client = new Selected(state); if (sync) client.RemoveLink("context", "selected/ö ?"); else await client.RemoveLinkAsync("context", "selected/ö ?"); }
            else if (operation == 6) { var client = new UploadPlural(state); var result = sync ? client.CreateConfiguredUploadLink("context", options) : await client.CreateConfiguredUploadLinkAsync("context", options); AssertUpload(result); AssertOptions(Body(state)); }
            else if (operation == 7) { var client = new UploadSelected(state); var result = sync ? client.GetUploadLinkDetails("context", "selected/ö ?") : await client.GetUploadLinkDetailsAsync("context", "selected/ö ?"); AssertUpload(result); }
            else if (operation == 8) { var client = new UploadPlural(state); var result = sync ? client.GetUploadLinkDetails("context") : await client.GetUploadLinkDetailsAsync("context"); AssertUpload(result.Links.Single()); Assert.That(result.Links.Single().RawJson, Is.Null); Assert.That(result.RawJson, Is.EqualTo(state.Json)); result.GetRawDetails()["upload-links"] = new JArray(); Assert.That(result.Links.Count, Is.EqualTo(1)); }
            else if (operation == 9) { var client = new UploadSelected(state); if (sync) client.UpdateConfiguredUploadLink("context", "selected/ö ?", options); else await client.UpdateConfiguredUploadLinkAsync("context", "selected/ö ?", options); AssertOptions(Body(state)); }
            else { var client = new UploadSelected(state); if (sync) client.RemoveUploadLink("context", "selected/ö ?"); else await client.RemoveUploadLinkAsync("context", "selected/ö ?"); }
            var method = operation < 3 || operation == 6 ? Method.Post : operation == 4 || operation == 9 ? Method.Put : operation == 5 || operation == 10 ? Method.Delete : Method.Get; Assert.That(state.Request.Method, Is.EqualTo(method));
            var path = operation < 3 ? "v2/links" : operation < 6 ? "v2/link/selected%2F%C3%B6%20%3F" : operation == 6 || operation == 8 ? "v2/upload-links" : "v2/upload-link/selected%2F%C3%B6%20%3F"; Assert.That(state.Request.Resource, Is.EqualTo(path).IgnoreCase);
            if (method == Method.Get || method == Method.Delete) Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(state.Request.Parameters.Where(p => p.Name == "Accept").Single().Value, Is.EqualTo("application/json")); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(state.Calls, Is.EqualTo(1));
        }
        private static LinkAccessControlOptions Controls() => new LinkAccessControlOptions { ExpiryDate = "2025-04-05T06:07:08.1234567+05:30", MaxDownloads = 0, Password = "", ViewOnly = false, HideDate = true, ["future"] = new JObject { ["choice"] = false } };
        private static UploadLinkOptions Options() => new UploadLinkOptions { Name = "Original", Folder = "actual-folder", Tags = new[] { "Tag", "tag", "Tag" }, ExpiryDate = "2025-04-05T06:07:08.1234567+05:30", MaxDocuments = 1, MaxBytes = 1073741824, Password = "", EmailCreatorOnUpload = false, ["future"] = new JObject { ["choice"] = false } };
        private static void AssertControls(JToken controls) { Assert.That(controls.Value<string>("expiry-date"), Is.EqualTo("2025-04-05T06:07:08.1234567+05:30")); Assert.That(controls.Value<long>("max-downloads"), Is.Zero); Assert.That(controls.Value<string>("password"), Is.Empty); Assert.That(controls.Value<bool>("view-only"), Is.False); Assert.That(controls.Value<bool>("hide-date"), Is.True); Assert.That(controls["future"].Value<bool>("choice"), Is.False); }
        private static void AssertOptions(JObject options) { Assert.That(options.Value<string>("name"), Is.EqualTo("Original")); Assert.That(options.Value<string>("folder"), Is.EqualTo("actual-folder")); Assert.That(options["collection"], Is.Null); Assert.That(options["tags"].Values<string>(), Is.EqualTo(new[] { "Tag", "tag", "Tag" })); Assert.That(options.Value<string>("expiry-date"), Is.EqualTo("2025-04-05T06:07:08.1234567+05:30")); Assert.That(options.Value<long>("max-documents"), Is.EqualTo(1)); Assert.That(options.Value<long>("max-bytes"), Is.EqualTo(1073741824)); Assert.That(options.Value<string>("password"), Is.Empty); Assert.That(options.Value<bool>("email-creator-on-upload"), Is.False); Assert.That(options["future"].Value<bool>("choice"), Is.False); }
        private static void AssertLink(LinkDetails result) { Assert.That(result.Web, Is.EqualTo(ServerWeb)); Assert.That(result.DocumentId, Is.EqualTo("actual-document")); Assert.That(result.Collection, Is.Null); Assert.That(result.Folder, Is.Null); Assert.That(result.Downloads, Is.EqualTo("18446744073709551616")); Assert.That(result.Views, Is.EqualTo("0")); Assert.That(result.ViewOnly, Is.False); Assert.That(result.HideDate, Is.True); Assert.That(result.ExpiryDate, Is.EqualTo("2025-04-05T06:07:08+05:30")); Assert.That(result.MaxDownloads, Is.EqualTo("100")); Assert.That(result.Password, Is.EqualTo("fixture-only")); result.GetAccessControl()["view-only"] = true; result.GetRawDetails()["web"] = "changed"; Assert.That(result.Web, Is.EqualTo(ServerWeb)); Assert.That(result.ViewOnly, Is.False); }
        private static void AssertUpload(UploadLinkDetails result) { Assert.That(result.Web, Is.EqualTo(ServerWeb)); Assert.That(result.Creator, Is.EqualTo("actual-owner")); Assert.That(result.Name, Is.EqualTo("Original")); Assert.That(result.CreationDate, Is.EqualTo("2025-04-05T06:07:08+05:30")); Assert.That(result.ExpiryDate, Is.Null); Assert.That(result.Folder, Is.EqualTo("actual-folder")); Assert.That(result.Collection, Is.Null); Assert.That(result.Tags, Is.EqualTo(new[] { "Tag", "tag" })); Assert.That(result.MaxDocuments, Is.EqualTo("18446744073709551616")); Assert.That(result.Uploads, Is.EqualTo("0")); Assert.That(result.UploadedBytes, Is.Null); Assert.That(result.EmailCreatorOnUpload, Is.False); Assert.That(result.Password, Is.EqualTo("fixture-only")); result.GetRawDetails()["web"] = "changed"; Assert.That(result.Web, Is.EqualTo(ServerWeb)); }
        [Test]
        public async Task CreationOmissionAndWholeControlReplacementAreExplicitWithoutDefaults()
        { var state = new State { Status = HttpStatusCode.Created, Json = Metadata(false) }; await new Plural(state).CreateConfiguredDocumentLinkAsync("context", "document"); Assert.That(Body(state).Properties().Select(p => p.Name), Is.EqualTo(new[] { "document-id" })); state.Status = HttpStatusCode.NoContent; await new Selected(state).ReplaceAccessControlAsync("context", "selected", new LinkAccessControlOptions()); Assert.That(((JObject)Body(state)["access-control"]).Properties(), Is.Empty); }
        [Test]
        public async Task UploadCreationAndPartialUpdateDoNotInsertDefaultsOrClearOmittedChoices()
        { var state = new State { Status = HttpStatusCode.Created, Json = Metadata(true) }; await new UploadPlural(state).CreateConfiguredUploadLinkAsync("context"); Assert.That(Body(state).Properties(), Is.Empty); state.Status = HttpStatusCode.NoContent; await new UploadSelected(state).UpdateConfiguredUploadLinkAsync("context", "selected", new UploadLinkOptions { Name = "changed" }); Assert.That(Body(state).Properties().Select(p => p.Name), Is.EqualTo(new[] { "name" })); }
        [Test]
        public async Task RawPasswordNullAndCollectionTargetArePreservedWithoutInferringCurrentFolder()
        { var state = new State { Status = HttpStatusCode.NoContent }; var options = new UploadLinkOptions { Collection = "selected", ["password"] = JValue.CreateNull() }; await new UploadSelected(state).UpdateConfiguredUploadLinkAsync("context", "link", options); Assert.That(Body(state)["password"].Type, Is.EqualTo(JTokenType.Null)); Assert.That(Body(state).Value<string>("collection"), Is.EqualTo("selected")); Assert.That(Body(state)["folder"], Is.Null); }
        [TestCase("{\"max-documents\":0}"), TestCase("{\"max-documents\":-1}"), TestCase("{\"max-documents\":1.5}"), TestCase("{\"max-bytes\":0}"), TestCase("{\"max-bytes\":1}"), TestCase("{\"max-bytes\":18446744073709551616}"), TestCase("{\"collection\":\"one\",\"folder\":\"two\"}"), TestCase("{\"folder\":null}"), TestCase("{\"tags\":\"[]\"}"), TestCase("{\"tags\":[false]}"), TestCase("{\"expiry-date\":5}"), TestCase("{\"email-creator-on-upload\":null}")]
        public void InvalidUploadChoicesDoNotAuthorize(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new UploadPlural(state).CreateConfiguredUploadLinkAsync("context", new UploadLinkOptions(Original(json))))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("{\"hide-date\":null}"), TestCase("{\"view-only\":1}"), TestCase("{\"max-downloads\":1.5}"), TestCase("{\"expiry-date\":5}"), TestCase("{\"password\":false}")]
        public void InvalidKnownControlsDoNotAuthorize(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Plural(state).CreateConfiguredDocumentLinkAsync("context", "document", new LinkAccessControlOptions(Original(json))))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(false), TestCase(true)]
        public void NullUpdatesAreNotImplicitClears(bool upload)
        { var state = new State(); Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => { if (upload) await new UploadSelected(state).UpdateConfiguredUploadLinkAsync("context", "selected", null); else await new Selected(state).ReplaceAccessControlAsync("context", "selected", null); })); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(null), TestCase(""), TestCase("."), TestCase("..")]
        public void InvalidSelectionsDoNotAuthorize(string selected)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Selected(state).GetLinkDetailsAsync("context", selected))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("{}"), TestCase("{\"id\":\"one\"}"), TestCase("{\"id\":\"one\",\"web\":\"url\",\"download\":null}"), TestCase("{\"id\":\"one\",\"web\":\"url\",\"download\":\"url\",\"views\":false}"), TestCase("{\"id\":\"one\",\"id\":\"two\"}"), TestCase("{\"id\":\"one\"}{}")]
        public void InvalidCreationDoesNotInventAUrlOrIdentity(string json)
        { var state = new State { Status = HttpStatusCode.Created, Json = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Plural(state).CreateConfiguredDocumentLinkAsync("context", "document"))); Assert.That(error.InnerException, Is.Not.Null); }
        [TestCase(false), TestCase(true)]
        public async Task SelectedUnknownMetadataStaysUnknownWithoutUrlCreatorOrCountFallback(bool upload)
        { var state = new State { Json = "{\"id\":\"selected\"}" }; if (upload) { var result = await new UploadSelected(state).GetUploadLinkDetailsAsync("context", "selected"); Assert.That(result.Web, Is.Null); Assert.That(result.Creator, Is.Null); Assert.That(result.Uploads, Is.Null); Assert.That(result.Tags, Is.Null); } else { var result = await new Selected(state).GetLinkDetailsAsync("context", "selected"); Assert.That(result.Web, Is.Null); Assert.That(result.Views, Is.Null); Assert.That(result.ViewOnly, Is.Null); } }
        [TestCase(false), TestCase(true)]
        public void AnotherSelectedIdentityIsRejected(bool upload)
        { var state = new State { Json = "{\"id\":\"other\"}" }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => { if (upload) await new UploadSelected(state).GetUploadLinkDetailsAsync("context", "selected"); else await new Selected(state).GetLinkDetailsAsync("context", "selected"); })); }
        [TestCase("{}"), TestCase("{\"upload-links\":null}"), TestCase("{\"upload-links\":[null]}"), TestCase("{\"upload-links\":[{\"id\":\"one\"},{\"id\":\"one\"}]}")]
        public void MissingOrDuplicateUploadInventoryIsNotAnEmptyList(string json)
        { var state = new State { Json = json }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new UploadPlural(state).GetUploadLinkDetailsAsync("context"))); }
        [Test]
        public async Task KnownEmptyUploadInventoryRemainsEmptyWithFutureEnvelopeFields()
        { var state = new State { Json = "{\"upload-links\":[],\"future\":false}" }; var result = await new UploadPlural(state).GetUploadLinkDetailsAsync("context"); Assert.That(result.Links, Is.Empty); Assert.That(result.GetRawDetails().Value<bool>("future"), Is.False); }
        [TestCase(403), TestCase(409), TestCase(500)]
        public void ServerErrorsDoNotInferExistingLinkPermissionsOrRetry(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Plural(state).CreateConfiguredDocumentLinkAsync("context", "document"))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6)]
        public async Task NativeCancellationReachesAuthorizationAndActiveIo(int operation)
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var task = Invoke(state, operation, source.Token); await state.Entered.Task; Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); Assert.That(state.Token, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); Assert.That(state.Calls, Is.EqualTo(1)); } }
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6)]
        public void PreCancellationDoesNotAuthorize(int operation)
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public void TypedNullOmissionAndNestedCopiesDoNotChangeCallerChoices()
        { var tags = new[] { "original" }; var options = new UploadLinkOptions { Tags = tags, MaxBytes = 1073741824 }; tags[0] = "changed"; Assert.That(options.Tags, Is.EqualTo(new[] { "original" })); options.MaxBytes = null; Assert.That(options["max-bytes"], Is.Null); var original = new JObject { ["future"] = new JObject { ["choice"] = false } }; var controls = new LinkAccessControlOptions(original); original["future"]["choice"] = true; Assert.That(controls["future"].Value<bool>("choice"), Is.False); }
        private static async Task Invoke(State state, int operation, CancellationToken token)
        { if (operation == 0) await new Plural(state).CreateConfiguredDocumentLinkAsync("context", "document", null, token); else if (operation == 1) await new Selected(state).GetLinkDetailsAsync("context", "selected", token); else if (operation == 2) await new Selected(state).ReplaceAccessControlAsync("context", "selected", new LinkAccessControlOptions(), token); else if (operation == 3) await new UploadPlural(state).CreateConfiguredUploadLinkAsync("context", null, token); else if (operation == 4) await new UploadPlural(state).GetUploadLinkDetailsAsync("context", token); else if (operation == 5) await new UploadSelected(state).UpdateConfiguredUploadLinkAsync("context", "selected", new UploadLinkOptions(), token); else await new UploadSelected(state).RemoveUploadLinkAsync("context", "selected", token); }
        private static JObject Body(State state) => Original((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private static JObject Original(string json) { using (var text = new StringReader(json)) using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/"; public string UserAgent => "isolated-complete-link-fixture"; }
        private sealed class Plural : LinksRestClient { private readonly State state; internal Plural(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class Selected : LinkRestClient { private readonly State state; internal Selected(State state) : base(state, new Config(), null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class UploadPlural : UploadLinksRestClient { private readonly State state; internal UploadPlural(State state) : base(state, new Config(), null, "v2/", "https://legacy.invalid/constructed/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class UploadSelected : UploadLinkRestClient { private readonly State state; internal UploadSelected(State state) : base(state, new Config(), null, "v2/", "https://legacy.invalid/constructed/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Json = "{}", Context; internal int Calls, Authorizations; internal bool Pending; internal Action Before; internal CancellationToken Token, AuthorizationToken; internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required."); public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; Before?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Request = request; Token = token; Calls++; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
