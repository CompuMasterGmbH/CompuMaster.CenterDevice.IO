# User listing details

`UsersRestClient.GetUsersDetails` and `GetUsersDetailsAsync` implement manufacturer API 2.29 section 5.8.3 with additive immutable metadata and optional `status`, `include-deleted` and deprecated `all` queries. Existing `GetAllUsers` methods, interfaces and mutable models remain unchanged.

Omitted query parameters preserve the server defaults. Explicit false remains distinct from omission. Prefer a status selection to `all`, which the server ignores when `status` is provided. Supplied status values are snapshotted before authorization and encoded as query values. Future status values, original spelling, ordering and duplicates are preserved; a supplied selection must contain individual nonblank strings without commas.

The OAuth token selects the tenant. Only internal users and administrators can filter by status/all. External users and guests receive every visible user regardless of those filters. The client does not apply another filter to the response or infer privileges from returned roles. Including deleted users requests only profiles still available to the server and does not reconstruct unavailable users.

User-list entries preserve IDs, hyphenated first/last names, email, role, status and nullable technical-user metadata. Missing or JSON-null metadata stays unknown; empty names and explicit false remain intact. Future role/status values are retained. Technical users cannot receive document/collection shares; absence of that indication is not a grant of sharing permission.

A successful response must have a user array containing objects with nonblank string IDs and correctly typed optional fields. Duplicate JSON property names and trailing JSON are rejected with an inner diagnostic. Explicit empty arrays return an empty read-only list; absent arrays and unexpected HTTP statuses do not become empty results. Responses preserve server order, including repeated entries, rather than inventing identity reconciliation.

Isolated tests verify requests, nullable metadata, failures, native cancellation and private TCP loopback behavior. Actual role-dependent visibility, deleted-profile availability and Teamwork/direct CenterDevice acceptance remain separate criteria in #20/#15. No release or downstream default package update is implied.
