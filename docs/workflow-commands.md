# Direct workflow commands

API 2.29 sections 5.20.2–5 are available through the additive `Workflows` factory and synchronous/native asynchronous methods on `WorkflowsRestClient`. These operations use explicit workflow identity and retain existing transport/authentication/default behavior.

| Operation | Route and body | Success |
|---|---|---|
| AssignToWorkflow | POST workflow/id, action assign | 204 |
| UnassignFromWorkflow | POST workflow/id, action unassign | 204 |
| RespondToWorkflow | POST workflow/id, action respond with params | 204 |
| RemoveWorkflow | DELETE workflow/id, no entity | 204 |

Assignment is available to visible workflow participants, requires metadata-form type and started state, and requires no other assignee. Unassignment is available to visible participants without inventing additional type/state restrictions. The manufacturer defines assign/unassign as idempotent; the existing transport conservatively does not retry POST writes.

A response selects `confirmed` or `rejected` and an explicit positive Int64 document version. It uses `params`, while workflow-template use uses `data`. Null comment omits it; empty text and original Unicode/newlines remain explicit. Rejection is available only for approval workflows; the client does not fetch or guess existing type to authorize rejection. The server enforces participation, visibility, incomplete state and metadata-form required document fields. A response adds timeline events, may complete the workflow, execute finalizing actions and remove workflow-created sharings. Uncertain response outcomes are not replayed.

Only a workflow's creator may remove it, and it must remain visible to that creator; losing document visibility can prevent deletion. Completed and incomplete workflows can be removed. The SDK does not infer current creator/state, erase documents or claim that removing a workflow reverses all previous action effects. The server confirms deletion and timeline behavior; uncertain deletion is not replayed.

Selections reject null/blank/dot path identities before authorization, escape the exact selected identity, and retain original server/transport causes. Native cancellation reaches authorization, admission and HTTP I/O. Completion is reported only for expected 204 status; unrelated successful statuses and permission/state failures remain errors. Existing interfaces and path/file-system APIs are unchanged.

Isolated fixtures cover sync/native routes, original comments, params shape, explicit versions, input guards, visibility/participation/state failures, original inner causes, cancellation and eight real public-factory loopback HTTP probes including 503 no-replay. Direct workflow creation, role-dependent detail/list/filter/attachment models and real coordinated state/role/notification/finalizing acceptance remain separate work under #21. Full current post-merge matrix, explicitly approved publication and downstream adoption are not established by isolated tests.
