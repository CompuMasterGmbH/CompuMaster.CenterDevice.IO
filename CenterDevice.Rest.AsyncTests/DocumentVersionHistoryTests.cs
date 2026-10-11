using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using RestSharp;
using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.AsyncTests
{
    [TestFixture]
    public class DocumentVersionHistoryTests
    {
        private const string History = "{\"id\":\"selected-document\",\"version\":2147483650,\"version-details\":[{\"version\":2147483650,\"filename\":\"latest.txt\",\"uploader\":\"not-visible\",\"size\":5000000000,\"version-date\":\"2025-09-10T12:30:00Z\",\"mimetype\":\"text/plain\",\"title\":\"title\",\"author\":\"author\",\"pages\":1},{\"version\":3,\"filename\":\"old.txt\"}]}";

        [TestCase(false), TestCase(true)]
        public async Task HistoryRequestsSelectedIdentityAndOnlyHistoryFieldsWithoutRenumbering(bool sync)
        {
            var authorization = new Authorization();
            var client = new Client(authorization);
            var result = sync ? client.GetDocumentVersionHistory("selected-context", "selected-document")
                : await client.GetDocumentVersionHistoryAsync("selected-context", "selected-document");
            Assert.That(client.Request.Resource, Is.EqualTo("v2/document/selected-document"));
            Assert.That(client.Request.Method, Is.EqualTo(Method.Get));
            Assert.That(client.Request.Parameters.Single(p => p.Name == "includes").Value, Is.EqualTo("id,version,version-details"));
            Assert.That(client.Info.UserId, Is.EqualTo("selected-context"));
            Assert.That(result.Id, Is.EqualTo("selected-document"));
            Assert.That(result.Version, Is.EqualTo(2147483650L));
            Assert.That(result.VersionDetails.Select(v => v.Version), Is.EqualTo(new long?[] { 2147483650L, 3 }));
            var latest = result.VersionDetails[0];
            Assert.That(latest.Size, Is.EqualTo(5000000000L));
            Assert.That(latest.Uploader, Is.EqualTo("not-visible"));
            Assert.That(latest.VersionDate, Is.EqualTo(new DateTime(2025, 9, 10, 12, 30, 0, DateTimeKind.Utc)));
            Assert.That(latest.MimeType, Is.EqualTo("text/plain"));
            Assert.That(latest.Title, Is.EqualTo("title"));
            Assert.That(latest.Author, Is.EqualTo("author"));
            Assert.That(latest.Pages, Is.EqualTo(1));
            Assert.That(result.VersionDetails[1].Size, Is.Null);
            Assert.That(result.VersionDetails[1].VersionDate, Is.Null);
            Assert.That(result.VersionDetails[1].Pages, Is.Null);
            Assert.That(client.Calls, Is.EqualTo(1));
            Assert.That(authorization.Calls, Is.EqualTo(1));
        }

        [TestCase("{\"id\":\"guest-document\",\"version\":4}")]
        [TestCase("{\"id\":\"guest-document\",\"version\":4,\"version-details\":null}")]
        public async Task OmittedHistoryDoesNotBecomeAKnownEmptyHistory(string json)
        {
            var client = new Client(new Authorization()) { Json = json };
            var result = await client.GetDocumentVersionHistoryAsync("context", "guest-document");
            Assert.That(result.VersionDetails, Is.Null);
            Assert.That(result.Version, Is.EqualTo(4));
        }

        [Test]
        public async Task ExplicitEmptyDetailsArePreservedAndMissingVersionIsUnknown()
        {
            var client = new Client(new Authorization()) { Json = "{\"id\":\"document\",\"version-details\":[]}" };
            var result = await client.GetDocumentVersionHistoryAsync("context", "document");
            Assert.That(result.VersionDetails, Is.Empty);
            Assert.That(result.Version, Is.Null);
        }

        [TestCase(null), TestCase(""), TestCase(" ")]
        public void MissingIdentityFailsBeforeAuthorization(string id)
        {
            var authorization = new Authorization();
            var client = new Client(authorization);
            Assert.Throws<ArgumentException>((Action)(() => { client.GetDocumentVersionHistory("context", id); }));
            Assert.Throws<ArgumentException>((Action)(() => { client.GetDocumentVersionHistoryAsync("context", id); }));
            Assert.That(authorization.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [Test]
        public void PreCancellationDoesNotAuthorizeOrSend()
        {
            var authorization = new Authorization();
            var client = new Client(authorization);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Catch<OperationCanceledException>((Action)(() => { client.GetDocumentVersionHistoryAsync("context", "document", cancellation.Token); }));
            }
            Assert.That(authorization.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [Test]
        public async Task ActiveCancellationReachesAuthorizationAndTransport()
        {
            var authorization = new Authorization();
            var client = new Client(authorization) { Pending = true };
            using (var cancellation = new CancellationTokenSource())
            {
                var operation = client.GetDocumentVersionHistoryAsync("context", "document", cancellation.Token);
                await client.Entered.Task;
                Assert.That(client.Token, Is.EqualTo(cancellation.Token));
                Assert.That(authorization.Token, Is.EqualTo(cancellation.Token));
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await operation; }));
            }
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [TestCase(403), TestCase(404), TestCase(500)]
        public void VisibilityAndServerErrorsPreserveTheirOriginalException(int status)
        {
            var original = new InvalidOperationException("isolated error");
            var client = new Client(new Authorization()) { Status = (HttpStatusCode)status, Error = original };
            var expected = status == 403 ? typeof(ForbiddenException) : status == 404 ? typeof(NotFoundException) : typeof(InternalServerErrorException);
            var error = Assert.ThrowsAsync(expected, (Func<Task>)(async () => { await client.GetDocumentVersionHistoryAsync("context", "document"); }));
            Assert.That(error.InnerException, Is.SameAs(original));
        }

        [Test]
        public async Task ExistingFullMetadataIncludesRemainUnchanged()
        {
            var client = new Client(new Authorization());
            await client.GetDocumentMetadataAsync("context", "document");
            var includes = client.Request.Parameters.Single(p => p.Name == "includes").Value.ToString();
            Assert.That(includes.Split(','), Does.Not.Contain("version-details"));
        }

        private sealed class Config : IRestClientConfiguration
        {
            public string BaseAddress => "https://isolated-history.invalid/";
            public string UserAgent => "history-tests";
        }
        private sealed class Authorization : IAsyncOAuthInfoProvider
        {
            internal int Calls;
            internal CancellationToken Token;
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Synchronous authorization was called.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                Calls++; Token = cancellationToken;
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-token" });
            }
        }
        private sealed class Client : DocumentRestClient
        {
            internal Client(Authorization authorization) : base(authorization, new Config(), null, null, "v2/") { }
            internal string Json = History;
            internal RestRequest Request;
            internal OAuthInfo Info;
            internal CancellationToken Token;
            internal int Calls;
            internal bool Pending;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal Exception Error;
            internal TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override async Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken cancellationToken = default(CancellationToken))
            {
                Calls++; Info = info; Request = request; Token = cancellationToken; Entered.TrySetResult(true);
                if (Pending) await Task.Delay(Timeout.Infinite, cancellationToken);
                return new RestResponse<T>(request) { StatusCode = Status, ErrorException = Error, Data = JsonSerializer.Deserialize<T>(Json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
            }
        }
    }
}
