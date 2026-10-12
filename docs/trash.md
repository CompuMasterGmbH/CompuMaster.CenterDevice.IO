# Own-user trash operations

`CenterDeviceClientBase.Trash` supplies an additive `TrashRestClient` for API2.29 sections 5.19.1–5.19.5. Existing document, timeline and IO interfaces/defaults remain unchanged. Requires an admin, internal or external user; every user can access only their own trash, including administrators. The authentication-context ID is not inferred to be a trash-document owner ID.

`GetDocuments[Async]` retrieves one page in descending server deletion-time order. The offset is nonnegative and rows are between1 and the server's500-item maximum. The response supplies no hit count; a full page does not prove that another page exists. The read-only page preserves order and rejects absent listings, unusable/repeated IDs, negative sizes or a page larger than requested. `TrashDocument` retains omitted metadata as null, including64-bit sizes and timestamps with server offsets. Owner and version fields are not invented. Unknown tags differ from a known empty tag list.

The owned Scopevisio Teamwork bridge probe on 2026-10-12 returned the latest filename and size plus version/deletion timestamps, but omitted upload time and tags. These omitted fields remain null rather than being filled from active-document metadata or interpreted as empty. The manufacturer reference describes these fields; this observation applies to the tested bridge/account and does not establish direct CenterDevice response parity.

`RestoreDocuments[Async]` and `EraseDocuments[Async]` snapshot nonempty selected identifiers before authorization. Their `DocumentCommandResult` preserves exact failed-ID spelling/order for partial success and rejects a malformed failure response or an ID outside the selected request. Restoration attempts to retain metadata, history and properties from deletion, but collections or other resources removed afterward cannot be restored. Selected erasure permanently removes all versions and representations; the data cannot be restored.

`EraseAllDocuments[Async]` is a separate explicitly named bodyless DELETE of the whole own-user trash. It includes documents unrelated to the caller's workflow. Use only selected erasure for test cleanup; no whole-trash mutation is part of the integration fixture. A pre-cancelled whole-trash call exercises only cancellation before authorization/dispatch.

Cancellation reaches authorization, request admission and native HTTP I/O, but does not establish whether an already submitted write committed. Restore/erase side effects are idempotent, while partial-failure responses can change between calls. Writes are not automatically replayed after uncertain outcomes; callers must reconcile partial success.

Isolated tests cover defaults/bounds, omission versus empty, large metadata, exact nested command bodies, distinct whole-trash shape, snapshotting, partial errors, roles/missing/transient errors, cancellation, immutable result lists and compatibility. Actual private loopback probes verify the public factory/default serializer with UTF-8 metadata and no POST/DELETE replay after503. The explicit owned Teamwork fixture verifies only selected restoration/erasure and metadata/history/payload/identity preservation, with journal recovery and independent finally cleanup; live evidence must be recorded against its exact tested head.

## Search

`Search[Async]` accepts a `TrashSearchRequest`. The query, metadata filters, field selection, ordering and requested facets are snapshotted before authorization. A default request sends `params: {}` so that query-dependent server defaults remain intact. Null options are omitted; explicit empty query text, lists and field projections remain explicit. `rows=0` requests hits and facets without document entries; a positive request can be capped by the server, and no 500-row search cap is inferred from the separate GET contract.

```csharp
var results = await client.Trash.SearchAsync(contextId, new TrashSearchRequest
{
    Query = new TrashSearchQuery { Text = "invoice" },
    Filter = new TrashSearchFilter
    {
        DeletionDate = new TrashSearchDateRange { From = DateTimeOffset.UtcNow.AddDays(-7) }
    },
    Sort = new TrashSearchSort { Field = "deletion-date", Order = "desc" },
    Rows = 25
}, cancellationToken);
```

Only the trash-specific fields are accepted. General-document collection/ownership filters, named queries and negation are unavailable. Size/date ranges support either inclusive bound; reversed bounds are rejected. Includes/excludes are mutually exclusive. Duplicate facets require distinct qualifiers on every occurrence. Facet sizes are positive and only affect term facets; range facets ignore them.

Results retain a 64-bit total hit count separately from the read-only page. Omitted document arrays are accepted only for zero hits, out-of-bounds offsets or explicit zero rows. Omitted metadata stays nullable; explicitly excluding `id` may leave it unknown. Supplied IDs must remain distinct, including GUID casing variants. Scores, timestamp offsets and 64-bit sizes are preserved without inventing owner/version fields. Facet data is a defensive `JObject` snapshot retaining complete term/range data, qualifiers, hidden counts, zero counts and numeric or textual range boundaries; missing facets remain different from an empty object. Existing expired-token refresh is retained, while uncertain HTTP outcomes are not replayed.

Search inherits the manufacturer's asynchronous indexing behavior: changes and latest versions may briefly lag behind metadata-by-ID. The client returns the server's current result without retrying for visibility. The owned integration fixture waits at most 30 seconds for its selected-ID hit and facet state, using only read-only searches with cancellation/lease checks; errors and unexpected identities fail immediately, and writes are not retried.

The search contract has isolated and actual private-loopback evidence. Its owned Teamwork server acceptance must be recorded separately against the exact tested head; earlier listing/restore/erase evidence does not prove the new search route.

Other administration/audit contracts, direct manufacturer authentication, other-role enforcement, publication and default DMS adoption remain separate. This bounded client does not imply complete API coverage.
