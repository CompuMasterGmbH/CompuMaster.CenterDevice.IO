# Collection archive configuration

Additive sync/native asynchronous expiry, archived-date source and freeze commands cover API 2.29 sections 5.3.15–5.3.17. Existing `ArchiveCollection` methods (5.3.14), interfaces, models and defaults remain unchanged. Each operation selects an escaped collection ID and requires HTTP 204; no permission/state lookup, grant change or speculative replay occurs.

Expiry requires an explicit ISO8601 date or complete positive-integer period and explicit action. Original date strings remain unchanged; the server computes expiry in Europe/Berlin. Period unit catalogs are days/weeks/months/years, with optional calendar choice (server default false). Typed Int64 amounts are convenient; complete raw JSON preserves larger positive integers without inventing a manufacturer width. Known structure, exclusive date/period selection and positive amounts are checked. Future unit/action strings remain server-authoritative.

Expiry actions include no-action, remove-from-collection, move-to-trash and permanent delete. No action is selected automatically. Null/empty setting requests do not implicitly unset expiry: `RemoveArchiveExpiry` explicitly sends `set-expiry` with an empty parameter object, removing date/period and action.

`SetArchivedDateSource` sends the original namespace.section.field key, currently user.dates fields. The server validates date-field support and uses its current date/time when an added document lacks that field. No document metadata is fetched or default timestamp inferred by the client. `RemoveArchivedDateSource` explicitly sends an empty parameter object.

`SetArchiveFrozen` preserves true/false. The server permits unfreezing only for an owner in unarchiving mode; outside that mode a frozen configuration rejects subsequent requests, including another freeze request. The client does not enable unarchiving, change tenant grants or replay this state-dependent operation. Ownership, internal/admin role, archiving whitelist, archived state and configuration restrictions remain server-authoritative.

Cancellation covers authorization/admission/I/O and cannot establish that no archive configuration changed. Isolated fixtures/private TCP loopback do not change real archives or expiry actions. Real acceptance must use exact-owned resources and establish cleanup/unarchiving capability before irreversible archiving or freezing; that remains separate in #19/#15, along with publication and downstream adoption.
