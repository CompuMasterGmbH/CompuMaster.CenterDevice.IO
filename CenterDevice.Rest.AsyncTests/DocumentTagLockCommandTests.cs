using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
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
    public class DocumentTagLockCommandTests
    {
        [TestCaseSource(nameof(WireCases))]
        public async Task SelectedTagAndLockCommandsPreserveWireValuesAndPartialFailures(string action, bool bulk, bool sync, bool partial)
        {
            var capture = new Capture { Status = partial ? HttpStatusCode.OK : HttpStatusCode.NoContent,
                Body = partial ? "{\"failed-documents\":[\"B\"]}" : null };
            var auth = new Auth();
            var docs = new Bulk(auth, capture);
            var doc = new Single(auth, capture);
            var values = action.EndsWith("lock") ? new[] { "create-new-version" } : new[] { " One ", "TWO", " ", "" };
            var result = await Execute(action, bulk, sync, doc, docs, bulk ? new[] { "A", "B" } : new[] { "B" }, values);
            var json = JObject.FromObject(capture.Request.Parameters.First(p => p.Type == ParameterType.RequestBody).Value);
            Assert.That(json["action"].Value<string>(), Is.EqualTo(action));
            Assert.That(json["params"][action.EndsWith("lock") ? "locks" : "tags"].Values<string>().ToArray(),
                Is.EqualTo(action == "add-tag" ? new[] { "One", "TWO" } : values));
            if (bulk) Assert.That(json["params"]["documents"].Values<string>().ToArray(), Is.EqualTo(new[] { "A", "B" }));
            else Assert.That(json["params"]["documents"], Is.Null);
            Assert.That(capture.Request.Resource, Is.EqualTo(bulk ? "v2/documents" : "v2/document/B"));
            Assert.That(capture.Request.Method, Is.EqualTo(Method.Post));
            Assert.That(capture.UserId, Is.EqualTo("context"));
            Assert.That(result.AllSucceeded, Is.EqualTo(!partial));
            Assert.That(result.FailedDocuments, Is.EqualTo(partial ? new[] { "B" } : new string[0]));
            Assert.That(capture.Calls, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>((Action)(() => ((IList<string>)result.FailedDocuments).Add("forged")));
        }

        public static IEnumerable<TestCaseData> WireCases()
        {
            foreach (var action in new[] { "add-tag", "remove-tag", "add-lock", "remove-lock" })
                foreach (var bulk in new[] { false, true })
                    foreach (var sync in new[] { false, true })
                        foreach (var partial in new[] { false, true })
                            yield return new TestCaseData(action, bulk, sync, partial);
        }

        [TestCase(null), TestCase(""), TestCase("{}"), TestCase("{\"failed-documents\":[]}"),
         TestCase("{\"failed-documents\":null}"), TestCase("{\"failed-documents\":[null]}"),
         TestCase("{\"failed-documents\":[42]}"), TestCase("{\"failed-documents\":[\"foreign\"]}")]
        public void MalformedOrForeignPartialFailuresNeverBecomeCompleteSuccess(string body)
        {
            var capture = new Capture { Status = HttpStatusCode.OK, Body = body };
            var error = Assert.ThrowsAsync<InvalidResponseDataException>((Func<Task>)(async () =>
                await new Single(new Auth(), capture).AddTagsAsync("context", "selected", new[] { "tag" })));
            Assert.That(error.InnerException, Is.Not.Null);
            Assert.That(capture.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ReportedUuidSpellingRemainsExactEvenWhenTheServerCanonicalizesIt()
        {
            var id = "9E91DAD2-0820-4BC0-BF37-D923191CE44D";
            var canonical = id.ToLowerInvariant();
            var capture = new Capture { Status = HttpStatusCode.OK, Body = "{\"failed-documents\":[\"" + canonical + "\"]}" };
            var result = await new Single(new Auth(), capture).AddTagsAsync("context", id, new[] { "tag" });
            Assert.That(result.FailedDocuments.Single(), Is.EqualTo(canonical));
        }

        [TestCase("null-ids"), TestCase("empty-ids"), TestCase("blank-id"), TestCase("null-values"), TestCase("empty-values"),
         TestCase("null-tag"), TestCase("unknown-lock"), TestCase("blank-single"), TestCase("relative-single")]
        public void InvalidSequencesAndUnsupportedLocksFailBeforeAuthorization(string invalid)
        {
            var capture = new Capture();
            var auth = new Auth();
            var expected = invalid.StartsWith("null-") && invalid != "null-tag" ? typeof(ArgumentNullException)
                : invalid == "unknown-lock" ? typeof(NotSupportedException) : typeof(ArgumentException);
            Assert.ThrowsAsync(expected, (Func<Task>)(async () =>
            {
                var values = invalid == "null-values" ? null : invalid == "empty-values" ? new string[0]
                    : new[] { invalid == "null-tag" ? null : "tag" };
                if (invalid == "unknown-lock")
                    await new Single(auth, capture).AddLocksAsync("context", "id", new[] { "made-up-lock" });
                else if (invalid.EndsWith("single"))
                    await new Single(auth, capture).AddTagsAsync("context", invalid == "blank-single" ? " " : "..", values);
                else
                    await new Bulk(auth, capture).AddTagsAsync("context", invalid == "null-ids" ? null : invalid == "empty-ids" ? new string[0]
                        : new[] { invalid == "blank-id" ? " " : "id" }, values);
            }));
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(capture.Calls, Is.Zero);
        }

        [Test]
        public async Task BothSequencesAreSnapshottedAndTagNormalizationPrecedesAuthorization()
        {
            var ids = new List<string> { "original" };
            var tags = new List<string> { " Case ", "CASE", " " };
            int idEnumerations = 0, tagEnumerations = 0;
            IEnumerable<string> Selected() { idEnumerations++; foreach (var id in ids) yield return id; }
            IEnumerable<string> Values() { tagEnumerations++; foreach (var tag in tags) yield return tag; }
            var auth = new Auth { OnAuthorize = () => { ids.Add("later"); tags.Add("later"); } };
            var capture = new Capture();
            await new Bulk(auth, capture).AddTagsAsync("context", Selected(), Values());
            var json = JObject.FromObject(capture.Request.Parameters.First(p => p.Type == ParameterType.RequestBody).Value);
            Assert.That(json["params"]["documents"].Values<string>().ToArray(), Is.EqualTo(new[] { "original" }));
            Assert.That(json["params"]["tags"].Values<string>().ToArray(), Is.EqualTo(new[] { "Case" }));
            Assert.That(idEnumerations, Is.EqualTo(1));
            Assert.That(tagEnumerations, Is.EqualTo(1));
        }

        [Test]
        public async Task IgnoredTagValuesStillUseTheServerResultForTheSelectedDocument()
        {
            var auth = new Auth();
            var capture = new Capture { Status = HttpStatusCode.OK, Body = "{\"failed-documents\":[\"selected\"]}" };
            var result = await new Single(auth, capture).AddTagsAsync("context", "selected", new[] { " ", "" });
            var json = JObject.FromObject(capture.Request.Parameters.First(p => p.Type == ParameterType.RequestBody).Value);
            Assert.That(json["params"]["tags"].Values<string>(), Is.Empty);
            Assert.That(result.AllSucceeded, Is.False, "Normalization must not fabricate a server success or permission grant.");
            Assert.That(auth.Calls, Is.EqualTo(1));
            Assert.That(capture.Calls, Is.EqualTo(1));
        }

        [Test]
        public void PreCancellationDoesNotEnumerateOrAuthorize()
        {
            var auth = new Auth();
            var capture = new Capture();
            using (var source = new CancellationTokenSource())
            {
                source.Cancel();
                Assert.ThrowsAsync<OperationCanceledException>((Func<Task>)(async () =>
                    await new Bulk(auth, capture).AddTagsAsync("context", ThrowIfEnumerated(), ThrowIfEnumerated(), source.Token)));
            }
            Assert.That(auth.Calls, Is.Zero);
            Assert.That(capture.Calls, Is.Zero);
        }

        [Test]
        public void ActiveCancellationIsForwardedWithoutReplay()
        {
            var auth = new Auth();
            using (var source = new CancellationTokenSource())
            {
                var capture = new Capture { Send = async token => { source.Cancel(); await Task.Delay(10, token); } };
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), (Func<Task>)(async () =>
                    await new Bulk(auth, capture).RemoveLocksAsync("context", new[] { "id" }, new[] { "create-new-version" }, source.Token)));
                Assert.That(capture.Token, Is.EqualTo(source.Token));
                Assert.That(capture.Calls, Is.EqualTo(1));
            }
        }

        [TestCase(403, typeof(ForbiddenException)), TestCase(404, typeof(NotFoundException)), TestCase(500, typeof(InternalServerErrorException))]
        public void HttpErrorsPreserveTheirOriginalCauseWithoutReplay(int status, Type type)
        {
            var cause = new IOException("isolated HTTP failure");
            var capture = new Capture { Status = (HttpStatusCode)status, Error = cause, Body = "{\"code\":300,\"message\":\"test state\"}" };
            var error = Assert.ThrowsAsync(type, (Func<Task>)(async () =>
                await new Single(new Auth(), capture).AddTagsAsync("context", "id", new[] { "tag" })));
            Assert.That(error.InnerException, Is.SameAs(cause));
            Assert.That(capture.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task ExistingLockDefaultsRetainTheirOriginalWireShape()
        {
            var capture = new Capture();
            var client = new Single(new Auth(), capture);
            await client.AddLockAsync("context", "selected");
            Assert.That(capture.Request.Resource, Is.EqualTo("v2/document/selected"));
            var json = JObject.FromObject(capture.Request.Parameters.First(p => p.Type == ParameterType.RequestBody).Value);
            Assert.That(json["params"]["locks"].Values<string>().ToArray(), Is.EqualTo(new[] { "create-new-version" }));
        }

        private static IEnumerable<string> ThrowIfEnumerated()
        {
            foreach (var value in new string[0]) yield return value;
            throw new AssertionException("Canceled request enumerated its input.");
        }

        private static Task<DocumentCommandResult> Execute(string action, bool bulk, bool sync, Single doc, Bulk docs, string[] ids, string[] values)
        {
            if (bulk)
            {
                if (sync) return Task.FromResult(action == "add-tag" ? docs.AddTags("context", ids, values)
                    : action == "remove-tag" ? docs.RemoveTags("context", ids, values)
                    : action == "add-lock" ? docs.AddLocks("context", ids, values) : docs.RemoveLocks("context", ids, values));
                return action == "add-tag" ? docs.AddTagsAsync("context", ids, values)
                    : action == "remove-tag" ? docs.RemoveTagsAsync("context", ids, values)
                    : action == "add-lock" ? docs.AddLocksAsync("context", ids, values) : docs.RemoveLocksAsync("context", ids, values);
            }
            if (sync) return Task.FromResult(action == "add-tag" ? doc.AddTags("context", ids[0], values)
                : action == "remove-tag" ? doc.RemoveTags("context", ids[0], values)
                : action == "add-lock" ? doc.AddLocks("context", ids[0], values) : doc.RemoveLocks("context", ids[0], values));
            return action == "add-tag" ? doc.AddTagsAsync("context", ids[0], values)
                : action == "remove-tag" ? doc.RemoveTagsAsync("context", ids[0], values)
                : action == "add-lock" ? doc.AddLocksAsync("context", ids[0], values) : doc.RemoveLocksAsync("context", ids[0], values);
        }

        private sealed class Config : IRestClientConfiguration
        {
            public string BaseAddress => "https://unused.test/";
            public string UserAgent => "tag-lock-tests";
        }
        private sealed class Auth : IAsyncOAuthInfoProvider
        {
            internal int Calls;
            internal Action OnAuthorize;
            public OAuthInfo GetOAuthInfo(string id) => throw new AssertionException("Async auth required.");
            public Task<OAuthInfo> GetOAuthInfoAsync(string id, CancellationToken cancellationToken = default(CancellationToken))
            { Calls++; OnAuthorize?.Invoke(); return Task.FromResult(new OAuthInfo { UserId = id, access_token = "fixture" }); }
        }
        private sealed class Capture
        {
            internal HttpStatusCode Status = HttpStatusCode.NoContent;
            internal string Body, UserId;
            internal Exception Error;
            internal RestRequest Request;
            internal CancellationToken Token;
            internal int Calls;
            internal Func<CancellationToken, Task> Send;
            internal async Task<RestResponse> Execute(OAuthInfo info, RestRequest request, CancellationToken token)
            {
                Calls++; UserId = info.UserId; Request = request; Token = token;
                if (Send != null) await Send(token);
                return new RestResponse(request) { StatusCode = Status, Content = Body, ErrorException = Error };
            }
        }
        private sealed class Single : DocumentRestClient
        {
            private readonly Capture capture;
            internal Single(Auth auth, Capture capture) : base(auth, new Config(), null, null, "v2/") { this.capture = capture; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
                => capture.Execute(info, request, token);
            protected override Task<RestResponse<T>> ExecuteAsync<T>(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
            {
                capture.Calls++; capture.Request = request;
                return Task.FromResult(new RestResponse<T>(request) { StatusCode = capture.Status, Content = capture.Body });
            }
        }
        private sealed class Bulk : DocumentsRestClient
        {
            private readonly Capture capture;
            internal Bulk(Auth auth, Capture capture) : base(auth, new Config(), null, null, "v2/") { this.capture = capture; }
            protected override Task<RestResponse> ExecuteAsync(OAuthInfo info, RestRequest request, CancellationToken token = default(CancellationToken))
                => capture.Execute(info, request, token);
        }
    }
}
