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
    }
}
