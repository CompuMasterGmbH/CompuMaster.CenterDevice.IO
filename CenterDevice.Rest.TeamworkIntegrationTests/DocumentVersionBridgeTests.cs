using CenterDevice.Rest;
using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Collections;
using CenterDevice.Rest.Clients.Documents.Metadata;
using CenterDevice.Rest.Clients.OAuth;
using CenterDevice.Rest.Exceptions;
using CenterDevice.Rest.ResponseHandler;
using CompuMaster.Scopevisio.CenterDeviceApi;
using CompuMaster.Scopevisio.OpenApi;
using CompuMaster.Scopevisio.OpenApi.Client;
using NUnit.Framework;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    // This project is deliberately outside the solution and default CI suite.
    [TestFixture, NonParallelizable, Category("RemoteDms")]
    public class DocumentVersionBridgeTests
    {
        private const string Prefix = "ZZZ_UnitTests_CenterDevice_API229_Versions_";
        private static readonly string LedgerPath = Path.Combine(Path.GetTempPath(), "CenterDevice.API229.Versions.owned-resources.json");
        private BridgeClient client;
        private string contextId, ownerId;
        private Ledger ledger;
        private DateTimeOffset leaseUntil;
        private string phase;
        private Exception originalFailure, cleanupFailure;

        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task RevertAndRemoveVersionsThroughTeamwork(bool synchronous)
        {
            RequireLease();
            using (var transport = CenterDeviceHttpTransport.CreateHttpClient(new System.Net.Http.HttpClientHandler()))
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8)))
            {
                originalFailure = cleanupFailure = null;
                try
                {
                    phase = "authorization";
                    var config = new Configuration
                    {
                        HttpClient = transport,
                        Username = Credential("TEST_USERNAME", "username"),
                        ClientNumber = Credential("TEST_CUSTOMERNO", "customer no."),
                        Password = Credential("TEST_PASSWORD", "password")
                    };
                    var open = new OpenScopeApiClient(config);
                    await open.AuthorizeWithUserCredentialsAsync(timeout.Token);
                    client = new BridgeClient(open);
                    contextId = open.Token.Uid;
                    ownerId = (await client.User.GetAuthenticatedUserDataAsync(contextId, timeout.Token)).Id;
                    Check(!string.IsNullOrEmpty(ownerId), "The actual CenterDevice owner ID is required.");
                    phase = "recover owned resources";
                    if (File.Exists(LedgerPath))
                    {
                        ledger = JsonSerializer.Deserialize<Ledger>(File.ReadAllText(LedgerPath));
                        await Cleanup(timeout.Token);
                    }
                    phase = "setup";
                    ledger = new Ledger { Name = Prefix + Guid.NewGuid().ToString("N"), Owner = ownerId };
                    SaveLedger(); // Record intent before a potentially ambiguous create.
                    Check(!(await Collections(timeout.Token)).Any(c => c.Name == ledger.Name), "Fresh collection must be absent.");
                    CheckLease();
                    ledger.Id = (await client.Collections.CreateCollectionAsync(contextId, ledger.Name, timeout.Token)).Id;
                    SaveLedger();
                    await VerifyCollection(timeout.Token);
                    phase = synchronous ? "sync version commands" : "async version commands";
                    await VerifyVersions(synchronous, timeout.Token);
                }
                catch (Exception error) { originalFailure = error; }
                finally
                {
                    if (client != null && ledger != null)
                    {
                        using (var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                        {
                            try { await Cleanup(cleanupTimeout.Token); }
                            catch (Exception error) { cleanupFailure = error; }
                        }
                    }
                }
                // Do not let NUnit print provider messages, headers, token bodies or credentials.
                // Retain both exceptions in memory; report separate sanitized diagnostics.
                if (originalFailure != null || cleanupFailure != null)
                    Assert.Fail("Phase: " + phase + "; failure: " + Diagnostic(originalFailure) + "; cleanup: " + Diagnostic(cleanupFailure));
                TestContext.Out.WriteLine((synchronous ? "Sync" : "Async") + ": identity, payload/history, repeat revert, current-version removal, non-reused numbers, delete-all rejection, cancellation and complete cleanup verified.");
            }
        }

        private async Task VerifyVersions(bool sync, CancellationToken token)
        {
            ledger.Filename = (sync ? "sync" : "async") + "-" + Guid.NewGuid().ToString("N") + ".txt";
            SaveLedger();
            CheckLease();
            var upload = await client.Documents.UploadDocumentAsync(contextId, ledger.Filename, () => Payload("A"), null,
                new List<string> { ledger.Id }, null, token);
            ledger.Documents.Add(upload.Id);
            SaveLedger();
            var doc = await client.Document.GetDocumentMetadataAsync(contextId, upload.Id, token);
            Check(doc.Owner == ownerId && doc.Filename == ledger.Filename, "Uploaded document ownership/identity must agree.");
            var v1 = doc.Version;
            var v2 = (await client.Document.UploadNewVersionAsync(contextId, doc.Id, ledger.Filename, () => Payload("B"), token)).Version;
            var v3 = (await client.Document.UploadNewVersionAsync(contextId, doc.Id, ledger.Filename, () => Payload("C"), token)).Version;
            Check(v2 > v1 && v3 > v2, "Uploads must advance version numbers.");
            CheckLease();
            phase = "revert existing version";
            var v4 = sync ? client.Document.RevertDocumentVersion(contextId, doc.Id, v1).Version
                : (await client.Document.RevertDocumentVersionAsync(contextId, doc.Id, v1, token)).Version;
            Check(v4 > v3, "Revert must create a new version.");
            await CheckPayload(doc.Id, v1, "A", token);
            await CheckPayload(doc.Id, v2, "B", token);
            await CheckPayload(doc.Id, v3, "C", token);
            await CheckPayload(doc.Id, v4, "A", token);
            Check((await client.Document.GetDocumentMetadataAsync(contextId, doc.Id, token)).Id == doc.Id, "Revert must preserve document ID.");
            CheckLease();
            phase = "repeat revert";
            var v5 = sync ? client.Document.RevertDocumentVersion(contextId, doc.Id, v1).Version
                : (await client.Document.RevertDocumentVersionAsync(contextId, doc.Id, v1, token)).Version;
            Check(v5 > v4, "A second revert must create another version.");
            await CheckPayload(doc.Id, v5, "A", token);
            CheckLease();
            phase = "remove selected/current versions";
            if (sync) client.Document.RemoveDocumentVersions(contextId, doc.Id, new[] { v2, v4, v5 });
            else await client.Document.RemoveDocumentVersionsAsync(contextId, doc.Id, new[] { v2, v4, v5 }, token);
            var remaining = await client.Document.GetDocumentMetadataAsync(contextId, doc.Id, token);
            Check(remaining.Id == doc.Id && remaining.Version == v3, "Largest remaining version must become current without changing identity.");
            await CheckPayload(doc.Id, null, "C", token);
            foreach (var deleted in new[] { v2, v4, v5 })
                await ExpectMissingVersion(doc.Id, deleted, token);
            await CheckPayload(doc.Id, v1, "A", token);
            phase = "repeat version removal";
            CheckLease();
            if (sync) client.Document.RemoveDocumentVersions(contextId, doc.Id, new[] { v2, v4, v5 });
            else await client.Document.RemoveDocumentVersionsAsync(contextId, doc.Id, new[] { v2, v4, v5 }, token);
            Check((await client.Document.GetDocumentMetadataAsync(contextId, doc.Id, token)).Version == v3, "Repeated version removal must preserve the current version.");
            CheckLease();
            var v6 = (await client.Document.UploadNewVersionAsync(contextId, doc.Id, ledger.Filename, () => Payload("D"), token)).Version;
            Check(v6 > v5, "Deleted version numbers must never be reused.");
            await CheckPayload(doc.Id, v6, "D", token);
            CheckLease();
            phase = "reject removal of all remaining versions";
            var rejected = false;
            try
            {
                if (sync) client.Document.RemoveDocumentVersions(contextId, doc.Id, new[] { v1, v3, v6 });
                else await client.Document.RemoveDocumentVersionsAsync(contextId, doc.Id, new[] { v1, v3, v6 }, token);
            }
            catch (ForbiddenException error) when (error.ErrorResponse?.Code == 300) { rejected = true; }
            Check(rejected, "Removing every remaining version must be rejected.");
            await CheckPayload(doc.Id, v1, "A", token);
            await CheckPayload(doc.Id, v3, "C", token);
            await CheckPayload(doc.Id, v6, "D", token);
            phase = "pre-canceled commands";
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                await ExpectCanceled(() => client.Document.RevertDocumentVersionAsync(contextId, doc.Id, v1, canceled.Token));
                await ExpectCanceled(() => client.Document.RemoveDocumentVersionsAsync(contextId, doc.Id, new[] { v1 }, canceled.Token));
            }
            Check((await client.Document.GetDocumentMetadataAsync(contextId, doc.Id, token)).Version == v6, "Pre-canceled commands must not change the current version.");
            await CheckPayload(doc.Id, v1, "A", token);
        }

        private async Task CheckPayload(string id, long? version, string expected, CancellationToken token)
        {
            using (var stream = await client.Document.DownloadDocumentAsync(contextId, id, version, cancellationToken: token))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                Check(await reader.ReadToEndAsync() == expected, "Version payload must match its original contents.");
        }

        private async Task ExpectMissingVersion(string id, long version, CancellationToken token)
        {
            try { await client.Document.GetDocumentMetadataAsync<DocumentBasicMetadata>(contextId, id, version, token); }
            catch (NotFoundException) { return; }
            throw new VerificationFailure("Removed version must be inaccessible.");
        }

        private static async Task ExpectCanceled(Func<Task> action)
        {
            try { await action(); }
            catch (OperationCanceledException) { return; }
            throw new VerificationFailure("Pre-canceled operation must throw cancellation.");
        }

        private async Task<List<Collection>> Collections(CancellationToken token)
            => (await client.Collections.GetCollectionsAsync(contextId, token)).Collections ?? new List<Collection>();

        private async Task VerifyCollection(CancellationToken token)
        {
            var collection = await client.Collection.GetCollectionAsync(contextId, ledger.Id, RestRequestFields.ALL, token);
            Check(collection.Name == ledger.Name && collection.Owner == ownerId && !collection.IsShared && !collection.Archived && !collection.IsIntelligent,
                "Cleanup scope must be an unshared, ordinary collection owned by the current CenterDevice user.");
        }

        private async Task Cleanup(CancellationToken token)
        {
            CheckLease(false);
            Check(ledger != null && ledger.Name != null && ledger.Name.StartsWith(Prefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(ledger.Name.Substring(Prefix.Length), "N", out _) && ledger.Owner == ownerId,
                "Recovery ledger must identify this fixture's exact owned scope.");
            var matches = (await Collections(token)).Where(c => c.Name == ledger.Name).ToList();
            Check(matches.Count <= 1, "Ambiguous collection identity prevents cleanup.");
            if (matches.Count == 1)
            {
                Check(ledger.Id == null || ledger.Id == matches[0].Id, "Collection ID must agree with recorded intent.");
                ledger.Id = matches[0].Id;
                SaveLedger();
                await VerifyCollection(token);
                var docs = (await client.Documents.GetAsync<DocumentFullMetadata>(contextId, ledger.Id, null, null, 0, 500, token)).Documents;
                Check(docs != null && docs.Count <= 1, "Unexpected collection contents prevent cleanup.");
                foreach (var doc in docs)
                {
                    Check(doc.Owner == ownerId && doc.Filename == ledger.Filename, "Only this fixture's owned document may be removed.");
                    if (!ledger.Documents.Contains(doc.Id)) ledger.Documents.Add(doc.Id);
                }
                SaveLedger();
                await client.Collection.EraseCollectionAsync(contextId, ledger.Id, true, token);
            }
            Check(!(await Collections(token)).Any(c => c.Name == ledger.Name || c.Id == ledger.Id), "Collection removal must be verified.");
            var trash = await client.Trash.List(contextId, token);
            var ownedTrash = trash.Where(d => ledger.Documents.Contains(d.Id)).ToList();
            Check(ownedTrash.All(d => d.Filename == ledger.Filename), "Trash identity must agree with the owned ledger.");
            if (ownedTrash.Count != 0)
                await client.Trash.Erase(contextId, ownedTrash.Select(d => d.Id).ToArray(), token);
            Check(!(await client.Trash.List(contextId, token)).Any(d => ledger.Documents.Contains(d.Id)), "Test documents must be absent from trash.");
            foreach (var id in ledger.Documents)
            {
                var absent = false;
                try { await client.Document.GetDocumentMetadataAsync(contextId, id, token); }
                catch (NotFoundException) { absent = true; }
                Check(absent, "Test document must be absent from active metadata.");
            }
            File.Delete(LedgerPath);
            ledger = null;
        }

        private void SaveLedger() => File.WriteAllText(LedgerPath, JsonSerializer.Serialize(ledger), new UTF8Encoding(true));
        private static MemoryStream Payload(string value) => new MemoryStream(Encoding.UTF8.GetBytes(value), false);
        private void RequireLease()
        {
            if (Environment.GetEnvironmentVariable("CENTERDEVICE_TEAMWORK_LIVE") != "1")
                Assert.Ignore("Remote integration requires explicit opt-in and a coordinated exclusive window.");
            Check(DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("CENTERDEVICE_TEAMWORK_EXCLUSIVE_UNTIL_UTC"), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out leaseUntil), "An exclusive-window expiry is required.");
            CheckLease();
        }
        private void CheckLease(bool reserveCleanup = true)
            => Check(DateTimeOffset.UtcNow.AddMinutes(reserveCleanup ? 15 : 5) < leaseUntil, "The coordinated exclusive window must cover execution and cleanup.");
        private static string Credential(string variable, string cacheKey)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(value))
            {
                using (var md5 = MD5.Create())
                {
                    var hash = BitConverter.ToString(md5.ComputeHash(Encoding.ASCII.GetBytes(cacheKey))).Replace("-", "");
                    var path = Path.Combine(Path.GetTempPath(), "~Buffer.Scopevisio.Teamwork.Test." + hash + ".tmp");
                    if (File.Exists(path)) value = File.ReadAllText(path);
                }
            }
            Check(!string.IsNullOrWhiteSpace(value), "Required test credentials are unavailable.");
            return value;
        }
        private static void Check(bool value, string message) { if (!value) throw new VerificationFailure(message); }
        private static string Diagnostic(Exception error)
        {
            if (error == null) return "none";
            if (error is VerificationFailure) return error.Message;
            return error.GetType().Name + (error is RestClientException rest && rest.ErrorResponse != null ? " (code " + rest.ErrorResponse.Code + ")" : "");
        }
        private sealed class VerificationFailure : Exception { internal VerificationFailure(string message) : base(message) { } }
        public sealed class Ledger
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string Owner { get; set; }
            public string Filename { get; set; }
            public List<string> Documents { get; set; } = new List<string>();
        }
        private sealed class BridgeClient : TeamworkRestClient
        {
            internal BridgeClient(OpenScopeApiClient open) : base(open) { Trash = new TrashCleanupClient(oAuthInfoProvider, configuration, errorHandler); }
            internal TrashCleanupClient Trash { get; }
        }
        private sealed class TrashCleanupClient : CenterDeviceRestClient
        {
            internal TrashCleanupClient(IOAuthInfoProvider provider, IRestClientConfiguration config, IRestClientErrorHandler errors) : base(provider, config, errors, "") { }
            internal async Task<List<DocumentBasicMetadata>> List(string userId, CancellationToken token)
            {
                var all = new List<DocumentBasicMetadata>();
                for (int offset = 0; ; offset += 500)
                {
                    var request = CreateRestRequest("trash", Method.Get);
                    request.AddQueryParameter("offset", offset.ToString(CultureInfo.InvariantCulture));
                    request.AddQueryParameter("rows", "500");
                    var response = await ExecuteAsync<TrashPage>(await GetOAuthInfoAsync(userId, token), request, token);
                    ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.OK));
                    Check(response.Data?.Documents != null, "Trash listing must be complete.");
                    all.AddRange(response.Data.Documents);
                    if (response.Data.Documents.Count < 500) return all;
                }
            }
            internal async Task Erase(string userId, string[] ids, CancellationToken token)
            {
                Check(ids.Length != 0, "Targeted trash cleanup requires exact document IDs.");
                var request = CreateRestRequest("trash", Method.Post, "application/json");
                request.AddJsonBody(new { action = "erase-documents", @params = new { documents = ids } });
                var response = await ExecuteAsync(await GetOAuthInfoAsync(userId, token), request, token);
                ValidateResponse(response, new StatusCodeResponseHandler(HttpStatusCode.NoContent));
            }
        }
        public sealed class TrashPage { public List<DocumentBasicMetadata> Documents { get; set; } }
    }
}
