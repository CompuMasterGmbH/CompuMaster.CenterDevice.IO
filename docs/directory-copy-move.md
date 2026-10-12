# Directory copies and file moves

`DirectoryInfo.AddCopy(DirectoryInfo)` and its named overload copy the visible source tree into a new child directory. `AddDirectoryCopyAsync` adds native asynchronous execution and cancellation, with an optional `includeSubdirectories` argument (default `true`). Its distinct name preserves overload resolution for existing `AddCopyAsync(FileInfo, ...)` consumers.

A root destination creates a normal collection; a collection or folder destination creates a folder. Immediate documents are copied by server commands without buffering or downloading file contents. Empty folders and filenames are retained. Source document IDs and references are preserved; new documents do not inherit source sharing, tags, comments, history or extended metadata. Destination visibility still follows the destination's existing sharing rules.

The complete visible source tree is planned before writes. Document pages contain at most 500 items and are read until the reported hit count is reached; missing pages, changing totals and repeated identities fail planning. This is not a transactional server snapshot: concurrent source mutations can still change content after planning. The existing general file-listing behavior is unchanged.

Names must be single path segments. Existing destination names, copying the complete root, self/descendant targets and repeated folder identities are rejected. Transfers require the same IO client or the same REST client reference and authenticated context; a matching bridge URL alone does not establish the selected tenant.

Bulk document copies partition repeated source identities into sequential batches with distinct identities per command. The Teamwork bridge coalesces repeated source identities within a single command. Results must contain one distinct new ID per selection, without source IDs or IDs returned by earlier batches. Results retain batch/server order, without an inferred source-to-result mapping.

Directory copies and multi-batch commands are not atomic. Failure or cancellation may leave confirmed earlier resources or uncertain current writes. The SDK does not replay uncertain writes, roll back copies or delete source documents. Callers must reconcile partial destination contents before deciding what to do next.

`FileInfo.Move` delegates to the existing native `MoveAsync` path. Moves retain document IDs and update the local parent only after server confirmation. Both directory file caches are invalidated after success or uncertain failure. Root and different-context destinations are rejected before dispatch.

Isolated tests cover both sync/async paths, destination types, empty and nonrecursive trees, 500/501/1001-document paging, cyclic identities, complete planning before writes, cancellation, partial failures, separate duplicate-source batches, result cardinality and cache invalidation. The published API compatibility fixture and all SDK targets remain applicable. The separate opt-in `CopyAndMoveThroughTeamwork` fixture tests actual bridge behavior with a unique private collection, recorded filenames/folder paths, recovery and independent finally cleanup. It does not run with the default isolated suite; live outcomes must be recorded against the exact tested revision.
