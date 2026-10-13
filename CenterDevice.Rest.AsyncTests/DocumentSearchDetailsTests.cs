using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
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
    public class DocumentSearchDetailsTests
    {
        [TestCase(false), TestCase(true)]
        public async Task CompleteTypedSearchPreservesNestedNamedParamsFiltersSortFacetsAndPaging(bool sync)
        {
            var tags = new[] { "Original", "Original" }; var named = new DocumentNamedSearchParameters { Name = "public-collections", Parameters = new JObject { ["include"] = false } };
            var filter = new DocumentSearchFilterParameters { Ids = new[] { "id" }, Filenames = new[] { "original.pdf" }, Extensions = new[] { "PDF" }, MimeTypes = new[] { "application/pdf" }, Titles = new[] { "title" }, Authors = new[] { "author" }, Owners = new[] { "owner" }, Tags = tags, Groups = new[] { "group" }, Collections = new[] { "collection" }, Hashes = new[] { "hash" }, LockedBy = new[] { "lock-owner" }, Locked = false, Folders = new DocumentSearchFolderParameters { Ids = new[] { "folder" }, Deep = false }, Size = new DocumentSearchSizeRange { From = -1, To = long.MaxValue }, UploadDate = new DocumentSearchDateRange { From = "2099-12-31T23:59:00+02:00" }, LastChange = new DocumentSearchDateRange { To = "2100-01-01T00:00:00Z" }, DocumentDate = new DocumentSearchDateRange { From = "2099-12-31" }, ArchivedDate = new DocumentSearchDateRange { To = "2100-01-01" }, ExtendedMetadata = new JObject { ["user.strings.missing"] = JValue.CreateNull(), ["user.numbers.amount"] = new JObject { ["from"] = "001.2500" } } };
            var parameters = new DocumentSearchParameters { Query = new DocumentTextSearchParameters { Text = " Original ", Scope = new JArray("filename", "future-scope") }, Filter = filter, Named = new[] { named }, Fields = new DocumentSearchFieldSelection { Includes = new[] { "id", "score", "workflows" } }, Offset = 5001, Rows = 501, Sort = new DocumentSearchSortParameters { Field = "extended-metadata", Order = "desc", Parameters = new JObject { ["key"] = "user.numbers.amount" } }, Facets = new[] { new DocumentSearchFacetParameters { Field = "extended-metadata", Size = 10, Qualifier = "original", Parameters = new JObject { ["section"] = "user.strings" } } }, DeepFolderFacets = false };
            tags[0] = "changed"; named.Parameters = new JObject { ["include"] = true }; filter.Locked = true; parameters.Query.Text = "changed";
            var state = new State { BeforeAuthorize = () => { parameters["named"][0]["params"]["include"] = true; parameters.Rows = 1; parameters["filter"]["upload-date"]["from"] = "changed"; } };
            var result = sync ? new Client(state).SearchDocumentDetails("context", parameters) : await new Client(state).SearchDocumentDetailsAsync("context", parameters);
            var body = Body(state); var sent = (JObject)body["params"]; Assert.That(body.Value<string>("action"), Is.EqualTo("search")); Assert.That(state.Request.Method, Is.EqualTo(Method.Post)); Assert.That(state.Request.Resource, Is.EqualTo("v2/documents")); Assert.That(sent["query"].Value<string>("text"), Is.EqualTo(" Original ")); Assert.That(sent["query"]["scope"].Values<string>(), Is.EqualTo(new[] { "filename", "future-scope" }));
            foreach(var key in new[] { "ids", "filenames", "extensions", "mimetypes", "titles", "authors", "owners", "tags", "groups", "collections", "hashes", "locked-by" }) Assert.That(sent["filter"][key], Is.TypeOf<JArray>(), key);
            Assert.That(sent["filter"]["tags"].Values<string>(), Is.EqualTo(new[] { "Original", "Original" })); Assert.That(sent["filter"].Value<bool>("locked"), Is.False); Assert.That(sent["filter"]["folders"].Value<bool>("deep"), Is.False); Assert.That(sent["filter"]["size"].Value<long>("from"), Is.EqualTo(-1)); Assert.That(sent["filter"]["size"].Value<long>("to"), Is.EqualTo(long.MaxValue)); Assert.That(sent["filter"]["upload-date"].Value<string>("from"), Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(sent["filter"]["extended-metadata"]["user.strings.missing"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That(sent["named"][0]["params"].Value<bool>("include"), Is.False); Assert.That(sent.Value<long>("offset"), Is.EqualTo(5001)); Assert.That(sent.Value<long>("rows"), Is.EqualTo(501)); Assert.That(sent["sort"]["params"].Value<string>("key"), Is.EqualTo("user.numbers.amount")); Assert.That(sent["facet"][0].Value<long>("size"), Is.EqualTo(10)); Assert.That(sent.Value<bool>("deep-folder-facets"), Is.False); Assert.That(result.Hits, Is.EqualTo("0")); Assert.That(result.Documents, Is.Empty); Assert.That(state.Context, Is.EqualTo("context"));
        }
        [Test]
        public async Task EmptySearchKeepsEveryDefaultOmittedAndLegacyNamedParamsRemainStrings()
        { var state = new State(); await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters()); Assert.That(((JObject)Body(state)["params"]).Properties(), Is.Empty); Assert.That(typeof(DocumentSearchNamed).GetProperty("Params").PropertyType, Is.EqualTo(typeof(string))); }
        [TestCase(null), TestCase(false), TestCase(true)]
        public async Task NullableBooleanChoicePreservesOmissionFalseAndTrue(bool? deep)
        { var state = new State(); await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters { DeepFolderFacets = deep }); var sent = (JObject)Body(state)["params"]; Assert.That(sent.Property("deep-folder-facets") != null, Is.EqualTo(deep.HasValue)); if(deep.HasValue) Assert.That(sent.Value<bool>("deep-folder-facets"), Is.EqualTo(deep.Value)); }
        [Test]
        public async Task NegationsMetadataOrUnitsAndFutureValuesRemainOriginal()
        {
            var parameters = new DocumentSearchParameters(OriginalObject("{\"query\":{\"text\":{\"not\":\" Original \"},\"scope\":\"filename\"},\"filter\":{\"tags\":{\"not\":[\"Original\"]},\"locked\":{\"not\":false},\"extended-metadata\":{\"or\":[{\"user.strings.type\":\"invoice\"},{\"user.numbers.amount\":{\"from\":\"001.2500\",\"to\":\"2.0000\"},\"user.strings.missing\":null}]}},\"named\":[{\"name\":\"workflow\",\"params\":{\"status\":\"future-status\",\"role\":\"future-role\"}},{\"name\":\"future-name\",\"params\":{\"future\":true}}],\"offset\":9223372036854775808,\"rows\":0,\"future\":false}"));
            var state = new State(); await new Client(state).SearchDocumentDetailsAsync("context", parameters); var sent = Body(state)["params"]; Assert.That(sent["query"]["text"].Value<string>("not"), Is.EqualTo(" Original ")); Assert.That(sent["filter"]["locked"].Value<bool>("not"), Is.False); Assert.That(sent["filter"]["extended-metadata"]["or"][1]["user.numbers.amount"].Value<string>("from"), Is.EqualTo("001.2500")); Assert.That(sent["offset"].ToString(), Is.EqualTo("9223372036854775808")); Assert.That(sent.Value<int>("rows"), Is.Zero); Assert.That(sent.Value<bool>("future"), Is.False);
        }
        [TestCase("public-collections", false), TestCase("public-collections", true), TestCase("archived", false), TestCase("archived", true)]
        public async Task ExplicitNamedInclusionDoesNotInferRoleOrArchiveVisibility(string name, bool include)
        { var state = new State(); await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters { Named = new[] { new DocumentNamedSearchParameters { Name = name, Parameters = new JObject { ["include"] = include } } } }); Assert.That(Body(state)["params"]["named"][0]["params"].Value<bool>("include"), Is.EqualTo(include)); }
        [TestCase("object"), TestCase("array")]
        public async Task OriginalFacetShapesRawPrecisionAndImmutableMetadataArePreserved(string shape)
        {
            var facets = shape == "object" ? "{\"owner.original\":{\"total\":2,\"other\":0,\"not-visible\":1,\"terms\":[{\"term\":\"owner\",\"count\":1}]}}" : "[{\"size\":{\"ranges\":[{\"name\":\"original\",\"from\":0,\"to\":1024,\"count\":0}]}}]";
            var state = new State { Json = "{\"hits\":9223372036854775808,\"documents\":[{\"id\":\"selected\",\"owner\":\"not-visible\",\"version-date\":\"2099-12-31T23:59:00+02:00\",\"score\":0.12345678901234567890123456789,\"workflows\":[\"creator-started\",\"future-workflow\"]}],\"facets\":" + facets + ",\"future\":false}" };
            var result = await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters()); Assert.That(result.Hits, Is.EqualTo("9223372036854775808")); Assert.That(result.Documents[0].Owner, Is.EqualTo("not-visible")); Assert.That(result.Documents[0].VersionDate, Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(result.Documents[0].GetRawDetails()["workflows"].Values<string>(), Is.EqualTo(new[] { "creator-started", "future-workflow" })); Assert.That(result.RawJson, Is.EqualTo(state.Json)); Assert.That(result.RawJson, Does.Contain("0.12345678901234567890123456789")); Assert.That(result.GetFacets().Type, Is.EqualTo(shape == "object" ? JTokenType.Object : JTokenType.Array)); result.GetRawDetails()["future"] = true; Assert.That(result.GetRawDetails().Value<bool>("future"), Is.False); Assert.Throws<NotSupportedException>((Action)(() => ((IList<DocumentDetails>)result.Documents).Clear()));
        }
        [TestCase(false), TestCase(true)]
        public async Task MetadataPageCopiesEveryQueryAndPreservesExclusiveUpperDateBound(bool sync)
        {
            var ids = new[] { "a", "a" }; var fields = new[] { "owner", "version-date" }; var options = new DocumentMetadataListingOptions { Ids = ids, Collection = "collection", Folder = "none", LastChangeFrom = "2099-12-31T23:59:00+02:00", LastChangeTo = "2100-01-01T00:00:00Z", IncludePublic = false, Offset = 4294967297L, Rows = 501, Includes = fields };
            var state = new State { Json = "{\"hits\":0,\"documents\":[]}", BeforeAuthorize = () => { ids[0] = "changed"; fields[0] = "id"; options.Rows = 1; options.LastChangeTo = "changed"; } }; var result = sync ? new Client(state).GetDocumentPage("context", options) : await new Client(state).GetDocumentPageAsync("context", options);
            var query = state.Request.Parameters.OfType<QueryParameter>().ToDictionary(p => p.Name, p => (string)p.Value); Assert.That(state.Request.Method, Is.EqualTo(Method.Get)); Assert.That(query["ids"], Is.EqualTo("a,a")); Assert.That(query["collection"], Is.EqualTo("collection")); Assert.That(query["folder"], Is.EqualTo("none")); Assert.That(query["last-change-from"], Is.EqualTo("2099-12-31T23:59:00+02:00")); Assert.That(query["last-change-to"], Is.EqualTo("2100-01-01T00:00:00Z")); Assert.That(query["include-public"], Is.EqualTo("false")); Assert.That(query["offset"], Is.EqualTo("4294967297")); Assert.That(query["rows"], Is.EqualTo("501")); Assert.That(query["includes"], Is.EqualTo("owner,version-date")); Assert.That(state.Request.Parameters.OfType<BodyParameter>(), Is.Empty); Assert.That(result.Documents, Is.Empty);
        }
        [Test]
        public async Task ExplicitIdExclusionKeepsUnknownIdentityAndDefaultMetadataQueriesOmitChoices()
        { var state = new State { Json = "{\"hits\":1,\"documents\":[{\"owner\":\"not-visible\"}]}" }; var result = await new Client(state).GetDocumentPageAsync("context", new DocumentMetadataListingOptions { Excludes = new[] { "id" } }); Assert.That(result.Documents[0].Id, Is.Null); state.Json = "{\"hits\":0,\"documents\":[]}"; await new Client(state).GetDocumentPageAsync("context"); Assert.That(state.Request.Parameters.OfType<QueryParameter>(), Is.Empty); }
        [TestCase("{\"query\":null}"), TestCase("{\"query\":{\"text\":0}}"), TestCase("{\"query\":{\"text\":{\"not\":\"text\",\"extra\":true}}}"), TestCase("{\"query\":{\"scope\":[false]}}"), TestCase("{\"filter\":{\"tags\":null}}"), TestCase("{\"filter\":{\"locked\":0}}"), TestCase("{\"filter\":{\"size\":{\"from\":1.5}}}"), TestCase("{\"filter\":{\"size\":{\"from\":9223372036854775808}}}"), TestCase("{\"filter\":{\"upload-date\":{\"from\":0}}}"), TestCase("{\"filter\":{\"extended-metadata\":{\"or\":[],\"another\":\"value\"}}}"), TestCase("{\"named\":[{\"name\":\"public-collections\"}]}"), TestCase("{\"named\":[{\"name\":\"workflow\",\"params\":{\"status\":\"started\"}}]}"), TestCase("{\"named\":[{\"name\":\"future\",\"params\":\"legacy-string\"}]}"), TestCase("{\"fields\":{\"includes\":[],\"excludes\":[]}}"), TestCase("{\"fields\":{\"excludes\":[\"base\"]}}"), TestCase("{\"offset\":-1}"), TestCase("{\"rows\":-1}"), TestCase("{\"rows\":0.5}"), TestCase("{\"sort\":{\"field\":\"extended-metadata\"}}"), TestCase("{\"facet\":[{\"field\":\"tags\",\"size\":0}]}"), TestCase("{\"facet\":[null]}"), TestCase("{\"deep-folder-facets\":null}")]
        public void InvalidKnownSearchShapesFailBeforeAuthorization(string json)
        { var state = new State(); Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters(OriginalObject(json))))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase(""), TestCase("{}"), TestCase("[]"), TestCase("{\"hits\":-1}"), TestCase("{\"hits\":\"0\"}"), TestCase("{\"hits\":0,\"documents\":null}"), TestCase("{\"hits\":1,\"documents\":[{}]}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":\"id\",\"score\":false}]}"), TestCase("{\"hits\":1,\"documents\":[{\"id\":\"id\",\"workflows\":[null]}]}"), TestCase("{\"hits\":0,\"facets\":false}"), TestCase("{\"hits\":0,\"hits\":1}"), TestCase("{\"hits\":0}{}")]
        public void MalformedSuccessRetainsItsInnerDiagnostic(string json)
        { var state = new State { Json = json }; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters()))); Assert.That(error.InnerException, Is.Not.Null); }
        [Test]
        public void MetadataPageRequiresAnArrayEvenWhenItsTotalIsZero()
        { var state = new State { Json = "{\"hits\":0}" }; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await new Client(state).GetDocumentPageAsync("context"))); }
        [TestCase("offset"), TestCase("rows"), TestCase("ids"), TestCase("fields"), TestCase("both")]
        public void InvalidMetadataPageSelectionsDoNotAuthorize(string kind)
        { var options = new DocumentMetadataListingOptions(); if(kind == "offset") options.Offset = -1; if(kind == "rows") options.Rows = 0; if(kind == "ids") options.Ids = new[] { "a,b" }; if(kind == "fields") options.Excludes = new[] { "base" }; if(kind == "both") { options.Includes = new[] { "id" }; options.Excludes = new[] { "owner" }; } var state = new State(); Assert.CatchAsync<ArgumentException>((Func<Task>)(async () => await new Client(state).GetDocumentPageAsync("context", options))); Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("search", 204), TestCase("search", 403), TestCase("metadata", 404), TestCase("metadata", 500)]
        public void OriginalErrorsDoNotWidenFiltersChangeOffsetsOrInferVisibility(string operation, int status)
        { var state = new State { Status = (HttpStatusCode)status }; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await Invoke(state, operation))); Assert.That(state.Calls, Is.EqualTo(1)); }
        [TestCase("search"), TestCase("metadata")]
        public void PreCancellationDoesNotAuthorize(string operation)
        { var state = new State(); using(var source = new CancellationTokenSource()) { source.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => await Invoke(state, operation, source.Token))); } Assert.That(state.Authorizations, Is.Zero); }
        [TestCase("search"), TestCase("metadata")]
        public async Task NativeCancellationReachesAuthorizationAndActiveHttp(string operation)
        { var state = new State { Pending = true }; using(var source = new CancellationTokenSource()) { var task = Invoke(state, operation, source.Token); await state.Entered.Task; Assert.That(state.Token, Is.EqualTo(source.Token)); Assert.That(state.AuthorizationToken, Is.EqualTo(source.Token)); source.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); } }
        private static async Task Invoke(State state, string operation, CancellationToken token = default(CancellationToken)) { if(operation == "search") await new Client(state).SearchDocumentDetailsAsync("context", new DocumentSearchParameters(), token); else await new Client(state).GetDocumentPageAsync("context", cancellationToken: token); }
        private static JObject OriginalObject(string json) { using(var source = new StringReader(json)) using(var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private static JObject Body(State state) => OriginalObject((string)state.Request.Parameters.OfType<BodyParameter>().Single().Value);
        private sealed class Config : IRestClientConfiguration { public string BaseAddress => "https://unused.invalid/bridge/"; public string UserAgent => "isolated-search-details-tests"; }
        private sealed class Client : DocumentsRestClient { private readonly State state; internal Client(State state) : base(state, new Config(), null, null, "v2/") { this.state = state; } protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => state.Send(request, token); }
        private sealed class State : IAsyncOAuthInfoProvider
        {
            internal RestRequest Request; internal HttpStatusCode Status = HttpStatusCode.OK; internal string Json, Context; internal int Calls, Authorizations; internal bool Pending; internal Action BeforeAuthorize; internal CancellationToken Token, AuthorizationToken;
            internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Authorizations++; Context = userId; AuthorizationToken = token; BeforeAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); }
            internal async Task<RestResponse> Send(RestRequest request, CancellationToken token) { Calls++; Request = request; Token = token; Entered.TrySetResult(true); if(Pending) await Task.Delay(Timeout.Infinite, token); return new RestResponse(request) { StatusCode = Status, Content = Json ?? (request.Method == Method.Get ? "{\"hits\":0,\"documents\":[]}" : "{\"hits\":0}") }; }
        }
    }
}
