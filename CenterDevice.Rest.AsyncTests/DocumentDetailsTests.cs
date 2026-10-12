using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
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
    public class DocumentDetailsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task SelectedIdVersionAndIncludesAreCopiedAndNative(bool sync)
        {
            var fields = new[] { "owner", "locks" }; var state = new State { Json = "{\"id\":\"part/with?query\",\"version\":4294967297}", BeforeAuthorize = () => fields[0] = "base" };
            var result = sync ? new Client(state).GetDocumentDetails("context", "part/with?query", 4294967297L, fields) : await new Client(state).GetDocumentDetailsAsync("context", "part/with?query", 4294967297L, fields);
            Assert.That(state.Request.Resource, Is.EqualTo("v2/document/part%2Fwith%3Fquery;version=4294967297")); Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(state.Request.Parameters.OfType<QueryParameter>().Single().Value, Is.EqualTo("owner,locks")); Assert.That(state.Request.Parameters.OfType<HeaderParameter>().Single(value => value.Name == "Accept").Value, Is.EqualTo("application/json")); Assert.That(state.Context, Is.EqualTo("context")); Assert.That(result.Version, Is.EqualTo("4294967297")); Assert.That(result.Owner, Is.Null); Assert.That(result.Locks, Is.Null); Assert.That(result.Size, Is.Null);
        }
        [Test]
        public async Task DefaultLatestMetadataDoesNotInsertFieldsOrVersion()
        { var state = new State(); var result = await new Client(state).GetDocumentDetailsAsync("context", "selected"); Assert.That(state.Request.Resource, Is.EqualTo("v2/document/selected")); Assert.That(state.Request.Parameters.OfType<QueryParameter>(), Is.Empty); Assert.That(result.Version, Is.Null); Assert.That(result.DocumentDate, Is.Null); Assert.That(result.LockedBy, Is.Null); }
        [Test]
        public async Task ExplicitIdExclusionPermitsMissingIdAndIsCopiedBeforeAuthorization()
        { var fields = new[] { "id", "representations" }; var state = new State { Json = "{}", BeforeAuthorize = () => fields[0] = "owner" }; var result = await new Client(state).GetDocumentDetailsAsync("context", "selected", excludes: fields); Assert.That(result.Id, Is.Null); Assert.That(state.Request.Parameters.OfType<QueryParameter>().Single().Value, Is.EqualTo("id,representations")); }
        [Test]
        public async Task AllMetadataRetainsOriginalDatesHiddenIdentitiesCatalogsAndImmutableCopies()
        {
            var state = new State { Json = "{\"id\":\"selected\",\"owner\":\"not-visible\",\"uploader\":\"not-visible\",\"filename\":\"name.pdf\",\"mimetype\":\"application/pdf\",\"title\":\"title\",\"author\":\"author\",\"upload-date\":\"2099-12-31T23:59:00+02:00\",\"version-date\":\"2099-12-31T23:59:00+02:00\",\"document-date\":\"2099-12-31T23:59:00+02:00\",\"archived-date\":\"2099-12-31T23:59:00+02:00\",\"deletion-date\":\"2099-12-31T23:59:00+02:00\",\"version\":1,\"size\":9223372036854775808,\"pages\":0,\"comments\":0,\"tags\":[\"a\",\"a\"],\"locks\":[\"create-new-version\",\"future-lock\"],\"locked-by\":\"lock-owner\",\"folders\":[\"f\"],\"link\":\"link\",\"hash\":\"md5\",\"extension\":\"pdf\",\"users\":{\"visible\":[],\"not-visible-count\":0},\"groups\":{\"visible\":[\"g\"],\"not-visible-count\":2},\"collections\":{\"visible\":[\"c\"],\"not-visible-count\":1},\"representations\":{\"pdf\":\"yes\",\"jpg\":\"supported\",\"mp4\":\"no\",\"future\":\"future-state\"},\"archived-dates\":[{\"collection\":\"c\",\"archived-date\":\"2099-12-31T23:59:00+02:00\"}],\"version-details\":[{\"version\":1,\"uploader\":\"not-visible\",\"filename\":\"old.pdf\",\"size\":0,\"version-date\":\"2099-12-31T23:59:00+02:00\",\"mimetype\":\"application/pdf\",\"title\":\"old title\",\"author\":\"old author\",\"pages\":1}],\"extended-metadata\":{\"user\":{\"numbers\":{\"original\":\"001.2500\"},\"dates\":{\"date\":\"2099-12-31T23:59:00+02:00\"}}},\"indirect-sharings\":{\"indirect-users\":[\"u\"],\"future\":true},\"future\":{\"date\":\"2099-12-31\"}}" };
            var result = await new Client(state).GetDocumentDetailsAsync("context", "selected");
            Assert.That(result.Owner, Is.EqualTo("not-visible")); Assert.That(result.Uploader, Is.EqualTo("not-visible")); Assert.That(result.Filename, Is.EqualTo("name.pdf")); Assert.That(result.MimeType, Is.EqualTo("application/pdf")); Assert.That(result.Title, Is.EqualTo("title")); Assert.That(result.Author, Is.EqualTo("author")); Assert.That(result.UploadDate, Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(result.VersionDate, Is.EqualTo(result.UploadDate)); Assert.That(result.DocumentDate, Is.EqualTo(result.UploadDate)); Assert.That(result.ArchivedDate, Is.EqualTo(result.UploadDate)); Assert.That(result.DeletionDate, Is.EqualTo(result.UploadDate));
            Assert.That(result.Size, Is.EqualTo("9223372036854775808")); Assert.That(result.Pages, Is.EqualTo("0")); Assert.That(result.Comments, Is.EqualTo("0")); Assert.That(result.Tags, Is.EqualTo(new[] { "a", "a" })); Assert.That(result.Locks, Is.EqualTo(new[] { "create-new-version", "future-lock" })); Assert.That(result.LockedBy, Is.EqualTo("lock-owner")); Assert.That(result.Folders, Is.EqualTo(new[] { "f" })); Assert.That(result.Link, Is.EqualTo("link")); Assert.That(result.Hash, Is.EqualTo("md5")); Assert.That(result.Extension, Is.EqualTo("pdf")); Assert.That(result.Users.Visible, Is.Empty); Assert.That(result.Users.NotVisibleCount, Is.EqualTo("0")); Assert.That(result.Groups.NotVisibleCount, Is.EqualTo("2")); Assert.That(result.Collections.Visible, Is.EqualTo(new[] { "c" }));
            Assert.That(result.GetRepresentations().Value<string>("jpg"), Is.EqualTo("supported")); Assert.That(result.GetRepresentations().Value<string>("future"), Is.EqualTo("future-state")); Assert.That(result.VersionDetails[0].Uploader, Is.EqualTo("not-visible")); Assert.That(result.VersionDetails[0].Filename, Is.EqualTo("old.pdf")); Assert.That(result.ArchivedDates[0].Collection, Is.EqualTo("c")); Assert.That(result.ArchivedDates[0].ArchivedDate, Is.EqualTo(result.UploadDate)); Assert.That(result.GetExtendedMetadata()["user"]["numbers"].Value<string>("original"), Is.EqualTo("001.2500")); Assert.That(result.GetIndirectSharings().Value<bool>("future"), Is.True); Assert.That(result.GetRawDetails()["future"]["date"].Type.ToString(), Is.EqualTo("String"));
            result.GetRawDetails()["owner"] = "changed"; result.GetRepresentations()["jpg"] = "yes"; result.GetExtendedMetadata()["user"]["numbers"]["original"] = "changed"; Assert.That(result.Owner, Is.EqualTo("not-visible")); Assert.That(result.GetRepresentations().Value<string>("jpg"), Is.EqualTo("supported")); Assert.That(result.GetExtendedMetadata()["user"]["numbers"].Value<string>("original"), Is.EqualTo("001.2500")); Assert.Throws<NotSupportedException>((Action)(() => ((IList<string>)result.Users.Visible).Add("changed")));
        }
        [Test]
        public async Task ExplicitNullAndEmptyMetadataPreserveUnknownVersusKnownEmpty()
        { var state = new State { Json = "{\"id\":\"selected\",\"version\":null,\"size\":null,\"users\":null,\"locks\":[],\"tags\":null,\"version-details\":[],\"extended-metadata\":null}" }; var result = await new Client(state).GetDocumentDetailsAsync("context", "selected"); Assert.That(result.Version, Is.Null); Assert.That(result.Size, Is.Null); Assert.That(result.Users, Is.Null); Assert.That(result.Locks, Is.Empty); Assert.That(result.Tags, Is.Null); Assert.That(result.VersionDetails, Is.Empty); Assert.That(result.GetExtendedMetadata(), Is.Null); Assert.That(result.GetRawDetails()["size"].Type.ToString(), Is.EqualTo("Null")); }
        [TestCase(""), TestCase("{}"), TestCase("[]"), TestCase("{\"id\":\"other\"}"), TestCase("{\"id\":\" \"}"), TestCase("{\"id\":5}"), TestCase("{\"id\":\"selected\",\"id\":\"other\"}"), TestCase("{\"id\":\"selected\"}{}")]
        [TestCase("{\"id\":\"selected\",\"version\":0}"), TestCase("{\"id\":\"selected\",\"version\":1.5}"), TestCase("{\"id\":\"selected\",\"size\":-1}"), TestCase("{\"id\":\"selected\",\"comments\":\"0\"}"), TestCase("{\"id\":\"selected\",\"owner\":false}"), TestCase("{\"id\":\"selected\",\"representations\":{\"pdf\":true}}"), TestCase("{\"id\":\"selected\",\"users\":[]}"), TestCase("{\"id\":\"selected\",\"groups\":{\"not-visible-count\":-1}}"), TestCase("{\"id\":\"selected\",\"locks\":[null]}"), TestCase("{\"id\":\"selected\",\"version-details\":[null]}"), TestCase("{\"id\":\"selected\",\"archived-dates\":[{\"archived-date\":0}]}"), TestCase("{\"id\":\"selected\",\"extended-metadata\":[]}")]
        public void InvalidSuccessRetainsItsDiagnosticRatherThanInventingDefaults(string json)
        { var state = new State { Json = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", "selected"))); Assert.That(error.InnerException, Is.Not.Null); }
        [Test]
        public void AReturnedDifferentRequestedVersionIsRejected()
        { var state = new State { Json = "{\"id\":\"selected\",\"version\":2}" }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", "selected", 1))); }
        [TestCase(null), TestCase(""), TestCase(" "), TestCase("."), TestCase("..")]
        public void InvalidSelectedIdDoesNotAuthorize(string id)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", id))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(0), TestCase(-1)]
        public void NonpositiveVersionsDoNotAuthorize(long version)
        { var state = new State(); Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", "selected", version))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("both"), TestCase("base"), TestCase("empty"), TestCase("comma")]
        public void InvalidFieldSelectionsDoNotAuthorize(string kind)
        { var state = new State(); var include = kind == "both" ? new[] { "id" } : kind == "empty" ? new string[0] : kind == "comma" ? new[] { "id,owner" } : null; var exclude = kind == "both" ? new[] { "owner" } : kind == "base" ? new[] { "base" } : null; Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", "selected", includes: include, excludes: exclude))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(204), TestCase(403), TestCase(404), TestCase(500)]
        public void OriginalErrorsDoNotInferPermissionOrEmptyMetadata(int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", "selected"))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [Test]
        public void PreCancellationDoesNotAuthorize()
        { var state = new State(); using (var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await new Client(state).GetDocumentDetailsAsync("context", "selected", cancellationToken: source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [Test]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttp()
        { var state = new State { Pending = true }; using (var source = new CancellationTokenSource()) { var task = new Client(state).GetDocumentDetailsAsync("context", "selected", cancellationToken: source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-document-details-tests"; }
        private sealed class Client : DocumentRestClient { private readonly State state; internal Client(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Json = "{\"id\":\"selected\"}", Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json }; }
        }
    }
}
