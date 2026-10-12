# Explicit bulk document membership

Additive collection `AddDocumentsToCollection`/`RemoveDocumentsFromCollection` and folder `AddDocuments`/`RemoveDocuments` sync/native asynchronous methods cover API 2.29 sections 5.3.3/5.3.4/5.4.4/5.4.5. Existing methods/interfaces and their defaults remain unchanged. Explicit nonempty document selections are copied before authorization; original IDs/order/duplicates are retained. Selected collection/folder IDs are escaped as one path segment.

Collection commands preserve document identities and modify references only. Removing collection membership also removes folder references within that collection, without erasing original documents or references in other collections. The server enforces visible collections/documents and sharing rights; guests can change only collections they own. HTTP 204 confirms success, while HTTP 200 returns original failed-document identifiers without claiming an atomic all-or-nothing result.

Adding to a folder adds collection membership when absent and moves an existing reference out of another folder in the same collection. It does not create a second document identity. The server permits administrators, internal and external users and requires visibility. Folder addition requires HTTP 204.

Folder removal retains collection membership when `removeFromCollection` is false or omitted. Explicit true also removes collection membership, which can partially fail with HTTP 200. Such failure identifiers refer to collection removal; the folder reference can already be removed. The client does not attempt to reconstruct previous references or replay partial changes.

`DocumentMembershipResult` exposes immutable original failures and aggregate reported success, without inferred ownership, locations or transactionality. Malformed success JSON, missing failure arrays, duplicate properties, trailing content and unselected failure identities retain inner diagnostics. GUID identity equivalence is accepted while original failure strings/order/duplicates remain unchanged. Other HTTP statuses remain failures. No upload, copy, erasure, extra sharing request or speculative mutation replay is added; cancellation cannot establish that no references changed.

Isolated fixtures/private TCP loopback cover original actions/IDs, nullable folder-removal choices, partial failures, snapshots and native cancellation. They do not prove live reference topology or permissions; exact-owned coordinated server acceptance remains separate in #19/#15. Publication and downstream default adoption remain separate.
