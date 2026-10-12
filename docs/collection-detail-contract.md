# Immutable collection details

GetCollectionDetails and its native asynchronous form implement API 2.29 section 5.3.20 alongside unchanged legacy getters and interfaces. The explicitly selected ID is escaped and optional individual fields are copied before authorization. Omission preserves server defaults; owner-only auditing-users and auditing-email-settings require explicit field requests and server permission. No visibility, opt-in, ownership or module permission is inferred.

CollectionDetails preserves nullable flags, hidden-owner placeholders, visible sharing identities and original hidden counts. Missing child-folder metadata remains unknown; explicit false and empty visible lists remain known. Original archive/expiry date strings retain timezone offsets without parsing or arithmetic. Counts and expiry amounts retain arbitrary-width original integer text. Audit categories, email choices and future period catalogs remain immutable and nullable.

Extended metadata preserves strings and string arrays, including empty arrays and duplicate values. Intelligent filter parameters and all unknown fields are retained. Accessors return defensive JSON copies; sharing/audit lists cannot be mutated. Missing or explicit null metadata remains unknown. A selected projection may omit the ID; when supplied, the ID must identify the requested collection (GUID formatting differences are equivalent).

Successful responses with invalid known JSON kinds, duplicate fields, trailing content or mismatched identities fail with the original parsing diagnostic retained as an inner exception. Native cancellation reaches authorization and HTTP I/O. Only HTTP 200 is accepted; errors remain server-authoritative. Existing default getters and published API compatibility are unchanged.

Isolated fixtures and a private TCP loopback exercise structure, snapshots, unknown/false/zero, original dates, hidden identities, errors and cancellation. They do not prove real owner-only disclosure, role visibility or archive metadata; coordinated Teamwork acceptance, publication and downstream adoption remain separate in #19/#15.
