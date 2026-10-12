# Group administration commands

The existing concrete `client.Groups` and `client.Group` factories now expose API 2.29 sections 5.11.1–5.11.8 through synchronous and native asynchronous methods. Existing interfaces, group retrieval/listing, synchronous signatures and defaults remain unchanged.

| Workflow | Method | Protocol |
|---|---|---|
| Create | Groups.CreateGroup[Async] | POST groups, 201 |
| Add selected members | Group.AddUsers[Async] | POST group/id, add-users |
| Remove selected members | Group.RemoveUsers[Async] | POST group/id, remove-users |
| Add private administrators | Group.AddAdmins[Async] | POST group/id, add-admins |
| Remove private administrators | Group.RemoveAdmins[Async] | POST group/id, remove-admins |
| Change existing administrator privileges | Group.UpdateAdminPrivileges[Async] | POST group/id, update-admin-privileges |
| Update selected fields | Group.UpdateGroup[Async] | PUT group/id |
| Remove the selected group | Group.RemoveGroup[Async] | bodyless DELETE group/id |

Creation has a required name, optional integer color, optional public/private type and initial members. Omitted type retains the public server default; private creation supplies at least one management administrator. Public names must be unique within the tenant ignoring case; private names need not be unique. Public creation/changes require tenant administration; private creation requires an internal user or tenant administrator. Private administration commands/name/color changes require group-management privilege, while group administrators can change members. The server checks actual roles and permitted type/state transitions; the SDK does not infer grants from the authentication context.

`GroupUpdateRequest` omits unchanged fields. An explicit `Users` list replaces all membership, and an empty list clears it. Use `AddUsers`/`RemoveUsers` for selected changes. Administrator updates remain separate: `AddAdmins` does not change an existing administrator's privilege; `UpdateAdminPrivileges` requires existing administrators. Removing administrators is non-idempotent, and a private group must retain a management administrator. Administrator status does not itself grant visibility of data shared with the group.

Creation, member changes and update retain exact partial failures. A 204 result confirms success; a partial 200/creation 201 must identify only selected distinct failed users. A partially successful creation still created a group: retain `CreatedGroupId` for cleanup/reconciliation. The SDK confirms it from `id` or the partial-creation `Location`, resolving a relative Location against the actual request URI, retaining the configured base path/API prefix and checking origin, route and consistency. It does not follow the Location or forward credentials. Malformed mutation responses remain failures with their original inner exception; no ambiguous mutation is retried. Removing a group removes its group access grants, not documents, and can remove users' document visibility.

All nested requests and enumerables are snapshotted before authorization; omitted defaults differ from explicit empty values. Cancellation reaches authorization/admission/native HTTP. Existing expired-token refresh remains, and POST/PUT/DELETE failures have no uncertain-outcome replay.

Isolated tests cover all eight operations, sync/async parity, exact partial selections, nested snapshots, member replacement/clear versus omission, distinct administrator wire shapes, privileges/invalid input, creation identity/relative Location handling, original errors and cancellation. Actual factory loopback probes cover UTF-8 creation, partial creation Location, partial PUT, administrator POST and POST/DELETE 503 without replay. A relative-Location regression failed before correction and passed afterward. Real private/public roles, names, membership visibility and group lifecycle require separate coordinated live acceptance; no real users, groups or shared data are mutated for these isolated checks. Package publication/default DMS consumption remain separate.
