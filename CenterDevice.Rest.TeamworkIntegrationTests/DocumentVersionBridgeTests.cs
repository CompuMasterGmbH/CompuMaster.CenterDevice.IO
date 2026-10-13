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
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    // This project is deliberately outside the solution and default CI suite.
    [TestFixture, NonParallelizable, Category("RemoteDms")]
    public partial class DocumentVersionBridgeTests
    {
        private const string Prefix = "ZZZ_UnitTests_CenterDevice_API229_Versions_";
        private static readonly string LedgerPath = Path.Combine(Path.GetTempPath(), "CenterDevice.API229.Versions.owned-resources.json");
        private BridgeClient client;
        private string contextId, ownerId;
        private Ledger ledger;
        private DateTimeOffset leaseUntil;
        private string phase;
        private string cleanupPhase;
        private Exception originalFailure, cleanupFailure;

        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task RevertAndRemoveVersionsThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false);

        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task DownloadRepresentationsThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, true);

        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task TagAndLockCommandsThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false, true);

        [Test, Explicit("Requires the same coordinated exclusive window as the owned-resource tests.")]
        public async Task RecoverOwnedResourcesThroughTeamwork()
            => await RunWithOwnedCollection(false, false, cleanupOnly: true);

        private async Task RunWithOwnedCollection(bool synchronous, bool representations, bool tagsAndLocks = false, bool copyAndMove = false, bool cleanupOnly = false, bool trashCommands = false, bool documentContracts = false, bool resourceContracts = false)
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
                    if (!cleanupOnly)
                    {
                    phase = "setup";
                    ledger = new Ledger { Name = Prefix + Guid.NewGuid().ToString("N"), Owner = ownerId };
                    SaveLedger(); // Record intent before a potentially ambiguous create.
                    Check(!(await Collections(timeout.Token)).Any(c => c.Name == ledger.Name), "Fresh collection must be absent.");
                    CheckLease();
                    ledger.Id = (await client.Collections.CreateCollectionAsync(contextId, ledger.Name, timeout.Token)).Id;
                    SaveLedger();
                    await VerifyCollection(timeout.Token);
                    phase = synchronous ? "sync document operations" : "async document operations";
                    if (resourceContracts) await VerifyResourceContracts(synchronous, timeout.Token);
                    else if (documentContracts) await VerifyDocumentContracts(synchronous, timeout.Token);
                    else if (trashCommands) await VerifyTrashCommands(synchronous, timeout.Token);
                    else if (copyAndMove) await VerifyCopyAndMove(synchronous, timeout.Token);
                    else if (tagsAndLocks) await VerifyTagsAndLocks(synchronous, timeout.Token);
                    else if (representations) await VerifyRepresentations(synchronous, timeout.Token);
                    else await VerifyVersions(synchronous, timeout.Token);
                    }
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
                    Assert.Fail("Phase: " + phase + "; failure: " + Diagnostic(originalFailure) + "; cleanup phase: " + cleanupPhase + "; cleanup: " + Diagnostic(cleanupFailure));
                if (cleanupOnly) { TestContext.Out.WriteLine("Owned resource recovery and ledger absence verified."); return; }
                TestContext.Out.WriteLine((synchronous ? "Sync" : "Async") + (resourceContracts
                    ? ": owned comments/tags, typed timeline/filter/cursor, original legacy ZIP, selected/bulk trash/direct erasure and complete cleanup verified."
                    : documentContracts
                    ? ": configured uploads/version, duplicate outcome, selected/current metadata, extended set/unset, latest-version search and complete cleanup verified."
                    : trashCommands
                    ? ": own-trash metadata, selected restore/partial failures, stable identity/history/payload, selected permanent erasure, cancellation and complete cleanup verified."
                    : copyAndMove
                    ? ": single/bulk and recursive/flat directory copies, new identities, source/history/payload preservation, file moves, cancellation and complete cleanup verified."
                    : tagsAndLocks
                    ? ": selected/bulk tags, exact partial failures, owner lock/unlock, cancellation and complete cleanup verified."
                    : representations
                    ? ": selected historical/current fulltext and ZIP contents verified; PDF/preview/unsupported MP4 outcomes recorded; cancellation and complete cleanup verified."
                    : ": identity, payload/history, repeat revert, current-version removal, non-reused numbers, delete-all rejection, cancellation and complete cleanup verified."));
            }
        }

        private async Task VerifyTagsAndLocks(bool sync, CancellationToken token)
        {
            ledger.Filename = "tags-locks-" + Guid.NewGuid().ToString("N") + ".txt";
            SaveLedger();
            CheckLease();
            var upload = await client.Documents.UploadDocumentAsync(contextId, ledger.Filename, () => Payload("Owned tag/lock fixture"), null,
                new List<string> { ledger.Id }, null, token);
            ledger.Documents.Add(upload.Id);
            SaveLedger();
            var doc = await client.Document.GetDocumentMetadataAsync(contextId, upload.Id, token);
            Check(doc.Owner == ownerId && doc.Filename == ledger.Filename, "Uploaded document ownership/identity must agree.");
            var missing = Guid.NewGuid().ToString();
            try { await client.Document.GetDocumentMetadataAsync(contextId, missing, token); throw new VerificationFailure("Negative-case document must be absent."); }
            catch (NotFoundException) { }
            Exception commandFailure = null;
            try
            {
                phase = "selected tag addition";
                CheckLease();
                var added = sync ? client.Document.AddTags(contextId, doc.Id, new[] { " red ", "blue", "RED", " ", "" }, token)
                    : await client.Document.AddTagsAsync(contextId, doc.Id, new[] { " red ", "blue", "RED", " ", "" }, token);
                Check(added.AllSucceeded, "Owned tag addition must succeed completely.");
                var tagged = await client.Document.GetDocumentMetadataAsync<TagLockMetadata>(contextId, doc.Id, cancellationToken: token);
                Check(tagged.Id == doc.Id, "Tag metadata must retain the selected document ID.");
                TestContext.Out.WriteLine("Owned tag outcome: count " + (tagged.Tags?.Count.ToString(CultureInfo.InvariantCulture) ?? "omitted") +
                    ", red " + (tagged.Tags?.Contains("red", StringComparer.OrdinalIgnoreCase) ?? false) +
                    ", blue " + (tagged.Tags?.Contains("blue", StringComparer.OrdinalIgnoreCase) ?? false) + ".");
                Check(tagged.Tags != null && tagged.Tags.Count == 2 && tagged.Tags.Contains("red", StringComparer.OrdinalIgnoreCase) && tagged.Tags.Contains("blue", StringComparer.OrdinalIgnoreCase),
                    "The server must trim/ignore empty tags and avoid case-insensitive duplicates.");
                phase = "bulk tag partial failure";
                CheckLease();
                var partial = sync ? client.Documents.AddTags(contextId, new[] { doc.Id, missing }, new[] { "second" }, token)
                    : await client.Documents.AddTagsAsync(contextId, new[] { doc.Id, missing }, new[] { "second" }, token);
                Check(!partial.AllSucceeded && partial.FailedDocuments.SequenceEqual(new[] { missing }), "Bulk tag failures must identify exactly the absent document.");
                Check((await client.Document.GetDocumentMetadataAsync<TagLockMetadata>(contextId, doc.Id, cancellationToken: token)).Tags.Contains("second"),
                    "Bulk partial success must retain the successfully added tag.");
                phase = "selected and bulk tag removal";
                CheckLease();
                var removed = sync ? client.Document.RemoveTags(contextId, doc.Id, new[] { "second" }, token)
                    : await client.Document.RemoveTagsAsync(contextId, doc.Id, new[] { "second" }, token);
                Check(removed.AllSucceeded, "Selected tag removal must succeed.");
                removed = sync ? client.Documents.RemoveTags(contextId, new[] { doc.Id }, new[] { "red", "blue" }, token)
                    : await client.Documents.RemoveTagsAsync(contextId, new[] { doc.Id }, new[] { "red", "blue" }, token);
                Check(removed.AllSucceeded, "Bulk tag removal must succeed.");
                var emptyTags = (await client.Document.GetDocumentMetadataAsync<TagLockMetadata>(contextId, doc.Id, cancellationToken: token)).Tags;
                Check(emptyTags == null || emptyTags.Count == 0, "Owned test tags must be removed.");
                phase = "selected lock and bulk unlock";
                CheckLease();
                var locked = sync ? client.Document.AddLocks(contextId, doc.Id, new[] { "create-new-version" }, token)
                    : await client.Document.AddLocksAsync(contextId, doc.Id, new[] { "create-new-version" }, token);
                Check(locked.AllSucceeded, "Owned document lock must succeed.");
                var state = await client.Document.GetDocumentMetadataAsync<TagLockMetadata>(contextId, doc.Id, cancellationToken: token);
                Check(state.Locks != null && state.Locks.Contains("create-new-version") && state.LockedBy == ownerId,
                    "Lock metadata must retain the actual owning CenterDevice user identity.");
                var unlocked = sync ? client.Documents.RemoveLocks(contextId, new[] { doc.Id }, new[] { "create-new-version" }, token)
                    : await client.Documents.RemoveLocksAsync(contextId, new[] { doc.Id }, new[] { "create-new-version" }, token);
                Check(unlocked.AllSucceeded, "Bulk unlock must succeed for the owned lock.");
                phase = "bulk lock partial failure and selected unlock";
                CheckLease();
                var lockPartial = sync ? client.Documents.AddLocks(contextId, new[] { doc.Id, missing }, new[] { "create-new-version" }, token)
                    : await client.Documents.AddLocksAsync(contextId, new[] { doc.Id, missing }, new[] { "create-new-version" }, token);
                Check(!lockPartial.AllSucceeded && lockPartial.FailedDocuments.SequenceEqual(new[] { missing }), "Bulk lock failures must retain exact document IDs.");
                unlocked = sync ? client.Document.RemoveLocks(contextId, doc.Id, new[] { "create-new-version" }, token)
                    : await client.Document.RemoveLocksAsync(contextId, doc.Id, new[] { "create-new-version" }, token);
                Check(unlocked.AllSucceeded, "Selected unlock must succeed.");
                using (var canceled = new CancellationTokenSource())
                {
                    canceled.Cancel();
                    await ExpectCanceled(() => client.Document.AddTagsAsync(contextId, doc.Id, new[] { "canceled" }, canceled.Token));
                    await ExpectCanceled(() => client.Document.AddLocksAsync(contextId, doc.Id, new[] { "create-new-version" }, canceled.Token));
                }
                var final = await client.Document.GetDocumentMetadataAsync<TagLockMetadata>(contextId, doc.Id, cancellationToken: token);
                Check((final.Tags == null || final.Tags.Count == 0) && (final.Locks == null || final.Locks.Count == 0), "Cancellation must not add tags or locks.");
            }
            catch (Exception error) { commandFailure = error; throw; }
            finally
            {
                // Independent cleanup works even if the main request token expired after committing a lock.
                try
                {
                    using (var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                    {
                        var state = await client.Document.GetDocumentMetadataAsync<TagLockMetadata>(contextId, doc.Id, cancellationToken: cleanup.Token);
                        if (state.Locks != null && state.Locks.Count > 0)
                        {
                            Check(state.LockedBy == ownerId, "Cleanup must not remove a foreign user's lock.");
                            var cleared = await client.Document.RemoveLocksAsync(contextId, doc.Id, state.Locks, cleanup.Token);
                            Check(cleared.AllSucceeded, "Test-owned locks must be removed before deleting the document.");
                        }
                    }
                }
                catch (Exception cleanupError) when (commandFailure != null)
                { throw new AggregateException("Document command and owned-lock cleanup both failed.", commandFailure, cleanupError); }
            }
        }

        public sealed class TagLockMetadata
        {
            public string Id { get; set; }
            public List<string> Tags { get; set; }
            public List<string> Locks { get; set; }
            [System.Text.Json.Serialization.JsonPropertyName("locked-by")]
            public string LockedBy { get; set; }
        }

        private async Task VerifyRepresentations(bool sync, CancellationToken token)
        {
            ledger.Filename = "representations-" + Guid.NewGuid().ToString("N") + ".txt";
            SaveLedger();
            CheckLease();
            var upload = await client.Documents.UploadDocumentAsync(contextId, ledger.Filename, () => Payload("Original fulltext"), null,
                new List<string> { ledger.Id }, null, token);
            ledger.Documents.Add(upload.Id);
            SaveLedger();
            var doc = await client.Document.GetDocumentMetadataAsync(contextId, upload.Id, token);
            Check(doc.Owner == ownerId && doc.Filename == ledger.Filename, "Uploaded document ownership/identity must agree.");
            await client.Document.UploadNewVersionAsync(contextId, doc.Id, ledger.Filename, () => Payload("Current fulltext"), token);
            phase = "historical and current fulltext";
            foreach (var historical in new[] { true, false })
            {
                CheckLease();
                long? version = historical ? (long?)doc.Version : null;
                using (var stream = sync ? client.Document.DownloadFulltext(contextId, doc.Id, version, 30, token)
                    : await client.Document.DownloadFulltextAsync(contextId, doc.Id, version, 30, token))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    Check(await reader.ReadToEndAsync() == (historical ? "Original fulltext" : "Current fulltext"),
                        "Fulltext must bind the requested historical/current document version.");
            }
            phase = "PDF representation";
            CheckLease();
            // Supply an actual PDF, instead of assuming this tenant converts plain text into PDF.
            ledger.Filename = "representations-" + Guid.NewGuid().ToString("N") + ".pdf";
            SaveLedger();
            await client.Document.UploadNewVersionAsync(contextId, doc.Id, ledger.Filename, PdfPayload, token);
            var pdfReady = false;
            try
            {
                using (var stream = sync ? client.Document.DownloadPdf(contextId, doc.Id, waitForGenerationSeconds: 30, cancellationToken: token)
                    : await client.Document.DownloadPdfAsync(contextId, doc.Id, waitForGenerationSeconds: 30, cancellationToken: token))
                {
                    var signature = new byte[5];
                    var count = 0;
                    while (count < signature.Length)
                    {
                        var read = await stream.ReadAsync(signature, count, signature.Length - count, token);
                        if (read == 0) break;
                        count += read;
                    }
                    Check(Encoding.ASCII.GetString(signature) == "%PDF-", "A ready PDF representation must contain PDF bytes.");
                }
                pdfReady = true;
                TestContext.Out.WriteLine("PDF representation outcome: ready with verified PDF signature.");
            }
            catch (DocumentRepresentationNotReadyException error)
            { TestContext.Out.WriteLine("PDF representation outcome: pending generation, status " + (int)error.StatusCode + "."); }
            catch (NotAcceptableException)
            { TestContext.Out.WriteLine("PDF representation outcome: unsupported by this tenant."); }
            phase = "unsupported MP4";
            CheckLease();
            var unsupported = false;
            try
            {
                using (sync ? client.Document.DownloadMp4(contextId, doc.Id, cancellationToken: token)
                    : await client.Document.DownloadMp4Async(contextId, doc.Id, cancellationToken: token)) { }
            }
            catch (NotAcceptableException) { unsupported = true; }
            Check(unsupported, "Unsupported PDF-to-MP4 conversion must not be returned as successful representation data.");
            phase = "original and PDF ZIP downloads";
            foreach (var streaming in new[] { false, true })
            {
                CheckLease();
                using (var source = sync ? client.Documents.DownloadDocumentsAsZip(contextId, new[] { doc.Id }, stream: streaming, cancellationToken: token)
                    : await client.Documents.DownloadDocumentsAsZipAsync(contextId, new[] { doc.Id }, stream: streaming, cancellationToken: token))
                    await CheckZip(source, ledger.Filename, token);
            }
            if (pdfReady)
            {
                CheckLease();
                using (var source = sync ? client.Documents.DownloadDocumentsAsZip(contextId, new[] { doc.Id },
                    CenterDevice.Rest.Clients.Documents.DocumentDownloadRepresentation.Pdf, cancellationToken: token)
                    : await client.Documents.DownloadDocumentsAsZipAsync(contextId, new[] { doc.Id },
                    CenterDevice.Rest.Clients.Documents.DocumentDownloadRepresentation.Pdf, cancellationToken: token))
                    await CheckZip(source, ledger.Filename, token);
            }
            phase = "preview pages";
            CheckLease();
            try
            {
                using (var preview = sync ? client.Document.DownloadPreviews(contextId, doc.Id, CenterDevice.Rest.Clients.Documents.PreviewSize._240,
                    waitForGenerationSeconds: 30, includeErrorInfo: true, cancellationToken: token)
                    : await client.Document.DownloadPreviewsAsync(contextId, doc.Id, CenterDevice.Rest.Clients.Documents.PreviewSize._240,
                    waitForGenerationSeconds: 30, includeErrorInfo: true, cancellationToken: token))
                    Check(preview.Content.ReadByte() >= 0, "A ready page preview must contain image bytes.");
                TestContext.Out.WriteLine("Preview outcome: ready with negotiated image MIME and nonempty body.");
                CheckLease();
                using (var previews = sync ? client.Document.DownloadPreviews(contextId, doc.Id, CenterDevice.Rest.Clients.Documents.PreviewSize._240,
                    "all", waitForGenerationSeconds: 30, cancellationToken: token)
                    : await client.Document.DownloadPreviewsAsync(contextId, doc.Id, CenterDevice.Rest.Clients.Documents.PreviewSize._240,
                    "all", waitForGenerationSeconds: 30, cancellationToken: token))
                {
                    if (previews.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var reader = new StreamReader(previews.Content, Encoding.UTF8))
                        {
                            var parts = await reader.ReadToEndAsync();
                            Check(parts.Contains("name=\"1\"") || parts.Contains("name=1"), "The actual preview part must retain its one-based page identity.");
                        }
                        TestContext.Out.WriteLine("All-pages preview outcome: multipart with boundary and actual page-1 part.");
                    }
                    else
                    {
                        Check(previews.Content.ReadByte() >= 0, "A single available preview must contain image bytes.");
                        TestContext.Out.WriteLine("All-pages preview outcome: single available image; no multipart page identities inferred.");
                    }
                }
            }
            catch (DocumentRepresentationNotReadyException error)
            { TestContext.Out.WriteLine("Preview outcome: pending generation, status " + (int)error.StatusCode + "."); }
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                await ExpectCanceled(() => client.Document.DownloadFulltextAsync(contextId, doc.Id, cancellationToken: canceled.Token));
            }
            phase = "active fulltext cancellation";
            CheckLease();
            using (var canceled = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var source = sync ? client.Document.DownloadFulltext(contextId, doc.Id, doc.Version, cancellationToken: canceled.Token)
                : await client.Document.DownloadFulltextAsync(contextId, doc.Id, doc.Version, cancellationToken: canceled.Token))
            {
                canceled.Cancel();
                await ExpectCanceled(async () => { await source.ReadAsync(new byte[1], 0, 1); });
            }
        }

        private static MemoryStream PdfPayload()
        {
            var content = "BT /F1 12 Tf 20 100 Td (CenterDevice test PDF) Tj ET\n";
            var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Length " + Encoding.ASCII.GetByteCount(content) + " >>\nstream\n" + content + "endstream",
                "<< /Length " + Encoding.ASCII.GetByteCount(content) + " >>\nstream\n" + content + "endstream" };
            var text = new StringBuilder("%PDF-1.4\n");
            var offsets = new List<int>();
            for (int i = 0; i < objects.Length; i++)
            {
                offsets.Add(Encoding.ASCII.GetByteCount(text.ToString()));
                text.Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
            }
            var xref = Encoding.ASCII.GetByteCount(text.ToString());
            text.Append("xref\n0 ").Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture)).Append("\n0000000000 65535 f \n");
            foreach (var offset in offsets) text.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            text.Append("trailer\n<< /Size ").Append((objects.Length + 1).ToString(CultureInfo.InvariantCulture))
                .Append(" /Root 1 0 R >>\nstartxref\n").Append(xref.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
            return new MemoryStream(Encoding.ASCII.GetBytes(text.ToString()), false);
        }

        private static async Task CheckZip(Stream source, string filename, CancellationToken token)
        {
            using (var bytes = new MemoryStream())
            {
                await source.CopyToAsync(bytes, 81920, token); // Only this tiny owned fixture is buffered for archive inspection.
                bytes.Position = 0;
                using (var zip = new ZipArchive(bytes, ZipArchiveMode.Read, true))
                {
                    Check(zip.Entries.Count == 1, "ZIP must contain exactly the selected visible test document.");
                    var entry = zip.Entries[0];
                    Check(entry.Name == filename, "ZIP must retain the selected file's current name.");
                    using (var body = entry.Open())
                    using (var reader = new StreamReader(body, Encoding.ASCII))
                        Check((await reader.ReadToEndAsync()).StartsWith("%PDF-", StringComparison.Ordinal), "ZIP content must match the current PDF document.");
                }
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
            await CheckHistory(sync, doc.Id, v3, new[] { v1, v2, v3 }, token);
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
            await CheckHistory(sync, doc.Id, v5, new[] { v1, v2, v3, v4, v5 }, token);
            await CheckPayload(doc.Id, v5, "A", token);
            CheckLease();
            phase = "remove selected/current versions";
            if (sync) client.Document.RemoveDocumentVersions(contextId, doc.Id, new[] { v2, v4, v5 });
            else await client.Document.RemoveDocumentVersionsAsync(contextId, doc.Id, new[] { v2, v4, v5 }, token);
            var remaining = await client.Document.GetDocumentMetadataAsync(contextId, doc.Id, token);
            Check(remaining.Id == doc.Id && remaining.Version == v3, "Largest remaining version must become current without changing identity.");
            await CheckHistory(sync, doc.Id, v3, new[] { v1, v3 }, token);
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

        private async Task CheckHistory(bool synchronous, string id, long currentVersion, long[] versions, CancellationToken token)
        {
            var history = synchronous ? client.Document.GetDocumentVersionHistory(contextId, id)
                : await client.Document.GetDocumentVersionHistoryAsync(contextId, id, token);
            Check(history.Id == id && history.Version == currentVersion, "History must bind the selected document and current version.");
            Check(history.VersionDetails != null, "The owning test account must receive version details.");
            Check(history.VersionDetails.Select(v => v.Version).OrderBy(v => v).SequenceEqual(versions.OrderBy(v => v).Select(v => (long?)v)),
                "History must contain the actual existing versions without removed or invented entries.");
            Check(history.VersionDetails.All(v => v.Filename == ledger.Filename && v.Size == 1 && v.VersionDate.HasValue),
                "Version history must retain per-version filename, content size and timestamp.");
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
            cleanupPhase = "scope validation";
            CheckLease(false);
            Check(ledger != null && ledger.Name != null && ledger.Name.StartsWith(Prefix, StringComparison.Ordinal) &&
                Guid.TryParseExact(ledger.Name.Substring(Prefix.Length), "N", out _) && ledger.Owner == ownerId,
                "Recovery ledger must identify this fixture's exact owned scope.");
            cleanupPhase = "collection lookup";
            var matches = (await Collections(token)).Where(c => c.Name == ledger.Name).ToList();
            Check(matches.Count <= 1, "Ambiguous collection identity prevents cleanup.");
            if (matches.Count == 1)
            {
                Check(ledger.Id == null || ledger.Id == matches[0].Id, "Collection ID must agree with recorded intent.");
                ledger.Id = matches[0].Id;
                SaveLedger();
                cleanupPhase = "collection ownership";
                await VerifyCollection(token);
                cleanupPhase = "document inventory";
                var page = await client.Documents.GetAsync<DocumentFullMetadata>(contextId, ledger.Id, null, null, 0, 500, token);
                var docs = page?.Documents;
                Check(docs != null && page.Hits == docs.Count && docs.Count <= (ledger.AllowedFilenames.Count == 0 ? 1 : 12), "Unexpected or incomplete collection contents prevent cleanup.");
                foreach (var doc in docs)
                {
                    Check(doc.Owner == ownerId && OwnedFilename(doc.Filename), "Only this fixture's owned document may be removed.");
                    if (!ledger.Documents.Contains(doc.Id)) ledger.Documents.Add(doc.Id);
                }
                SaveLedger();
                cleanupPhase = "folder inventory and removal";
                await CleanupOwnedFolders(token);
                cleanupPhase = "collection removal";
                await client.Collection.EraseCollectionAsync(contextId, ledger.Id, true, token);
            }
            Check(!(await Collections(token)).Any(c => c.Name == ledger.Name || c.Id == ledger.Id), "Collection removal must be verified.");
            cleanupPhase = "trash inventory";
            var trash = await client.Trash.List(contextId, token);
            var ownedTrash = trash.Where(d => ledger.Documents.Contains(d.Id)).ToList();
            Check(ownedTrash.All(d => OwnedFilename(d.Filename)), "Trash identity must agree with the owned ledger.");
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
            cleanupPhase = "ledger removal";
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
            if (error is HttpRequestException http)
            {
                var kind = typeof(HttpRequestException).GetProperty("HttpRequestError")?.GetValue(http)?.ToString() ?? "unspecified";
                var httpStatus = typeof(HttpRequestException).GetProperty("StatusCode")?.GetValue(http)?.ToString() ?? "absent";
                var frames = new System.Diagnostics.StackTrace(http, false).GetFrames()?.Take(4).Select(frame => frame.GetMethod()?.DeclaringType?.Name + "." + frame.GetMethod()?.Name);
                var reason = new[] { "response ended prematurely", "invalid request URI", "Name or service not known", "No such host", "invalid header", "Error while copying content", "HTTP/2" }
                    .FirstOrDefault(text => (http.Message ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
                return "HttpRequestException (" + kind + "; status " + httpStatus + (reason == null ? "" : "; " + reason) + "; methods " + string.Join(",", frames ?? new string[0]) + ")" + (http.InnerException == null ? "" : " [" + Diagnostic(http.InnerException) + "]");
            }
            if (error is AggregateException aggregate) return "AggregateException [" + string.Join("; ", aggregate.InnerExceptions.Select(Diagnostic)) + "]";
            if (error is VerificationFailure) return error.Message;
            if (error is InvalidResponseDataException && error.InnerException is InvalidDataException invalid)
            {
                var media = System.Text.RegularExpressions.Regex.Match(invalid.Message, @"received ([a-zA-Z0-9!#$&^_.+-]+/[a-zA-Z0-9!#$&^_.+-]+)");
                if (media.Success) return "InvalidResponseDataException (media type " + media.Groups[1].Value + ")";
            }
            var status = System.Text.RegularExpressions.Regex.Match(error.Message ?? "", "but got \"([A-Za-z]+)\"");
            var statusText = status.Success && Enum.TryParse(status.Groups[1].Value, out HttpStatusCode code) ? " (HTTP " + (int)code + ")" : "";
            return error.GetType().Name + statusText + (error is RestClientException rest && rest.ErrorResponse != null ? " (code " + rest.ErrorResponse.Code + ")" : "")
                + (error.InnerException == null ? "" : " [" + Diagnostic(error.InnerException) + "]");
        }
        private sealed class VerificationFailure : Exception { internal VerificationFailure(string message) : base(message) { } }
        public sealed class Ledger
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string Owner { get; set; }
            public string Filename { get; set; }
            public List<string> Documents { get; set; } = new List<string>();
            public List<string> AllowedFilenames { get; set; } = new List<string>();
            public List<string> AllowedFolderPaths { get; set; } = new List<string>();
            public Dictionary<string, string> FolderIds { get; set; } = new Dictionary<string, string>();
        }
        private sealed class BridgeClient : TeamworkRestClient
        {
            internal BridgeClient(OpenScopeApiClient open) : base(open) { Trash = new TrashCleanupClient(oAuthInfoProvider, configuration, errorHandler); }
            internal new TrashCleanupClient Trash { get; }
            internal new CenterDevice.Rest.Clients.Documents.DocumentRestClient Document
                => new MediaProbeClient(oAuthInfoProvider, configuration, errorHandler, DefaultStreamWrapper(), apiVersionPrefix);
        }
        private sealed class MediaProbeClient : CenterDevice.Rest.Clients.Documents.DocumentRestClient
        {
            internal MediaProbeClient(IOAuthInfoProvider auth, IRestClientConfiguration config, IRestClientErrorHandler errors, IStreamWrapper wrapper, string prefix)
                : base(auth, config, errors, wrapper, prefix) { }
            protected override async Task<HttpResponseMessage> SendResourceDownloadRequestAsync(HttpRequestMessage request, CancellationToken token)
            {
                var response = await base.SendResourceDownloadRequestAsync(request, token);
                // Only MIME/status diagnostics from owned fixtures, never response bodies, locations or authorization headers.
                TestContext.Out.WriteLine("Representation response: " + (int)response.StatusCode + ", MIME " + (response.Content?.Headers.ContentType?.MediaType ?? "missing") + ".");
                return response;
            }
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
