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
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class ConfiguredUploadTests
    {
        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task AllNewDocumentFormsKeepStreamingFramingOriginalMetadataAndPartialOutcomes(bool fromFile, bool synchronous)
        {
            using (var fixture = new Fixture())
            {
                var options = CompleteOptions(); fixture.Transport.Json = "{\"id\":\"created\",\"upload-actions\":{\"add-to-folder\":{\"failed\":[\"missing\",\"missing\"]},\"future\":false}}";
                fixture.Authorization.Before = () => options["document"]["title"] = "changed";
                DocumentUploadResult result;
                if (fromFile) result = synchronous ? fixture.Documents.UploadConfiguredDocument("context", "ö original.bin", fixture.Path, options) : await fixture.Documents.UploadConfiguredDocumentAsync("context", "ö original.bin", fixture.Path, options);
                else result = synchronous ? fixture.Documents.UploadConfiguredDocument("context", "ö original.bin", fixture.Open, options) : await fixture.Documents.UploadConfiguredDocumentAsync("context", "ö original.bin", fixture.Open, options);
                Assert.That(fixture.Transport.PartNames, Is.EqualTo(new[] { "metadata", "document" })); Assert.That(fixture.Transport.Chunked, Is.True); Assert.That(fixture.Transport.Bytes, Is.EqualTo(Fixture.Payload)); Assert.That(fixture.Transport.Calls, Is.EqualTo(1)); Assert.That(fixture.Transport.Route, Is.EqualTo("/v2/documents"));
                var metadata = Original(fixture.Transport.Metadata)["metadata"];
                Assert.That(metadata["document"].Value<string>("filename"), Is.EqualTo("ö original.bin")); Assert.That(metadata["document"].Value<long>("size"), Is.EqualTo(Fixture.Payload.Length)); Assert.That(metadata["document"].Value<string>("title"), Is.EqualTo("Original Title")); Assert.That(metadata["document"].Value<string>("document-date"), Is.EqualTo("2025-04-05T06:07:08.1234567+05:30"));
                Assert.That(metadata["document"].Value<string>("author"), Is.EqualTo("Original Author")); Assert.That(metadata.Value<bool>("require-valid-extended-metadata"), Is.False); Assert.That(metadata["extended-metadata"]["user"]["numbers"].Value<string>("price"), Is.EqualTo("12345678901234567890.123456789")); Assert.That(metadata["actions"]["share"]["emails"].Values<string>(), Is.EqualTo(new[] { "Original@Example.invalid" }));
                Assert.That(metadata["actions"]["add-to-collection"].Values<string>(), Is.EqualTo(new[] { "collection" })); Assert.That(metadata["actions"]["add-to-folder"].Values<string>(), Is.EqualTo(new[] { "folder", "missing" })); Assert.That(metadata["actions"]["add-tag"].Values<string>(), Is.EqualTo(new[] { "Tag", "Tag" })); Assert.That(metadata["actions"]["add-lock"].Values<string>(), Is.EqualTo(new[] { "future-lock" })); Assert.That(metadata["actions"]["share"]["users"].Values<string>(), Is.EqualTo(new[] { "user" })); Assert.That(metadata["actions"]["share"]["groups"].Values<string>(), Is.EqualTo(new[] { "group" }));
                Assert.That(metadata["actions"].Value<bool>("extract-archive"), Is.False); Assert.That(metadata["actions"]["email-processing"].Value<bool>("attachment-text-indexing"), Is.False); Assert.That(metadata["actions"]["check-for-duplicate"].Value<bool>("allow-different-filenames"), Is.False); Assert.That(metadata["actions"]["check-for-duplicate"].Value<bool>("allow-different-owners"), Is.True); Assert.That(metadata["future"].Value<string>("value"), Is.EqualTo("retained"));
                Assert.That(result.Id, Is.EqualTo("created")); Assert.That(result.Version, Is.Null); Assert.That(result.DuplicateDetected, Is.False); Assert.That(result.Duplicates, Is.Null); Assert.That(result.FailedFolders, Is.EqualTo(new[] { "missing", "missing" })); result.GetRawDetails()["id"] = "mutated"; Assert.That(result.Id, Is.EqualTo("created")); Assert.That(result.GetRawDetails()["upload-actions"].Value<bool>("future"), Is.False); Assert.That(result.RawJson, Is.EqualTo(fixture.Transport.Json));
                if (!fromFile) { Assert.That(fixture.FactoryCalls, Is.EqualTo(1)); Assert.That(fixture.Stream.Disposals, Is.EqualTo(1)); }
            }
        }
        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task VersionFormsUseExplicitIdentityAndOriginalWideVersionWithoutActions(bool fromFile, bool synchronous)
        {
            using (var fixture = new Fixture())
            {
                fixture.Transport.Json = "{\"version\":\"18446744073709551616\",\"future\":false}"; var options = new DocumentUploadOptions { Document = new DocumentUploadMetadata { Author = "Author", DocumentDate = "2025-04-05T06:07:08+05:30" }, ExtendedMetadata = new JObject { ["user"] = new JObject { ["value"] = "original" } } };
                DocumentUploadResult result;
                if (fromFile) result = synchronous ? fixture.Document.UploadConfiguredVersion("context", "selected/ö ?", "file.bin", fixture.Path, options) : await fixture.Document.UploadConfiguredVersionAsync("context", "selected/ö ?", "file.bin", fixture.Path, options);
                else result = synchronous ? fixture.Document.UploadConfiguredVersion("context", "selected/ö ?", "file.bin", fixture.Open, options) : await fixture.Document.UploadConfiguredVersionAsync("context", "selected/ö ?", "file.bin", fixture.Open, options);
                Assert.That(fixture.Transport.Route, Is.EqualTo("/v2/document/selected%2F%C3%B6%20%3F").IgnoreCase); Assert.That(result.Version, Is.EqualTo("18446744073709551616")); Assert.That(result.Id, Is.Null); Assert.That(result.DuplicateDetected, Is.False); Assert.That(Original(fixture.Transport.Metadata)["metadata"]["actions"], Is.Null); Assert.That(fixture.Transport.PartNames, Is.EqualTo(new[] { "metadata", "document" })); Assert.That(fixture.Transport.Calls, Is.EqualTo(1)); if (!fromFile) Assert.That(fixture.Stream.Disposals, Is.EqualTo(1));
            }
        }
        [Test]
        public async Task OmittedChoicesDoNotInsertDatesProcessingLocationsSharingOrDuplicateChecks()
        { using (var fixture = new Fixture()) { var result = await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open); var metadata = (JObject)Original(fixture.Transport.Metadata)["metadata"]; Assert.That(metadata.Properties().Select(p => p.Name), Is.EqualTo(new[] { "document" })); Assert.That(((JObject)metadata["document"]).Properties().Select(p => p.Name), Is.EqualTo(new[] { "filename", "size" })); Assert.That(result.FailedFolders, Is.Null); Assert.That(fixture.Transport.DocumentContentType, Is.EqualTo("application/octet-stream")); } }
        [TestCase("{\"actions\":{\"check-for-duplicate\":{}}}"), TestCase("{\"actions\":{\"check-for-duplicate\":{\"allow-different-owners\":false}}}")]
        public async Task ExplicitDuplicateDetectionRetainsAllDuplicatesAndDoesNotCreateOrReplay(string json)
        { using (var fixture = new Fixture()) { fixture.Transport.Status = HttpStatusCode.Conflict; fixture.Transport.Json = "{\"duplicates\":[\"first\",\"second\",\"first\"],\"future\":false}"; var result = await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, new DocumentUploadOptions(Original(json))); Assert.That(result.DuplicateDetected, Is.True); Assert.That(result.Id, Is.Null); Assert.That(result.Duplicates, Is.EqualTo(new[] { "first", "second", "first" })); Assert.That(fixture.Transport.Calls, Is.EqualTo(1)); Assert.That(fixture.Stream.Disposals, Is.EqualTo(1)); } }
        [TestCase(403), TestCase(409), TestCase(500)]
        public void UnrequestedConflictsAndOtherServerErrorsRemainErrorsWithoutReplay(int status)
        { using (var fixture = new Fixture()) { fixture.Transport.Status = (HttpStatusCode)status; fixture.Transport.Json = "{\"duplicates\":[\"existing\"]}"; Assert.CatchAsync<RestClientException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open))); Assert.That(fixture.Transport.Calls, Is.EqualTo(1)); Assert.That(fixture.Stream.Disposals, Is.EqualTo(1)); } }
        [TestCase("{\"id\":\"\"}"), TestCase("{}"), TestCase("{\"id\":2}"), TestCase("{\"id\":\"one\",\"id\":\"two\"}"), TestCase("{\"id\":\"one\"}{}"), TestCase("{\"id\":\"one\",\"upload-actions\":null}"), TestCase("{\"id\":\"one\",\"upload-actions\":{\"add-to-folder\":{\"failed\":[2]}}}")]
        public void MalformedCreatedResponsesRetainInnerDiagnosticsAndDispose(string json)
        { using (var fixture = new Fixture()) { fixture.Transport.Json = json; var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open))); Assert.That(error.InnerException, Is.Not.Null); Assert.That(fixture.Stream.Disposals, Is.EqualTo(1)); } }
        [TestCase("{}"), TestCase("{\"duplicates\":[]}"), TestCase("{\"duplicates\":null}"), TestCase("{\"duplicates\":[0]}"), TestCase("{\"duplicates\":[\" \"]}")]
        public void MalformedDuplicatesCannotMasqueradeAsSafeExistingDocuments(string json)
        { using (var fixture = new Fixture()) { fixture.Transport.Status = HttpStatusCode.Conflict; fixture.Transport.Json = json; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, new DocumentUploadOptions { Actions = new DocumentUploadActions { CheckForDuplicate = new DocumentUploadDuplicateCheck() } }))); } }
        [TestCase("{\"version\":0}"), TestCase("{\"version\":\"-1\"}"), TestCase("{\"version\":1.5}"), TestCase("{\"version\":null}"), TestCase("{}")]
        public void InvalidConfirmedVersionDoesNotInferTheNextVersion(string json)
        { using (var fixture = new Fixture()) { fixture.Transport.Json = json; Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () => await fixture.Document.UploadConfiguredVersionAsync("context", "selected", "file", fixture.Open))); } }
        [TestCase("{\"document\":null}"), TestCase("{\"document\":{\"filename\":\"override\"}}"), TestCase("{\"document\":{\"size\":1}}"), TestCase("{\"document\":{\"document-date\":5}}"), TestCase("{\"require-valid-extended-metadata\":null}"), TestCase("{\"actions\":null}"), TestCase("{\"actions\":{\"add-to-folder\":5}}"), TestCase("{\"actions\":{\"add-to-collection\":[false]}}"), TestCase("{\"actions\":{\"share\":{\"emails\":null}}}"), TestCase("{\"actions\":{\"check-for-duplicate\":false}}"), TestCase("{\"actions\":{\"extract-archive\":1}}"), TestCase("{\"actions\":{\"extract-archive\":{\"keep-folder-structure\":true},\"add-to-folder\":[\"one\",\"two\"]}}"), TestCase("{\"actions\":{\"email-processing\":{\"store-email\":\"none\"}}}"), TestCase("{\"actions\":{\"email-processing\":{\"store-email\":\"text\",\"attachment-text-indexing\":true}}}"), TestCase("{\"extended-metadata\":{\"user\":{\"numbers\":{\"number\":1.5}}}}")]
        public void InvalidOptionsFailBeforeAuthorizationAndStreamCreation(string json)
        { using (var fixture = new Fixture()) { Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, new DocumentUploadOptions(Original(json))))); Assert.That(fixture.FactoryCalls, Is.Zero); Assert.That(fixture.Authorization.Calls, Is.Zero); Assert.That(fixture.Transport.Calls, Is.Zero); } }
        [TestCase("{\"actions\":{}}"), TestCase("{\"actions\":{\"extract-archive\":false}}")]
        public void VersionUploadRejectsAllActionsBeforeOpeningOrAuthorization(string json)
        { using (var fixture = new Fixture()) { Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => await fixture.Document.UploadConfiguredVersionAsync("context", "selected", "file", fixture.Open, new DocumentUploadOptions(Original(json))))); Assert.That(fixture.FactoryCalls, Is.Zero); Assert.That(fixture.Authorization.Calls, Is.Zero); } }
        [Test]
        public void CommonNamespaceRemainsReadOnlyForUploads()
        { using (var fixture = new Fixture()) { Assert.ThrowsAsync<NotSupportedException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, new DocumentUploadOptions { ExtendedMetadata = new JObject { ["common"] = new JObject() } }))); Assert.That(fixture.FactoryCalls, Is.Zero); } }
        [TestCase("{\"actions\":{\"extract-archive\":true}}"), TestCase("{\"actions\":{\"extract-archive\":{\"keep-folder-structure\":true},\"add-to-folder\":\"selected\"}}"), TestCase("{\"actions\":{\"email-processing\":{\"store-email\":\"none\",\"extract-attachments\":true,\"attachment-text-indexing\":false}}}"), TestCase("{\"actions\":{\"email-processing\":{\"store-email\":\"future-value\",\"future\":false}}}")]
        public async Task ExplicitProcessingAndFutureSelectionsRemainUnchanged(string json)
        { using (var fixture = new Fixture()) { var options = new DocumentUploadOptions(Original(json)); await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, options); Assert.That(JToken.DeepEquals(Original(fixture.Transport.Metadata)["metadata"]["actions"], options["actions"]), Is.True); } }
        [Test]
        public void CopiesNestedInputsAndTypedNullRemovesOnlyTheSelectedChoice()
        {
            var document = new DocumentUploadMetadata { Title = "original" }; var share = new DocumentUploadSharing { Users = new[] { "selected" } }; var actions = new DocumentUploadActions { Share = share, EmailProcessing = new DocumentUploadEmailProcessing { StoreEmail = "original" }, CheckForDuplicate = new DocumentUploadDuplicateCheck { AllowDifferentOwners = false } }; var options = new DocumentUploadOptions { Document = document, Actions = actions, RequireValidExtendedMetadata = false }; document.Title = "changed"; share.Users = new[] { "changed" }; actions.Share = null;
            Assert.That(options.Document.Title, Is.EqualTo("original")); Assert.That(options.Actions.Share.Users, Is.EqualTo(new[] { "selected" })); options.Document.Title = "mutated copy"; Assert.That(options.Document.Title, Is.EqualTo("original")); options.RequireValidExtendedMetadata = null; Assert.That(options["require-valid-extended-metadata"], Is.Null); Assert.That(options.Actions.CheckForDuplicate.AllowDifferentOwners, Is.False);
        }
        [TestCase(false), TestCase(true)]
        public void PreCancellationDoesNotOpenAStreamOrAuthorize(bool version)
        { using (var fixture = new Fixture()) using (var cancellation = new CancellationTokenSource()) { cancellation.Cancel(); Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () => { if (version) await fixture.Document.UploadConfiguredVersionAsync("context", "selected", "file", fixture.Open, null, cancellation.Token); else await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, null, cancellation.Token); })); Assert.That(fixture.FactoryCalls, Is.Zero); Assert.That(fixture.Authorization.Calls, Is.Zero); } }
        [TestCase(false), TestCase(true)]
        public async Task ActiveCancellationDisposesOwnedContentWithoutReplaying(bool version)
        { using (var fixture = new Fixture()) using (var cancellation = new CancellationTokenSource()) { fixture.Transport.Pending = true; var task = version ? fixture.Document.UploadConfiguredVersionAsync("context", "selected", "file", fixture.Open, null, cancellation.Token) : fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, null, cancellation.Token); await fixture.Transport.Entered.Task; Assert.That(fixture.Authorization.Token, Is.EqualTo(cancellation.Token)); cancellation.Cancel(); Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => await task)); Assert.That(fixture.Stream.Disposals, Is.EqualTo(1)); Assert.That(fixture.Transport.Calls, Is.EqualTo(1)); } }
        [Test]
        public void AuthorizationFailureDisposesTheInspectedStream()
        { using (var fixture = new Fixture()) { fixture.Authorization.Before = () => throw new InvalidOperationException("authorization fixture"); Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open))); Assert.That(fixture.Stream.Disposals, Is.EqualTo(1)); Assert.That(fixture.Transport.Calls, Is.Zero); } }
        [TestCase(false), TestCase(true)]
        public void ExpectedDuplicateCannotHideAnotherTransportFailure(bool innerFailure)
        {
            var original = innerFailure ? new HttpRequestException("Request failed with status code Conflict", new IOException("uncertain transport")) : new HttpRequestException("uncertain transport");
            var client = new FailedResponseClient(original); var error = Assert.ThrowsAsync<RestClientException>((Func<Task>)(async () => await client.UploadConfiguredDocumentAsync("context", "file", () => new MemoryStream(Fixture.Payload), new DocumentUploadOptions { Actions = new DocumentUploadActions { CheckForDuplicate = new DocumentUploadDuplicateCheck() } })));
            Assert.That(error.InnerException, Is.SameAs(original));
        }
        [Test]
        public void DuplicateOutcomeStillHonorsTheOriginalConfiguredErrorHook()
        {
            var original = new InvalidOperationException("custom error hook"); var hook = new ErrorHook(original);
            var client = new FailedResponseClient(new HttpRequestException("Request failed with status code Conflict"), hook);
            Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await client.UploadConfiguredDocumentAsync("context", "file", () => new MemoryStream(Fixture.Payload), new DocumentUploadOptions { Actions = new DocumentUploadActions { CheckForDuplicate = new DocumentUploadDuplicateCheck() } })));
            Assert.That(hook.Calls, Is.EqualTo(1)); Assert.That(hook.Seen.ErrorException, Is.TypeOf<HttpRequestException>());
        }
        [TestCase(null), TestCase(""), TestCase(" ")]
        public void InvalidFilenameDoesNotOpenAStreamOrAuthorize(string filename)
        { using (var fixture = new Fixture()) { Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", filename, fixture.Open))); Assert.That(fixture.FactoryCalls, Is.Zero); Assert.That(fixture.Authorization.Calls, Is.Zero); } }
        [TestCase(null), TestCase(""), TestCase("."), TestCase("..")]
        public void InvalidVersionIdentityDoesNotOpenAStreamOrAuthorize(string id)
        { using (var fixture = new Fixture()) { Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await fixture.Document.UploadConfiguredVersionAsync("context", id, "file", fixture.Open))); Assert.That(fixture.FactoryCalls, Is.Zero); Assert.That(fixture.Authorization.Calls, Is.Zero); } }
        [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
        public async Task ExplicitMimeTypeIsASnapshottedDocumentPartHeaderNotMetadata(bool version, bool fromFile)
        {
            using (var fixture = new Fixture())
            {
                var options = new DocumentUploadOptions { MimeType = "application/x-original; charset=utf-8" }; Assert.That(new DocumentUploadOptions(options).MimeType, Is.EqualTo(options.MimeType)); fixture.Authorization.Before = () => options.MimeType = "text/plain"; if (version) fixture.Transport.Json = "{\"version\":2}";
                if (version) { if (fromFile) await fixture.Document.UploadConfiguredVersionAsync("context", "selected", "file", fixture.Path, options); else await fixture.Document.UploadConfiguredVersionAsync("context", "selected", "file", fixture.Open, options); }
                else { if (fromFile) await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Path, options); else await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, options); }
                Assert.That(fixture.Transport.DocumentContentType, Is.EqualTo("application/x-original; charset=utf-8")); Assert.That(Original(fixture.Transport.Metadata)["metadata"]["MimeType"], Is.Null); Assert.That(fixture.Transport.PartNames, Is.EqualTo(new[] { "metadata", "document" })); Assert.That(fixture.Transport.Calls, Is.EqualTo(1));
            }
        }
        [TestCase(""), TestCase(" "), TestCase("not a MIME"), TestCase("text/plain\r\nX: invalid")]
        public void InvalidMimeTypeFailsBeforeOpeningAndAuthorization(string mimeType)
        { using (var fixture = new Fixture()) { Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => await fixture.Documents.UploadConfiguredDocumentAsync("context", "file", fixture.Open, new DocumentUploadOptions { MimeType = mimeType }))); Assert.That(fixture.FactoryCalls, Is.Zero); Assert.That(fixture.Authorization.Calls, Is.Zero); } }
        private static DocumentUploadOptions CompleteOptions() => new DocumentUploadOptions
        {
            Document = new DocumentUploadMetadata { Title = "Original Title", Author = "Original Author", DocumentDate = "2025-04-05T06:07:08.1234567+05:30" }, RequireValidExtendedMetadata = false,
            ExtendedMetadata = new JObject { ["user"] = new JObject { ["numbers"] = new JObject { ["price"] = "12345678901234567890.123456789" } } }, ["future"] = new JObject { ["value"] = "retained" },
            Actions = new DocumentUploadActions { AddToCollections = new[] { "collection" }, AddToFolders = new JArray("folder", "missing"), Share = new DocumentUploadSharing { Users = new[] { "user" }, Emails = new[] { "Original@Example.invalid" }, Groups = new[] { "group" } }, AddTags = new[] { "Tag", "Tag" }, AddLocks = new[] { "future-lock" }, ExtractArchive = new JValue(false), EmailProcessing = new DocumentUploadEmailProcessing { StoreEmail = "original", ExtractAttachments = false, AttachmentTextIndexing = false }, CheckForDuplicate = new DocumentUploadDuplicateCheck { AllowDifferentFilenames = false, AllowDifferentOwners = true } }
        };
        private static JObject Original(string json) { using (var source = new StringReader(json)) using (var reader = new JsonTextReader(source) { DateParseHandling = DateParseHandling.None }) return JObject.Load(reader); }
        private sealed class Fixture : IDisposable
        {
            internal static readonly byte[] Payload = { 0, 17, 128, 255 }; internal readonly string Path = System.IO.Path.GetTempFileName(); internal readonly Capture Transport = new Capture(); internal readonly Authorization Authorization = new Authorization(); internal readonly DocumentsRestClient Documents; internal readonly DocumentRestClient Document; internal TrackedStream Stream; internal int FactoryCalls;
            private readonly HttpClient http; private readonly RestClient rest;
            internal Fixture() { File.WriteAllBytes(Path, Payload); var origin = "https://" + Guid.NewGuid().ToString("N") + ".invalid/"; http = CenterDeviceHttpTransport.CreateHttpClient(Transport); rest = new RestClient(http, new RestClientOptions(origin)); var config = new Config(origin); Documents = new DocumentsRestClient(Authorization, config, null, null, "v2/"); Document = new DocumentRestClient(Authorization, config, null, null, "v2/"); Replace(Documents); Replace(Document); }
            internal Stream Open() { FactoryCalls++; return Stream = new TrackedStream(Payload); }
            private void Replace(CenterDeviceRestClient client) { client.DisableOfflineModeSimulation(); var field = typeof(CenterDeviceRestClient).GetField("client", BindingFlags.Instance | BindingFlags.NonPublic); ((RestClient)field.GetValue(client)).Dispose(); field.SetValue(client, rest); }
            public void Dispose() { rest.Dispose(); http.Dispose(); File.Delete(Path); }
        }
        private sealed class Config : IRestClientConfiguration { internal Config(string origin) { BaseAddress = origin; } public string BaseAddress { get; } public string UserAgent => "isolated-configured-upload-fixture"; }
        private sealed class TrackedStream : MemoryStream { internal int Disposals; internal TrackedStream(byte[] payload) : base(payload, false) { } protected override void Dispose(bool disposing) { if (disposing) Disposals++; base.Dispose(disposing); } }
        private sealed class Authorization : IAsyncOAuthInfoProvider
        { internal Action Before; internal int Calls; internal CancellationToken Token; public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Native authorization is required."); public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken token = default(CancellationToken)) { Calls++; Token = token; Before?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "fixture" }); } }
        private sealed class ErrorHook : IRestClientErrorHandler
        { private readonly Exception failure; internal int Calls; internal RestResponse Seen; internal ErrorHook(Exception failure) { this.failure = failure; } public void ValidateResponse(RestResponse response) { Calls++; Seen = response; throw failure; } public OAuthInfo RefreshToken(OAuthInfo info) => throw new AssertionException("No token refresh expected."); }
        private sealed class FailedResponseClient : DocumentsRestClient
        {
            private readonly Exception failure;
            internal FailedResponseClient(Exception failure, IRestClientErrorHandler hook = null) : base(new Authorization(), new Config("https://unused.invalid/"), hook, null, "v2/") { this.failure = failure; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken)) => Task.FromResult(new RestResponse(request) { StatusCode = HttpStatusCode.Conflict, ResponseStatus = ResponseStatus.Error, ErrorException = failure, Content = "{\"duplicates\":[\"existing\"]}", Headers = new HeaderParameter[0] });
        }
        private sealed class Capture : HttpMessageHandler
        {
            internal int Calls; internal string Metadata, Route, DocumentContentType, Json = "{\"id\":\"created\"}"; internal string[] PartNames; internal byte[] Bytes; internal bool? Chunked; internal bool Pending; internal HttpStatusCode Status = HttpStatusCode.Created; internal readonly TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            { Calls++; Route = request.RequestUri.AbsolutePath; Chunked = request.Headers.TransferEncodingChunked; var parts = ((MultipartFormDataContent)request.Content).ToArray(); PartNames = parts.Select(p => p.Headers.ContentDisposition.Name.Trim('"')).ToArray(); Metadata = await parts[0].ReadAsStringAsync(); DocumentContentType = parts[1].Headers.ContentType.ToString(); Bytes = await parts[1].ReadAsByteArrayAsync(); Entered.TrySetResult(true); if (Pending) await Task.Delay(Timeout.Infinite, token); return new HttpResponseMessage(Status) { Content = new StringContent(Json, Encoding.UTF8, "application/json") }; }
        }
    }
}
