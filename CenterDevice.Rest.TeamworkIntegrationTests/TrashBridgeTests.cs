using CenterDevice.Rest.Clients;
using CenterDevice.Rest.Clients.Trash;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    public partial class DocumentVersionBridgeTests
    {
        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task TrashCommandsThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false, trashCommands: true);

        private async Task VerifyTrashCommands(bool sync, CancellationToken token)
        {
            ledger.Filename = "trash-" + Guid.NewGuid().ToString("N") + ".txt";
            SaveLedger();
            CheckLease();
            var upload = await client.Documents.UploadDocumentAsync(contextId, ledger.Filename, () => Payload("initial-trash-payload"), null, new List<string> { ledger.Id }, null, token);
            ledger.Documents.Add(upload.Id);
            SaveLedger();
            var original = await client.Document.GetDocumentMetadataAsync(contextId, upload.Id, token);
            Check(original.Owner == ownerId && original.Filename == ledger.Filename, "Trash tests must use the exact newly owned document.");
            var latest = (await client.Document.UploadNewVersionAsync(contextId, original.Id, ledger.Filename, () => Payload("latest-trash-payload"), token)).Version;
            await client.Document.AddTagsAsync(contextId, original.Id, new[] { "owned-trash-tag" }, token);
            var trash = ((CenterDeviceClientBase)client).Trash; // Use the new production client; private cleanup remains separate.
            var missing = Guid.NewGuid().ToString("D");
            try { await client.Document.GetDocumentMetadataAsync(contextId, missing, token); throw new VerificationFailure("The negative-case document must be absent."); }
            catch (NotFoundException) { }
            Check(!(await client.Trash.List(contextId, token)).Any(document => document.Id == missing), "The negative-case document must be absent from trash.");

            phase = "soft deletion and production trash metadata";
            CheckLease();
            await client.Document.DeleteDocumentAsync(contextId, original.Id, token);
            var page = sync ? trash.GetDocuments(contextId, cancellationToken: token) : await trash.GetDocumentsAsync(contextId, cancellationToken: token);
            var deleted = page.SingleOrDefault(document => document.Id == original.Id);
            TestContext.Out.WriteLine("Trash metadata: found " + (deleted != null) + ", filename match " + (deleted?.Filename == ledger.Filename) +
                ", size " + (deleted?.Size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "omitted") + ", expected " + "latest-trash-payload".Length +
                ", upload/version/deletion dates " + (deleted?.UploadDate.HasValue ?? false) + "/" + (deleted?.VersionDate.HasValue ?? false) + "/" + (deleted?.DeletionDate.HasValue ?? false) +
                ", tag count " + (deleted?.Tags?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "omitted") + ".");
            Check(deleted != null && deleted.Filename == ledger.Filename && deleted.Size == "latest-trash-payload".Length &&
                deleted.VersionDate.HasValue && deleted.DeletionDate.HasValue &&
                (!deleted.UploadDate.HasValue || deleted.UploadDate.Value <= deleted.VersionDate.Value) &&
                (deleted.Tags == null || deleted.Tags.Contains("owned-trash-tag")),
                "Production trash metadata must preserve supplied metadata; optional upload time and tags may remain unknown.");

            phase = "scoped production trash search";
            CheckLease();
            var searchRequest = new TrashSearchRequest
            {
                Query = new TrashSearchQuery { Text = "trash" },
                Filter = new TrashSearchFilter { Ids = new List<string> { original.Id }, Filenames = new List<string> { ledger.Filename } },
                Fields = new TrashSearchFields { Includes = new List<string> { "id", "filename", "size", "deletion-date", "score" } },
                Sort = new TrashSearchSort { Field = "deletion-date", Order = "desc" }, Rows = 1
            };
            var search = await WaitForOwnedTrashSearch(sync, trash, searchRequest, original.Id,
                matched => matched.Hits == 1 && matched.Documents.Count == 1 && matched.Documents[0].Id == original.Id &&
                    matched.Documents[0].Filename == ledger.Filename && matched.Documents[0].Size == "latest-trash-payload".Length, token);
            TestContext.Out.WriteLine("Scoped trash search: hits " + search.Hits + ", page count " + search.Documents.Count +
                ", owned ID match " + (search.Documents.FirstOrDefault()?.Id == original.Id) +
                ", filename match " + (search.Documents.FirstOrDefault()?.Filename == ledger.Filename) +
                ", size " + (search.Documents.FirstOrDefault()?.Size?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "omitted") + ".");
            Check(search.Hits == 1 && search.Documents.Count == 1 && search.Documents[0].Id == original.Id &&
                search.Documents[0].Filename == ledger.Filename && search.Documents[0].Size == "latest-trash-payload".Length,
                "Trash search must match only the exact owned ID and latest filename/payload size.");
            Check(search.Documents[0].UploadDate == null && search.Documents[0].Tags == null,
                "Unrequested trash metadata must remain unknown.");
            searchRequest.Query = null;
            searchRequest.Fields = null;
            searchRequest.Rows = 0;
            searchRequest.Facets = new List<TrashSearchFacet> { new TrashSearchFacet { Field = "extension" } };
            search = await WaitForOwnedTrashSearch(sync, trash, searchRequest, original.Id,
                matched => matched.Hits == 1 && matched.Documents.Count == 0 && matched.Facets?["extension"]?["terms"] is Newtonsoft.Json.Linq.JArray facetTerms &&
                    facetTerms.Any(term => term.Type == Newtonsoft.Json.Linq.JTokenType.Object && term.Value<string>("term") == "txt" && term.Value<long?>("count") == 1), token);
            Check(search.Hits == 1 && search.Documents.Count == 0 && search.Facets != null,
                "Facet-only trash search must retain total hits and facet data without returning documents.");
            var terms = search.Facets["extension"]?["terms"] as Newtonsoft.Json.Linq.JArray;
            Check(terms != null && terms.Any(term => term.Type == Newtonsoft.Json.Linq.JTokenType.Object && term.Value<string>("term") == "txt" && term.Value<long?>("count") == 1),
                "The exact owned filename must contribute one txt extension facet.");
            search = await trash.SearchAsync(contextId, new TrashSearchRequest { Filter = new TrashSearchFilter { Ids = new List<string> { missing } } }, token);
            Check(search.Hits == 0 && search.Documents.Count == 0, "The independently verified missing ID must not match trash search.");

            phase = "selected trash restoration and exact partial failure";
            CheckLease();
            var result = sync ? trash.RestoreDocuments(contextId, new[] { original.Id, missing }, token)
                : await trash.RestoreDocumentsAsync(contextId, new[] { original.Id, missing }, token);
            Check(!result.AllSucceeded && result.FailedDocuments.SequenceEqual(new[] { missing }), "Restoration must report exactly the missing selection.");
            var restored = await client.Document.GetDocumentMetadataAsync<CopyMetadata>(contextId, original.Id, cancellationToken: token);
            Check(restored.Id == original.Id && restored.Owner == ownerId && restored.Version == latest && restored.Collections.Visible.Contains(ledger.Id) && restored.Tags.Contains("owned-trash-tag"),
                "Restoration must retain identity, ownership, latest version, existing collection and tags.");
            var history = await client.Document.GetDocumentVersionHistoryAsync(contextId, original.Id, token);
            Check(history.VersionDetails.Select(version => version.Version).OrderBy(version => version).SequenceEqual(new long?[] { original.Version, latest }), "Restoration must preserve both original versions.");
            await CheckPayload(original.Id, original.Version, "initial-trash-payload", token);
            await CheckPayload(original.Id, null, "latest-trash-payload", token);
            Check(!(await trash.GetDocumentsAsync(contextId, cancellationToken: token)).Any(document => document.Id == original.Id), "Restored document must no longer be in trash.");

            phase = "trash command pre-cancellation";
            using (var canceled = new CancellationTokenSource())
            {
                canceled.Cancel();
                await ExpectCanceled(() => trash.GetDocumentsAsync(contextId, cancellationToken: canceled.Token));
                await ExpectCanceled(() => trash.SearchAsync(contextId, new TrashSearchRequest(), canceled.Token));
                await ExpectCanceled(() => trash.RestoreDocumentsAsync(contextId, new[] { original.Id }, canceled.Token));
                await ExpectCanceled(() => trash.EraseDocumentsAsync(contextId, new[] { original.Id }, canceled.Token));
                await ExpectCanceled(() => trash.EraseAllDocumentsAsync(contextId, canceled.Token)); // Proven pre-cancelled; no whole-trash request is submitted.
            }
            await CheckPayload(original.Id, null, "latest-trash-payload", token);
            phase = "selected permanent trash erasure and exact partial failure";
            CheckLease();
            await client.Document.DeleteDocumentAsync(contextId, original.Id, token);
            result = sync ? trash.EraseDocuments(contextId, new[] { original.Id, missing }, token)
                : await trash.EraseDocumentsAsync(contextId, new[] { original.Id, missing }, token);
            Check(!result.AllSucceeded && result.FailedDocuments.SequenceEqual(new[] { missing }), "Permanent erasure must report exactly the missing selection.");
            Check(!(await trash.GetDocumentsAsync(contextId, cancellationToken: token)).Any(document => document.Id == original.Id), "The selected erased document must leave trash.");
            var absent = false;
            try { await client.Document.GetDocumentMetadataAsync(contextId, original.Id, token); }
            catch (NotFoundException) { absent = true; }
            Check(absent, "The permanently erased owned document must leave active metadata.");
        }

        private async Task<CenterDevice.Rest.Clients.Trash.TrashSearchResults> WaitForOwnedTrashSearch(bool sync,
            CenterDevice.Rest.Clients.Trash.TrashRestClient trash, TrashSearchRequest request, string ownedId,
            Func<CenterDevice.Rest.Clients.Trash.TrashSearchResults, bool> expected, CancellationToken token)
        {
            // API2.29 5.19.2 inherits 5.2.22's asynchronous indexing. Retry only this read-only selected-ID search.
            using (var indexing = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                indexing.CancelAfter(TimeSpan.FromSeconds(30));
                long? lastHits = null;
                int? lastCount = null;
                try
                {
                    while (true)
                    {
                        CheckLease();
                        var result = sync ? trash.Search(contextId, request, indexing.Token) : await trash.SearchAsync(contextId, request, indexing.Token);
                        lastHits = result.Hits; lastCount = result.Documents.Count;
                        Check(result.Hits <= 1 && result.Documents.All(document => document.Id == ownedId),
                            "Scoped search must never return another identity or multiple owned-ID hits.");
                        if (expected(result)) return result;
                        await Task.Delay(TimeSpan.FromSeconds(1), indexing.Token);
                    }
                }
                catch (OperationCanceledException) when (indexing.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    throw new VerificationFailure("Owned trash search indexing timed out after 30 seconds; last hits " +
                        (lastHits?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown") + ", page count " +
                        (lastCount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown") + ".");
                }
            }
        }
    }
}
