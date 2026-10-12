# Document reminders

`client.Reminders` implements API 2.29 sections 5.25.1–5.25.5 through synchronous and native asynchronous methods. Existing factories, interfaces and default behavior remain unchanged.

| Workflow | Method | Protocol |
|---|---|---|
| Create a future document reminder | CreateReminder[Async] | POST reminders, 201 ID |
| Retrieve a selected own reminder | GetReminder[Async] | GET reminder/id, 200 |
| Retrieve latest own reminders | GetReminders[Async] | GET reminders, optional statuses/entity-id |
| Complete a triggered own reminder | CompleteReminder[Async] | POST reminder/id, action complete, 204 |
| Remove a selected own reminder | RemoveReminder[Async] | bodyless DELETE reminder/id, 204 |

Creation requires a visible document and a future `DateTimeOffset` expiry, sent with its explicit offset. Null note omits it, while an empty note remains explicit. The SDK's note guard limits .NET string length to 10000 UTF-16 code units. The server revalidates expiry after authorization and governs notifications according to user/tenant mail settings. Creation is non-idempotent; retain the returned ID for reconciliation and cleanup. Completion is non-idempotent and requires a triggered reminder; pending/completed/repeat errors remain server errors. Removal preserves the document.

Only the creator can retrieve, complete or remove the reminder. The returned schema has no creator/recipient identifiers, so no owner is inferred from the authentication context. Read-only models retain exact reminder/entity identities, original status/type strings (including unknown future values), note omission versus empty and ISO8601 text. Explicit offsets expose `DateTimeOffset`; offset-free dates do not acquire the workstation timezone. Successful metadata responses must confirm the selected identity and required fields.

The list endpoint returns the latest **at most 500** matching reminders and provides no paging/total/completeness flag. It is not a complete history; even a full page does not prove absence of older entries. Status filters accept pending/triggered/completed, snapshot the sequence once, preserve explicit duplicates and distinguish null omission from an explicit empty sequence. The optional entity filter remains distinct from the authentication context. Read results preserve server order, unique identities and selected filters.

Native cancellation reaches authorization/admission/HTTP; original errors and existing expired-token refresh remain. Existing bounded GET-read retries are preserved. Uncertain POST/DELETE mutations are not replayed. No automatic state transition, waiting for notifications or hidden iteration is introduced.

Isolated contracts cover all five sync/async workflows, date offsets/text, unknown metadata, identities, filter snapshots/empty values, latest-list cap, malformed success/error and cancellation. Actual public-factory TCP probes cover all five wire workflows and GET 503 retry versus create/complete/delete 503 without replay. No real reminder, document, mail or server is touched by these probes. Actual own-reminder lifecycle, trigger timing/completion, creator visibility and notification behavior require coordinated live acceptance and exact owned cleanup separately. Post-merge matrix, publication and default DMS package adoption remain separate prerequisites.
