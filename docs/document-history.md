# Document version history

API 2.29 section 5.2.20 exposes `version-details` through document metadata. `DocumentRestClient.GetDocumentVersionHistory` and its native asynchronous counterpart request only `id`, `version` and `version-details`. Existing full/basic metadata overloads and interfaces keep their previous field sets and defaults.

The response binds history to its selected document ID and current version. Each `DocumentVersionMetadata` entry contains the original version number, uploader, filename, size, timestamp, MIME type, title, author and page count when supplied. Version numbers and sizes use 64-bit values; optional numeric/date fields are nullable. The SDK preserves server order and values without sorting or inventing missing versions.

```csharp
var history = await client.Document.GetDocumentVersionHistoryAsync(contextUserId, documentId, cancellationToken);
if (history.VersionDetails != null)
{
    foreach (var version in history.VersionDetails)
    {
        // Use history.Id with the selected version.Version for version-specific downloads/actions.
    }
}
```

Null `VersionDetails` means the field was omitted or null. The server may omit empty metadata, and guests do not receive older-version information. An explicitly empty list is preserved, but neither shape establishes that a caller sees the complete document history. Uploader IDs can be the `not-visible` placeholder; do not interpret it as the authentication-context ID or a resolvable user. This helper does not infer role grants or document ownership.

The method is read-only, forwards cancellation through authorization/admission/HTTP I/O and uses existing visibility/not-found/server error handling. It rejects a missing document ID before authorization. Direct CenterDevice and Teamwork retain their own configured endpoints and authentication contexts; bridge verification does not establish direct OAuth login.
