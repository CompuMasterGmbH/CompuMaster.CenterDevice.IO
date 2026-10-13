using CenterDevice.Rest.Clients.Documents;
using CenterDevice.Rest.Clients.Timeline;
using CenterDevice.Rest.Exceptions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    public partial class DocumentVersionBridgeTests
    {
        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task OwnedResourceContractsThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false, resourceContracts: true);

        [TestCase(false), TestCase(true)]
        [Explicit("Records the canonical four-segment comment GET independently of other owned resource acceptance.")]
        public async Task SelectedOwnedCommentReadThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false, resourceContracts: true, selectedCommentRead: true);

        private async Task VerifyResourceContracts(bool sync, CancellationToken token, bool selectedCommentRead)
        {
            var from = DateTimeOffset.UtcNow.AddHours(-1).ToString("o", CultureInfo.InvariantCulture);
            var to = DateTimeOffset.UtcNow.AddHours(1).ToString("o", CultureInfo.InvariantCulture);
            var firstName = "resource-" + Guid.NewGuid().ToString("N") + ".txt";
            var payload = "Owned resource payload " + Guid.NewGuid().ToString("N");
            var first = await UploadResourceDocument(firstName, payload, sync, token);
            var marker = "api229-owned-" + Guid.NewGuid().ToString("N");
            CheckLease(); ResourcePhase("owned tag setup");
            var tags = sync ? client.Document.AddTags(contextId, first, new[] { marker }, token)
                : await client.Document.AddTagsAsync(contextId, first, new[] { marker }, token);
            Check(tags.AllSucceeded, "Owned tag addition must succeed.");
            ResourcePhase("deprecated tag visibility");
            await WaitResourceRead(async () =>
            {
#pragma warning disable CS0618 // Explicit manufacturer-deprecated endpoint acceptance.
                var visible = sync ? client.Tags.GetTags(contextId, token) : await client.Tags.GetTagsAsync(contextId, token);
#pragma warning restore CS0618
                return visible.Tags.Contains(marker);
            }, "The exact owned tag must become visible without modifying unrelated tags.", token);

            var originalText = "<b>Owned comment " + Guid.NewGuid().ToString("N") + " ä</b>";
            var updatedText = "<b>Owned update " + Guid.NewGuid().ToString("N") + " ä</b>";
            CheckLease(); ResourcePhase("owned comment creation");
            var created = sync ? client.Comments.AddComment(contextId, first, originalText, token)
                : await client.Comments.AddCommentAsync(contextId, first, originalText, token);
            Check(!string.IsNullOrWhiteSpace(created.Id), "Comment creation must confirm an identity.");
            if (selectedCommentRead)
            {
                ResourcePhase("selected owned comment read");
                var selected = sync ? client.Comments.GetComment(contextId, first, created.Id, token)
                    : await client.Comments.GetCommentAsync(contextId, first, created.Id, token);
                Check(selected.Id == created.Id && selected.Author == ownerId && selected.Text == originalText,
                    "Selected comment identity, actual author and original HTML must agree.");
                return;
            }
            ResourcePhase("owned comment list");
            var initialComments = sync ? client.Comments.GetComments(contextId, first, token)
                : await client.Comments.GetCommentsAsync(contextId, first, token);
            Check(initialComments.Comments.Count == 1 && initialComments.Comments[0].Id == created.Id
                && initialComments.Comments[0].Author == ownerId && initialComments.Comments[0].Text == originalText,
                "The fresh document comment list must confirm the actual author and original HTML.");
            CheckLease(); ResourcePhase("owned comment update");
            if (sync) client.Comments.UpdateComment(contextId, first, created.Id, updatedText, token);
            else await client.Comments.UpdateCommentAsync(contextId, first, created.Id, updatedText, token);
            var comments = sync ? client.Comments.GetComments(contextId, first, token)
                : await client.Comments.GetCommentsAsync(contextId, first, token);
            Check(comments.Comments.Count == 1 && comments.Comments[0].Id == created.Id && comments.Comments[0].Text == updatedText,
                "A fresh owned document must retain exactly its updated owned comment.");
            CheckLease(); ResourcePhase("owned comment removal");
            if (sync) client.Comments.RemoveComment(contextId, first, created.Id, token);
            else await client.Comments.RemoveCommentAsync(contextId, first, created.Id, token);
            Check(!(await client.Comments.GetCommentsAsync(contextId, first, token)).Comments.Any(), "Owned comment removal must be verified.");

            ResourcePhase("owned timeline search");
            TimelineEventPage events = null;
            var search = new TimelineSearchRequest { DocumentIds = new List<string> { first }, Users = new List<string> { ownerId },
                Timestamp = new TimelineTimestampRange { From = from, To = to }, Offset = 0, Rows = 1000 };
            await WaitResourceRead(async () =>
            {
                events = sync ? client.Timeline.SearchTimeline(contextId, search, token) : await client.Timeline.SearchTimelineAsync(contextId, search, token);
                return events.Entries.Count != 0;
            }, "Owned document events must become visible within the bounded indexing wait.", token);
            Check(events.Entries.All(entry => entry.Creator == ownerId && (string)entry.Details?["document-id"] == first),
                "Timeline filters must return only the actual creator and owned document.");
            Check(events.Entries.All(entry => DateTimeOffset.TryParse(entry.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp)
                && timestamp >= DateTimeOffset.Parse(from, CultureInfo.InvariantCulture) && timestamp < DateTimeOffset.Parse(to, CultureInfo.InvariantCulture)),
                "Original timeline timestamps must lie within the explicit inclusive/exclusive interval.");
            ResourcePhase("owned timeline scroll");
            var scroll = new TimelineScrollRequest { DocumentIds = new List<string> { first }, Users = new List<string> { ownerId }, Rows = 1, Sort = "desc" };
            var page = sync ? client.Timeline.ScrollTimeline(contextId, scroll, token) : await client.Timeline.ScrollTimelineAsync(contextId, scroll, token);
            Check(page.Entries.Count == 1, "The first bounded owned timeline scroll must identify one event.");
            var previous = page.Entries[0].Id; scroll.PreviousId = previous;
            var next = sync ? client.Timeline.ScrollTimeline(contextId, scroll, token) : await client.Timeline.ScrollTimelineAsync(contextId, scroll, token);
            Check(next.Entries.All(entry => entry.Id != previous && entry.Creator == ownerId && (string)entry.Details?["document-id"] == first),
                "The explicit next page must exclude its cursor and preserve owned filters.");

            ResourcePhase("owned deprecated original ZIP");
#pragma warning disable CS0618 // Explicit manufacturer-deprecated endpoint acceptance.
            using (var download = sync ? client.Documents.DownloadDocumentsAsZipLegacy(contextId, new[] { first }, cancellationToken: token)
                : await client.Documents.DownloadDocumentsAsZipLegacyAsync(contextId, new[] { first }, cancellationToken: token))
#pragma warning restore CS0618
            using (var zip = new ZipArchive(download, ZipArchiveMode.Read))
            {
                Check(zip.Entries.Count == 1 && zip.Entries[0].Name == firstName, "The original ZIP must contain exactly the selected owned file.");
                using (var reader = new StreamReader(zip.Entries[0].Open())) Check(await reader.ReadToEndAsync() == payload, "Original ZIP payload must remain exact.");
            }

            var second = await UploadResourceDocument("resource-" + Guid.NewGuid().ToString("N") + ".txt", "Owned second payload " + Guid.NewGuid().ToString("N"), sync, token);
            Check(first != second, "Fresh owned uploads must have distinct identities.");
            CheckLease(); ResourcePhase("selected owned trash movement");
            var moved = sync ? client.Document.MoveDocumentToTrash(contextId, first, token) : await client.Document.MoveDocumentToTrashAsync(contextId, first, token);
            Check(moved.AllSucceeded, "Selected owned trash movement must report success.");
            await RequireResourceMissing(first, token);
            Check((await client.Trash.List(contextId, token)).Any(document => document.Id == first && document.Filename == firstName), "Selected owned document must be identifiable in trash.");
            CheckLease(); ResourcePhase("selected owned restoration");
            var restored = await ((CenterDevice.Rest.Clients.CenterDeviceClientBase)client).Trash.RestoreDocumentsAsync(contextId, new[] { first }, token);
            Check(restored.AllSucceeded, "Owned selected restoration must report success.");
            await CheckPayload(first, null, payload, token);
            CheckLease(); ResourcePhase("bulk owned trash movement with detailed outcomes");
            var bulk = sync ? client.Documents.MoveDocumentsToTrash(contextId, new[] { first, second }, true, token)
                : await client.Documents.MoveDocumentsToTrashAsync(contextId, new[] { first, second }, true, token);
            Check(bulk.AllSucceeded, "Bulk owned trash movement must report success.");
            await RequireResourceMissing(first, token); await RequireResourceMissing(second, token);
            var ownedTrash = (await client.Trash.List(contextId, token)).Where(document => document.Id == first || document.Id == second).ToList();
            Check(ownedTrash.Count == 2 && ownedTrash.All(document => OwnedFilename(document.Filename)), "Both exact owned documents must be present in trash.");
            CheckLease(); ResourcePhase("owned restoration before direct erasure");
            var restoredForErasure = await ((CenterDevice.Rest.Clients.CenterDeviceClientBase)client).Trash.RestoreDocumentsAsync(contextId, new[] { first, second }, token);
            Check(restoredForErasure.AllSucceeded, "Both owned documents must be restored before exercising direct active-document erasure.");
            await CheckPayload(first, null, payload, token);
            var activeSecond = await client.Document.GetDocumentDetailsAsync(contextId, second, cancellationToken: token);
            Check(activeSecond.Id == second && activeSecond.Owner == ownerId && OwnedFilename(activeSecond.Filename),
                "Second restored document must retain its confirmed owned identity.");
            CheckLease(); ResourcePhase("selected direct owned erasure");
            var erased = sync ? client.Document.EraseDocument(contextId, first, token) : await client.Document.EraseDocumentAsync(contextId, first, token);
            Check(erased.AllSucceeded, "Selected direct owned erasure must report success.");
            CheckLease(); ResourcePhase("bulk direct owned erasure");
            var erasedBulk = sync ? client.Documents.EraseDocuments(contextId, new[] { second }, true, token) : await client.Documents.EraseDocumentsAsync(contextId, new[] { second }, true, token);
            Check(erasedBulk.AllSucceeded, "Bulk direct owned erasure must report success.");
            await RequireResourceMissing(first, token); await RequireResourceMissing(second, token);
            Check(!(await client.Trash.List(contextId, token)).Any(document => document.Id == first || document.Id == second), "Direct erasure must remove both owned trash entries.");
        }

        private void ResourcePhase(string value)
        { phase = value; TestContext.Progress.WriteLine("Owned resource phase: " + value + "."); }

        private async Task<string> UploadResourceDocument(string name, string payload, bool sync, CancellationToken token)
        {
            ResourcePhase("owned resource upload intent");
            ledger.Filename = name; ledger.AllowedFilenames.Add(name); SaveLedger();
            var existing = await client.Documents.GetDocumentPageAsync(contextId, new DocumentMetadataListingOptions { Collection = ledger.Id, Rows = 20 }, token);
            Check(existing.Hits == existing.Documents.Count.ToString(CultureInfo.InvariantCulture) && existing.Documents.Count == ledger.Documents.Count
                && existing.Documents.All(document => ledger.Documents.Contains(document.Id)) && !existing.Documents.Any(document => document.Filename == name),
                "Owned upload intent requires complete known inventory and exact filename absence.");
            CheckLease(); ResourcePhase("fresh owned resource upload");
            var options = new DocumentUploadOptions { MimeType = "text/plain; charset=utf-8", Actions = new DocumentUploadActions { AddToCollections = new[] { ledger.Id } } };
            var created = sync ? client.Documents.UploadConfiguredDocument(contextId, name, () => Payload(payload), options, token)
                : await client.Documents.UploadConfiguredDocumentAsync(contextId, name, () => Payload(payload), options, token);
            Check(!string.IsNullOrWhiteSpace(created.Id) && !created.DuplicateDetected, "Fresh owned upload must confirm one created identity.");
            ledger.Documents.Add(created.Id); SaveLedger();
            var metadata = await client.Document.GetDocumentDetailsAsync(contextId, created.Id, cancellationToken: token);
            Check(metadata.Id == created.Id && metadata.Owner == ownerId && metadata.Filename == name, "Owned upload identity/name/owner must agree before other commands.");
            return created.Id;
        }
        private async Task RequireResourceMissing(string id, CancellationToken token)
        { try { await client.Document.GetDocumentDetailsAsync(contextId, id, cancellationToken: token); } catch (NotFoundException) { return; } throw new VerificationFailure("Removed owned document must be absent from active metadata."); }
        private async Task WaitResourceRead(Func<Task<bool>> read, string message, CancellationToken token)
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(90);
            while (true) { CheckLease(); if (await read()) return; Check(DateTimeOffset.UtcNow < until, message); await Task.Delay(1000, token); }
        }
    }
}
