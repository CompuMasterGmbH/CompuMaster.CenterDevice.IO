# CenterDevice API 2.29 coverage inventory

Parent: [SDK #15](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/15). Consumer: [DMS #90](https://github.com/CompuMasterGmbH/CompuMaster.Dms/issues/90).

This is the initial, reproducible source inventory for the next development cycle. It does **not** certify complete request/response parity, permission enforcement or live availability. Permission/feature confirmation and model drift remain explicit review tasks. Do not mark the parent coverage acceptance complete based on this extraction alone.

## Pinned evidence

- SDK: `CompuMaster.CenterDevice.Rest` 2026.10.09.0, integration branch `master`, commit `0b89c9ae06735b228e01c4816842772e2b475cb0`. This was still the integration tip on 2026-10-11. Its publication and post-merge workflows completed successfully. These historical results do not verify new APIs.
- Manufacturer: [REST API 2.29 reference](https://public.centerdevice.de/02bf3cfd-06c6-4d43-9cd4-3c18aab0020a), `centerdevice-rest-api-v2-29.pdf`, 309 pages, dated 2025-09-10. Verified SHA256: `5edf3d5d916e82b60527a9e5c8e5bd1f01a836d3c6a526dcf631414f94fc00d6`. Page numbers below refer to the PDF's numbered pages. The public link could not be fetched by the web reader in this audit; the explicitly supplied local copy was hash-verified. The [integration overview](https://www.centerdevice.de/integration-api/) was independently accessible.
- Existing direct-authentication PR [#2](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/pull/2) is open at `1049c1b0ce0d44ef52b0bd736a445dc973e48b88`. It is an independent older prototype and is not included in this baseline or silently imported. Keep [#1](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/1) open until the sample and registered-client authentication acceptance are actually verified.
- Manufacturer Rust reference: [centerdevice-rs](https://github.com/CenterDevice/centerdevice-rs/tree/3d2216646f7f55a264204f850db68a03256c70c4), MIT, commit `3d2216646f7f55a264204f850db68a03256c70c4`, crate `centerdevice` 0.5.1. Archive status, last push (2020-04-09), package version and README's incomplete/unsupported alpha status were rechecked. Its OAuth CodeProvider separates browser/code acquisition; search uses nested named-search params; multipart upload constructs metadata/command and document parts. These are design references, not current contract authority. No Rust code is copied here. Preserve its license/attribution if future implementations copy code. Blocking transport and old assumptions must not be imported automatically.

The earlier negative public Python/.NET SDK search is background research, not a fresh exhaustive registry audit and not evidence that private partner SDKs do not exist.

## Reading the matrix

[coverage.csv](coverage.csv) contains **169 resource-reference sections: 167 HTTP operation sections and two event-type inventories** (timeline and audit log). The resource-to-URI overview is excluded. Baseline source inspection identifies entry points for 59 operation sections; 108 have no dedicated entry point found. One existing timeline event inventory is mapped separately. An entry point can still support only a fraction of the contract; these counts are not a completeness percentage.

Each row records section/page, operation, request route, logical command action, extracted request JSON keys, role mentions, ownership/sharing restriction keywords, paging/availability hints, baseline evidence and verification state. Multiple commands on one POST path are separate rows. All HTTP operation rows have a route; event-type rows are not counted as routes or HTTP operations.

Role mentions are **not grants**: a role can occur in an exclusion or field-specific restriction. Keyword extraction does not resolve referenced schemas, defaults, nested constraints, inherited upload metadata or response fields. Confirm the cited section before designing or enabling an operation. New-version upload inherits the new-upload document metadata contract while excluding upload actions (5.2.2). The Request keys cell cannot replace that cross-reference. Requests with paging must retain documented server caps and cursor semantics.

Baseline evidence names concrete methods/types, including methods outside legacy interfaces and their generic model support. Protected `Execute`/`ExecuteAsync`, constants and high-level download/reupload copy composition do not by themselves provide a missing public REST command. Generic metadata retrieval can deserialize caller types; that does not establish coverage by the SDK's built-in models. Absence is a source-inventory finding, not a statement that consumers cannot implement custom subclasses.

The extractor is [inventory.py](inventory.py). Supply a page-marked UTF-8 text extraction beside the hash-pinned PDF:

```text
python docs/api-2.29/inventory.py --repo <sdk-clone> --reference-text <path>/centerdevice-rest-api-v2-29.txt
```

It validates the PDF hash, verifies mapped baseline symbols, regenerates UTF-8 BOM/CRLF CSV, and reports unresolved routes. The supplied extraction uses `=== PDF PAGE N ===` markers. Do not commit manufacturer documents, local credentials or entire research/test directories. Regenerate from the pinned baseline when interpreting baseline counts.

## Verified differences and next steps

| Priority | Scope | Owning issue | Source findings and next verification |
|---|---|---|---|
| 1 | OAuth foundation | [#16](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/16), linked to #1/#2 | Existing refresh/swap/destroy flows are present. Authorization-code exchange and tenant/state URL creation are absent from the baseline. Explicit refresh force/prolong options are absent. Registered client and exact redirect registration are required for live direct authorization. |
| 2 | Versions and representations | [#17](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/17) | Current/specific-version downloads, offset ranges, preview images and version uploads exist. Revert, delete-versions, PDF/MP4/fulltext, ZIP and broader preview options lack dedicated entry points. Preview currently fixes pages=1, wait-for-generation=10 and include-error-info=false. |
| 2 | Document commands/search/model drift | [#18](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/18) | Upload actions are limited to collection/folder placement; built-in metadata omits several manufacturer fields. Search filter models are limited and named-search Params is string although contract params are objects. Dedicated direct/bulk document sharing, tags, extended metadata and ownership operations are absent. Existing locks cover create-new-version only. |
| 2 | Server copy/move | Existing [#6](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/6) | MoveDocuments and IO copy composition exist. Manufacturer single/bulk copy commands (5.2.38-39) need explicit REST coverage and selected-ID/non-inherited-metadata tests. Reuse #6; do not equate IO download/reupload with server command support. |
| 2 | Collections/folders | [#19](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/19) | Core CRUD/membership/share/erase/archive/parent-move methods exist with limited inputs. Collection/folder shared-group filters introduced in 2.29 are absent. Review private has-subfolders binding and metadata/unknown-zero behavior with isolated fixtures before claiming a runtime defect. Remaining archive/auditing/ownership/metadata/download commands are separate work. |
| 3 | Administration/statistics/audit/trash | [#20](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/20) | Group/user/tenant reads and some tenant settings/features exist. Dedicated statistics, audit-log and trash clients are absent; timeline is a different resource. Validate role/tenant/own-trash restrictions separately. |
| 4 | Rules/workflows/templates | [#21](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/21) | No dedicated clients found. Model nested actions, ownership, participant sharing, 2.29 attachments and tenant features in bounded independent PRs. |
| 5 | Upload-email/SFTP, search history, reminders, metadata templates | [#22](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/22) | No dedicated clients found. Keep each API group separate, including secrets returned by import endpoint management. |

Range support must not be reported as entirely missing. Link and upload-link clients also exist; their parameter/model parity remains in the matrix rather than being declared complete. Keep native transport/concurrency and live large-transfer acceptance in existing [#8](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/8).

API 2.29's changelog (page 299) also adds a tenant 2FA restriction setting, workflow attachments and an access-control restriction error for links. These require typed settings/model/error review in the owning batches; an unchanged route count would miss them.

## Verification and integration boundary

No direct CenterDevice server tests, Teamwork bridge tests or new remote CI runs were performed for this inventory. No fresh manufacturer client registration or coordinated exclusive server window is established. Existing GitHub concurrency serializes this repository's matrix but is repository-scoped; it cannot exclude local sessions or the other SDK repository. Do not trigger the remote matrix until access is coordinated through setup and cleanup.

Preserve existing synchronous/async public signatures and defaults. Add contracts without expanding existing interfaces in ways that break third-party implementers. Keep streaming disposal/cancellation and identity/ownership/sharing observable in isolated tests, and retain original server diagnostics. Never automatically replay ambiguous writes.

Integration order remains SDK implementation and isolated checks, review/merge, coordinated post-merge testing, explicit release approval and independent SDK publication, then exact documented package versions and immutable commit pins in Teamwork/DMS through DMS #90. No new version is selected or consumed by this inventory. DMS 2026.10.10.0 and the shared DMS checkout are unchanged.
