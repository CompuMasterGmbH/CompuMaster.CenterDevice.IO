# Upload-email endpoint management

API 2.29 sections 5.22.1–5 are available through the additive `UploadEmails` factory and matching synchronous/native asynchronous methods on `UploadEmailsRestClient`.

| Operation | Route | Success |
|---|---|---|
| CreateUploadEmail | POST upload-emails | 201, confirmed id |
| GetUploadEmail | GET upload-email/id | 200, selected metadata |
| GetUploadEmails | GET upload-emails | 200, own endpoint list |
| UpdateUploadEmail | PUT upload-email/id | 204 |
| RemoveUploadEmail | DELETE upload-email/id | 204 |

Creation takes the requested import address and nonnull options with a display name. Empty names remain original strings. A user may create at most ten endpoints; administrators, internal and external users can create them, and only their creator can retrieve/update/remove them. Destinations must be visible. If destination visibility is subsequently lost, the address remains valid but imports are no longer added to that destination. No owner/role is inferred locally and no paging, total or history contract is invented.

Options snapshot each selected sequence once before authorization. Null omits a field; explicit false and empty arrays remain distinct. Creation omits defaults: storage none, extraction true, and attachment indexing true only for original storage, false otherwise. The client rejects known invalid creation combinations and explicitly conflicting destinations/nonempty filter lists. Extensions, tag strings, case, dots, whitespace and order are not normalized.

Updates omit unchanged fields and cannot change the import address. Empty arrays explicitly remove tag/filter selections. Switching whitelist/blacklist requires clearing the existing opposite list explicitly. One empty and one nonempty array are therefore allowed together, despite the vendor's shorter mutually-exclusive-fields wording; the detailed update contract explicitly describes this switch. Unknown existing state remains server-validated. Partial indexing/extraction selections do not apply creation defaults or guess current storage. No unspecified null-based destination clearing is offered.

Metadata retains server identity, exact name/address, future storage strings and nullable boolean/destination values. Absent/null lists remain null; empty arrays remain immutable empty lists. Malformed successful replies and repeated canonical GUID identities fail with an inner cause. Response content/import addresses are not embedded in new client validation messages. Treat returned addresses as confidential import credentials and avoid logging them. Original server/transport diagnostics remain unchanged.

Native cancellation reaches authorization/admission/HTTP; existing safe GET retry and expired-token behavior remain unchanged. POST/PUT/DELETE uncertain outcomes are not replayed. Deleting an endpoint preserves already imported documents. Management itself does not send mail or import data.

Isolated fixtures use synthetic `.invalid` addresses and loopback HTTP only. They cover all routes, status/identity/schema validation, options/defaults/partial updates, immutable metadata, snapshots, UTF-8, cancellation, original errors, GET retries and mutation no-replay. Real creator roles, endpoint quotas, destination visibility loss and actual mail/attachment import require separately coordinated server acceptance and owned cleanup. No such acceptance, direct CenterDevice login, publication or default downstream package adoption is implied.
