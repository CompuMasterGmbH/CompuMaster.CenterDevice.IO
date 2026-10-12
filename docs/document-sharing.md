# Document sharing commands

API 2.29 sections 5.2.23–5.2.26 define selected and bulk document sharing/unsharing. Invitation error codes come from section 5.8.5.

Concrete DocumentRestClient and DocumentsRestClient now expose synchronous and native asynchronous Share, Unshare and explicit UnshareAll methods. Existing interfaces, methods, default metadata fields and published package pins remain unchanged.

- Share accepts explicit user IDs, group IDs and e-mail addresses plus an optional notification comment. Unknown addresses can cause invitations; notifications and tenant access restrictions are server behavior.
- Unshare requires at least one explicit recipient. Null/empty recipient selections are rejected before authorization, preventing accidental removal of all direct sharings.
- UnshareAll explicitly omits recipient fields. The owner retains access; inherited access can remain. No invitations are sent by unsharing.
- Inputs are snapshotted once before authorization. Null lists/comment are omitted; empty lists/comment and original values are preserved. Invalid e-mail syntax is sent to the server to obtain its specific invitation/unsharing failures.
- HTTP 204 reports complete success. HTTP 200 can report successful invitations, invitation failures, or failed selected documents/users/groups/e-mails. Read-only results preserve server order, spelling and future numeric invitation error codes. Invitations alone count as success. Malformed or contradictory outcomes do not count as success and retain their parsing cause.
- Matching response IDs allows equivalent GUID spelling. E-mail reconciliation allows case differences. Failure identity must belong to the submitted selection; duplicate and contradictory invitations are rejected.
- Native cancellation covers authorization/admission/HTTP. Partial failures and uncertain mutations are not replayed. Cancellation cannot prove that the server made no changes.

Isolated fixtures and actual private loopback HTTP probes exercise wire format, response reconciliation, omission, snapshots, cancellation, error causes and no uncertain write replay. These do not verify actual tenant roles, invitation delivery, permission inheritance or Teamwork server behavior. Real acceptance requires a coordinated exclusive window and cleanup limited to exact owned documents/collections/invitations; ordinary tests must not invite arbitrary users or erase broad sharing state. No package publication or default DMS adoption is implied.
