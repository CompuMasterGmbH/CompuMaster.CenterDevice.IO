# Metadata-template management

API 2.29 sections 5.26.1–5 are available through the additive `MetadataTemplates` factory and synchronous/native asynchronous methods on `MetadataTemplatesRestClient`. Management creates or changes template definitions; it does not apply metadata to documents or infer tenant feature availability.

| Operation | Route | Success |
|---|---|---|
| CreateMetadataTemplate | POST metadata-templates | 201, confirmed id |
| GetMetadataTemplate | GET metadata-template/id | 200, original detail |
| GetMetadataTemplates | GET metadata-templates | 200, server-selected list |
| UpdateMetadataTemplate | PUT metadata-template/id | 204 |
| RemoveMetadataTemplate | DELETE metadata-template/id | 204 |

Creation provides current type `user`, an explicit name and complete fields/sharings arrays; empty strings/arrays remain explicit. Fields use full well-known `user.strings.`, `user.numbers.` or `user.dates.` keys with a usable tail. Optional default text is retained exactly: strings, decimal text including precision/trailing zeros and ISO8601 date text are not converted or normalized. Null omits the default. Server field-schema/default validation remains authoritative.

Sharing definitions select exact entity ID, viewer/editor/coowner right and user/group/role type. Rights are hierarchical. Owners/coowners/editors/viewers can view template detail; only owners/coowners can see sharing details. Editors can update fields; only owners/coowners may update sharings or remove the template. No right/owner is inferred from the authenticated caller.

Metadata is immutable and retains original id/name/type, owner ID, caller sharing-right, ISO8601 creation/modification text and ordered fields/sharings. Explicit timezone offsets become nullable DateTimeOffset values; offset-free dates retain raw text without acquiring a workstation timezone. Future response type/right/entity/field names remain original strings. Hidden/absent/JSON-null sharings remain null; explicit empty sharings are a read-only empty list. Hidden sharings never mean unshared and must not be rewritten as an invented empty array.

Updates select name/type/complete fields/complete sharings without sending omitted fields. Supplied arrays replace the entire respective state, including explicit empty arrays; per-entry patches are not offered. The manufacturer explicitly requires complete arrays but does not spell out every omitted scalar combination; omission acceptance remains server-validated. The client does not fetch-and-merge hidden data or promote rights. Current request field/type/sharing guards remain separate from future response preservation.

The list description says templates created by the requesting user while documenting access for owner/coowner/editor/viewer. Preserve the server-returned list and visibility without applying an additional owner filter or inventing tenant completeness, paging or totals. Canonical repeated GUID identities or malformed successful metadata fail clearly with original inner causes. Original feature/permission/transport diagnostics remain errors rather than fake empty lists or unsupported feature conclusions.

Selections snapshot sequences once before authorization and keep exact order/text. Native cancellation and existing token/admission/transport defaults remain intact. Safe GET retries remain bounded; uncertain create/update/remove writes are not replayed. Creating templates is non-idempotent.

Isolated tests cover all five routes, nested JSON defaults/sharings, hidden-versus-empty metadata, original ownership and future names, dates/identity/schema failures, snapshots, original errors, UTF-8, cancellation, public-factory loopback HTTP, GET retry and write no-replay. Real tenant availability, role grants, update omission acceptance, default application and cleanup require separate coordinated exact-owned server verification. Current post-merge matrix, explicitly approved publication and exact downstream default adoption remain separate.
