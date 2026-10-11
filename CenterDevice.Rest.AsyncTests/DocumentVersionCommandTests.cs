using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
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
    public class DocumentVersionCommandTests
    {
        [TestCase(false), TestCase(true)]
        public async Task RevertUsesTheSelectedDocumentAndNumericSourceVersionAndReturnsTheNewVersion(bool synchronous)
        {
            var authorization = new Authorization();
            var client = new Client(authorization);
            var result = synchronous ? client.RevertDocumentVersion("selected-user", "selected-document", 2147483650L)
                : await client.RevertDocumentVersionAsync("selected-user", "selected-document", 2147483650L);
            Assert.That(result.Version, Is.EqualTo(41));
            Assert.That(client.Request.Resource, Is.EqualTo("v2/document/selected-document"));
            Assert.That(client.Request.Method, Is.EqualTo(Method.Post));
            var body = Body(client.Request);
            Assert.That(body["action"].Value<string>(), Is.EqualTo("revert"));
            Assert.That(body["params"]["version"].Type, Is.EqualTo(JTokenType.Integer));
            Assert.That(body["params"]["version"].Value<long>(), Is.EqualTo(2147483650L));
            Assert.That(client.Info.UserId, Is.EqualTo("selected-user"));
            Assert.That(authorization.Calls, Is.EqualTo(1));
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [TestCase(false), TestCase(true)]
        public async Task RemovalUsesDeleteVersionsWithoutDeletingTheDocumentOrRenumberingTheSelection(bool synchronous)
        {
            var client = new Client(new Authorization());
            var versions = new long[] { 3, 2147483650L, 1 };
            if (synchronous) client.RemoveDocumentVersions("selected-user", "selected-document", versions);
            else await client.RemoveDocumentVersionsAsync("selected-user", "selected-document", versions);
            var body = Body(client.Request);
            Assert.That(client.Request.Resource, Is.EqualTo("v2/document/selected-document"));
            Assert.That(client.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(body["action"].Value<string>(), Is.EqualTo("delete-versions"));
            Assert.That(body["params"]["versions"].Values<long>(), Is.EqualTo(versions));
            Assert.That(body["params"]["versions"].All(v => v.Type == JTokenType.Integer), Is.True);
            Assert.That(client.Info.UserId, Is.EqualTo("selected-user"));
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task VersionSequenceIsEnumeratedOnceAndSnapshottedBeforeAuthorization()
        {
            var input = new List<long> { 1, 2 };
            int enumerations = 0;
            IEnumerable<long> Once()
            {
                if (++enumerations != 1) throw new AssertionException("The version sequence was enumerated again.");
                foreach (var version in input) yield return version;
            }
            var client = new Client(new Authorization { OnCall = input.Clear });
            await client.RemoveDocumentVersionsAsync("user", "document", Once());
            Assert.That(Body(client.Request)["params"]["versions"].Values<long>(), Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(enumerations, Is.EqualTo(1));
            Assert.That(input, Is.Empty);
        }

        [TestCase(0), TestCase(-1)]
        public void InvalidRevertVersionFailsBeforeAuthorization(long version)
        {
            var auth = new Authorization();
            var client = new Client(auth);
            Assert.ThrowsAsync<ArgumentOutOfRangeException>((Func<Task>)(async () => { await client.RevertDocumentVersionAsync("user", "document", version); }));
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [TestCase(null), TestCase(""), TestCase(" ")]
        public void MissingDocumentIdentityFailsBeforeAuthorization(string id)
        {
            var auth = new Authorization();
            var client = new Client(auth);
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await client.RevertDocumentVersionAsync("user", id, 1); }));
            Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await client.RemoveDocumentVersionsAsync("user", id, new long[] { 1 }); }));
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [Test]
        public void InvalidVersionSelectionFailsBeforeAuthorization()
        {
            var auth = new Authorization();
            var client = new Client(auth);
            Assert.ThrowsAsync<ArgumentNullException>((Func<Task>)(async () => { await client.RemoveDocumentVersionsAsync("user", "document", null); }));
            foreach (var versions in new[] { new long[0], new long[] { 0 }, new long[] { 1, -1 } })
                Assert.ThrowsAsync<ArgumentException>((Func<Task>)(async () => { await client.RemoveDocumentVersionsAsync("user", "document", versions); }));
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public void PreCancellationDoesNotEnumerateOrAuthorizeOrSend(bool revert)
        {
            var auth = new Authorization();
            var client = new Client(auth);
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () =>
                {
                    if (revert) await client.RevertDocumentVersionAsync("user", "document", 1, cancel.Token);
                    else await client.RemoveDocumentVersionsAsync("user", "document", new ThrowingVersions(), cancel.Token);
                }));
            }
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(client.Calls, Is.Zero);
        }

        [TestCase(false), TestCase(true)]
        public async Task ActiveCancellationIsForwardedAndDoesNotReplayTheWrite(bool revert)
        {
            var auth = new Authorization();
            var client = new Client(auth) { Pending = true };
            using (var cancel = new CancellationTokenSource())
            {
                var operation = revert ? (Task)client.RevertDocumentVersionAsync("user", "document", 1, cancel.Token)
                    : client.RemoveDocumentVersionsAsync("user", "document", new long[] { 1 }, cancel.Token);
                await client.Entered.Task;
                Assert.That(client.Token, Is.EqualTo(cancel.Token));
                Assert.That(auth.Token, Is.EqualTo(cancel.Token));
                cancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>((Func<Task>)(async () => { await operation; }));
            }
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        [TestCase(false, 403), TestCase(true, 403), TestCase(false, 500), TestCase(true, 500)]
        public void PermissionOrServerFailuresRetainOriginalExceptionAndAreNotSuccessfulOrReplayed(bool revert, int status)
        {
            var original = new InvalidOperationException("isolated server failure");
            var client = new Client(new Authorization()) { Status = (HttpStatusCode)status, Error = original };
            var expected = status == 403 ? typeof(ForbiddenException) : typeof(InternalServerErrorException);
            var error = Assert.ThrowsAsync(expected, (Func<Task>)(async () =>
            {
                if (revert) await client.RevertDocumentVersionAsync("user", "document", 1);
                else await client.RemoveDocumentVersionsAsync("user", "document", new long[] { 1 });
            }));
            Assert.That(error.InnerException, Is.SameAs(original));
            Assert.That(client.Calls, Is.EqualTo(1));
        }

        private static JObject Body(RestRequest request)
            => JObject.FromObject(request.Parameters.First(p => p.Type == ParameterType.RequestBody).Value);

        private sealed class Configuration : IRestClientConfiguration
        {
            public string BaseAddress => "https://isolated-version.invalid/";
            public string UserAgent => "isolated-version-tests";
        }

        private sealed class ThrowingVersions : IEnumerable<long>
        {
            public IEnumerator<long> GetEnumerator() => throw new AssertionException("Cancelled removal enumerated input.");
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class Authorization : IAsyncOAuthInfoProvider
        {
            internal int Calls;
            internal CancellationToken Token;
            internal Action OnCall;
            public OAuthInfo GetOAuthInfo(string userId) => throw new AssertionException("Synchronous authorization was used.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string userId, CancellationToken cancellationToken = default(CancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                Calls++; Token = cancellationToken; OnCall?.Invoke();
                return Task.FromResult(new OAuthInfo { UserId = userId, access_token = "isolated-version-token" });
            }
        }

        private sealed class Client : DocumentRestClient
        {
            internal int Calls;
            internal RestRequest Request;
            internal OAuthInfo Info;
            internal CancellationToken Token;
            internal bool Pending;
            internal HttpStatusCode? Status;
            internal Exception Error;
            internal TaskCompletionSource<bool> Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Client(Authorization auth) : base(auth, new Configuration(), null, null, "v2/") { }
            protected override async Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                Capture(info, request, token);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse<T>(request) { StatusCode = Status ?? HttpStatusCode.Created, ErrorException = Error, Data = (T)(object)new NewVersionUploadResponse { Version = 41 } };
            }
            protected override async Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                Capture(info, request, token);
                if (Pending) await Task.Delay(Timeout.Infinite, token);
                return new RestResponse { StatusCode = Status ?? HttpStatusCode.NoContent, ErrorException = Error };
            }
            private void Capture(OAuthInfo info, RestRequest request, CancellationToken token)
            { Calls++; Info = info; Request = request; Token = token; Entered.TrySetResult(true); }
        }
    }
}
