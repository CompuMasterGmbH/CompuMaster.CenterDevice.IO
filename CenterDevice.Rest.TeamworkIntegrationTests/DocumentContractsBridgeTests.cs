using CenterDevice.Rest.Clients.Documents;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CenterDevice.Rest.TeamworkIntegrationTests
{
    public partial class DocumentVersionBridgeTests
    {
        [TestCase(false), TestCase(true)]
        [Explicit("Requires coordinated exclusive access across local sessions and CI until verified cleanup.")]
        public async Task ConfiguredUploadsMetadataAndSearchThroughTeamwork(bool synchronous)
            => await RunWithOwnedCollection(synchronous, false, documentContracts: true);

        private async Task VerifyDocumentContracts(bool sync, CancellationToken token)
        {
            phase = "configured upload intent";
            ledger.Filename = "contract-" + Guid.NewGuid().ToString("N") + ".txt";
            ledger.AllowedFilenames.Add(ledger.Filename); // Recovery also covers an unexpected second created duplicate.
            SaveLedger();
            Check(!(await client.Documents.GetDocumentPageAsync(contextId, new DocumentMetadataListingOptions { Collection = ledger.Id, Rows = 10 }, token)).Documents.Any(), "Fresh owned collection must contain no documents.");
            var originalDate = "2025-04-05T06:07:08+05:30";
            var options = new DocumentUploadOptions
            {
                MimeType = "text/plain; charset=utf-8",
                Document = new DocumentUploadMetadata { Title = "Owned contract title", Author = "Owned contract author", DocumentDate = originalDate },
                Actions = new DocumentUploadActions { AddToCollections = new[] { ledger.Id }, CheckForDuplicate = new DocumentUploadDuplicateCheck() }
            };
            CheckLease(); phase = "configured document upload";
            var uploaded = sync ? client.Documents.UploadConfiguredDocument(contextId, ledger.Filename, () => Payload("Owned contract payload"), options, token)
                : await client.Documents.UploadConfiguredDocumentAsync(contextId, ledger.Filename, () => Payload("Owned contract payload"), options, token);
            Check(!uploaded.DuplicateDetected && !string.IsNullOrWhiteSpace(uploaded.Id), "Fresh upload must create one identified document.");
            ledger.Documents.Add(uploaded.Id); SaveLedger();
            var details = sync ? client.Document.GetDocumentDetails(contextId, uploaded.Id, cancellationToken: token)
                : await client.Document.GetDocumentDetailsAsync(contextId, uploaded.Id, cancellationToken: token);
            Check(details.Id == uploaded.Id && details.Owner == ownerId && details.Filename == ledger.Filename, "Complete selected metadata must retain owned document identity.");
            Check(details.Title == "Owned contract title" && details.Author == "Owned contract author", "Explicit title and author must survive the actual upload.");
            Check(DateTimeOffset.TryParse(details.DocumentDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var actualDate) && actualDate == DateTimeOffset.Parse(originalDate, CultureInfo.InvariantCulture), "Stored original document date must retain its instant.");
            await CheckPayload(uploaded.Id, null, "Owned contract payload", token);

            CheckLease(); phase = "explicit duplicate detection";
            var duplicate = sync ? client.Documents.UploadConfiguredDocument(contextId, ledger.Filename, () => Payload("Owned contract payload"), options, token)
                : await client.Documents.UploadConfiguredDocumentAsync(contextId, ledger.Filename, () => Payload("Owned contract payload"), options, token);
            if (!duplicate.DuplicateDetected && !string.IsNullOrWhiteSpace(duplicate.Id)) { ledger.Documents.Add(duplicate.Id); SaveLedger(); }
            Check(duplicate.DuplicateDetected && duplicate.Id == null && duplicate.Duplicates.Contains(uploaded.Id), "Explicit duplicate detection must identify the existing owned document without creating one.");
            var inventory = await client.Documents.GetDocumentPageAsync(contextId, new DocumentMetadataListingOptions { Collection = ledger.Id, Rows = 10 }, token);
            Check(inventory.Hits == "1" && inventory.Documents.Count == 1 && inventory.Documents[0].Id == uploaded.Id, "Duplicate detection must leave exactly one document in the complete owned collection.");
            var pageOptions = new DocumentMetadataListingOptions { Collection = ledger.Id, Ids = new[] { uploaded.Id }, Offset = 0, Rows = 10 };
            var page = sync ? client.Documents.GetDocumentPage(contextId, pageOptions, token) : await client.Documents.GetDocumentPageAsync(contextId, pageOptions, token);
            Check(page.Documents.Count == 1 && page.Documents[0].Id == uploaded.Id && page.Hits == "1", "Current metadata page must identify exactly one matching owned document.");

            var marker = "owned-" + Guid.NewGuid().ToString("N");
            CheckLease(); phase = "extended metadata set";
            var update = new DocumentMetadataUpdateRequest { Set = new JObject { ["user.strings.bridge-contract"] = marker, ["user.numbers.bridge-number"] = "123.45" } };
            if (sync) client.Document.UpdateExtendedMetadata(contextId, uploaded.Id, update, token); else await client.Document.UpdateExtendedMetadataAsync(contextId, uploaded.Id, update, token);
            details = await client.Document.GetDocumentDetailsAsync(contextId, uploaded.Id, cancellationToken: token);
            Check((string)details.GetExtendedMetadata()?["user"]?["strings"]?["bridge-contract"] == marker && (string)details.GetExtendedMetadata()?["user"]?["numbers"]?["bridge-number"] == "123.45", "Version-independent extended metadata must match the explicit string and decimal values.");

            CheckLease(); phase = "configured version upload";
            var versionOptions = new DocumentUploadOptions { MimeType = "text/plain", Document = new DocumentUploadMetadata { Title = "Owned new-version title", Author = "Owned new-version author", DocumentDate = originalDate } };
            var version = sync ? client.Document.UploadConfiguredVersion(contextId, uploaded.Id, ledger.Filename, () => Payload("Owned new-version payload"), versionOptions, token)
                : await client.Document.UploadConfiguredVersionAsync(contextId, uploaded.Id, ledger.Filename, () => Payload("Owned new-version payload"), versionOptions, token);
            details = await client.Document.GetDocumentDetailsAsync(contextId, uploaded.Id, cancellationToken: token);
            Check(details.Id == uploaded.Id && details.Owner == ownerId && details.Version == version.Version && details.Title == "Owned new-version title" && details.Author == "Owned new-version author", "Configured version must preserve identity and expose confirmed current metadata.");
            Check((string)details.GetExtendedMetadata()?["user"]?["strings"]?["bridge-contract"] == marker, "Extended metadata must remain version-independent after a new version.");
            await CheckPayload(uploaded.Id, null, "Owned new-version payload", token);

            phase = "bounded indexing wait and typed search";
            var search = new DocumentSearchParameters { Filter = new DocumentSearchFilterParameters { Ids = new[] { uploaded.Id }, Collections = new[] { ledger.Id }, Owners = new[] { ownerId } }, Offset = 0, Rows = 10, Sort = new DocumentSearchSortParameters { Field = "filename", Order = "asc" } };
            var until = DateTimeOffset.UtcNow.AddSeconds(90); DocumentQueryPage found;
            do
            {
                CheckLease(); found = sync ? client.Documents.SearchDocumentDetails(contextId, search, token) : await client.Documents.SearchDocumentDetailsAsync(contextId, search, token);
                if (found.Documents.Count == 1 && found.Documents[0].Version == version.Version) break;
                await Task.Delay(1000, token);
            } while (DateTimeOffset.UtcNow < until);
            Check(found.Hits == "1" && found.Documents.Count == 1 && found.Documents[0].Id == uploaded.Id && found.Documents[0].Version == version.Version, "Indexed filtered search must return the latest owned version within the explicit acceptance timeout.");

            CheckLease(); phase = "extended metadata unset";
            var unset = new DocumentMetadataUpdateRequest { Unset = new[] { "user.strings.bridge-contract", "user.numbers.bridge-number" } };
            if (sync) client.Document.UpdateExtendedMetadata(contextId, uploaded.Id, unset, token); else await client.Document.UpdateExtendedMetadataAsync(contextId, uploaded.Id, unset, token);
            details = await client.Document.GetDocumentDetailsAsync(contextId, uploaded.Id, cancellationToken: token);
            Check(details.GetExtendedMetadata()?["user"]?["strings"]?["bridge-contract"] == null && details.GetExtendedMetadata()?["user"]?["numbers"]?["bridge-number"] == null, "Explicit owned extended fields must be removed.");
            TestContext.Out.WriteLine("Owned configured upload/version, duplicate outcome, selected/current metadata, extended set/unset and bounded latest-version search verified; exact cleanup follows.");
        }
    }
}
