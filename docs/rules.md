# Own rule lifecycle

`client.Rules` exposes API 2.29 sections 5.16.1–5.16.7 through synchronous and native asynchronous methods. Existing interfaces/defaults and the DMS workflow API remain unchanged.

| Workflow | Method | Protocol |
|---|---|---|
| Create own active rule | CreateRule[Async] | POST rules, 201 ID |
| Retrieve own rule | GetRule[Async] | GET rule/id, 200 or absent 204 |
| Retrieve own rules | GetRules[Async] | GET rules, 200 or empty 204 |
| Update selected definition fields | UpdateRule[Async] | PUT rule/id, 204 |
| Activate own rule | ActivateRule[Async] | POST rule/id, action activate, 204 |
| Deactivate own rule | DeactivateRule[Async] | POST rule/id, action deactivate, 204 |
| Remove selected own rule | RemoveRule[Async] | bodyless DELETE rule/id, 204 |

Admin, internal and external users can manage their own rules; guests cannot. Rules are private to the creator but their document actions can affect shared data. New rules default to active. Creation is non-idempotent; retain the new ID for reconciliation/cleanup. The server validates actual scope/target/workflow visibility, configuration and execution cycles. Activation/deactivation are documented idempotent under unchanged objects, but uncertain writes are not automatically replayed.

`RuleCreateRequest` supplies description, scope, matching trigger and nonempty ordered actions; the only creation rule type is user. Scope is collection/folder with a visible ID and optional boolean include-subfolders; omission retains the true server default. Trigger type must correspond to document-added-to-collection/folder. `RuleUpdateRequest` omits unchanged fields and never sends rule type. Supplied action definitions replace the entire list; an empty list is invalid. When only scope or trigger is updated, the server validates it against the existing counterpart.

`RuleComponent` is an immutable typed envelope with a defensive complete JSON data object. This lifecycle slice recognizes all ten documented action names: add/remove tags, set/remove extended metadata, add/move to collection/folder, add-workflow and split-by-marker. Complete advanced payload keys, arrays, strings, decimal text and omitted/default options are retained. Detailed action payload validity remains server enforced; this is not a complete strongly typed builder/validator for all action parameters. In particular, no metadata decimal/date normalization, folder hierarchy/collection/cycle inference, workflow-requester substitution or local reinterpretation of the manufacturer's marker-pattern subset occurs. A successful PDF split can erase the source document; no split or other rule action is executed by the isolated tests.

Read models retain exact identities, description, original status/type/deactivation reason (including future names), complete scope/trigger/actions and exact creation timestamp text. Explicit offsets expose DateTimeOffset; offset-free dates do not acquire the workstation timezone. Actions/pages are read-only and nested data getters return defensive snapshots. GET rule 204 becomes null; GET rules 204 becomes an empty read-only result. Privilege/errors remain errors, without invented creator metadata, paging or completeness contracts.

Requests are snapshotted before authorization. Native cancellation, original causes and existing expired-token refresh remain. Existing bounded GET retries are preserved; uncertain POST/PUT/DELETE outcomes are not replayed. Isolated contracts cover all seven workflows, nested snapshots/raw advanced payload preservation, selected update omission, type/scope/trigger validation, read absence/identity/future metadata, errors/cancellation and public-factory HTTP serialization/retry behavior. No real rules, documents, shared data or notifications are created or altered. Actual own-rule lifecycle, action execution, advanced payload/schema parity, roles/features, workflows/templates, current-head post-merge matrix, publication and default DMS adoption remain separate prerequisites under #21/#15 and DMS #90.
