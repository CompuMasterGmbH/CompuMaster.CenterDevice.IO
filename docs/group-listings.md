# Explicit group listings and folder-name search

API 2.29 adds a `group` filter for collection and folder listings (sections 5.3.21 and 5.4.11). `CollectionsRestClient.GetCollectionsSharedWithGroup[Async]` and `FoldersRestClient.GetFoldersSharedWithGroup[Async]` select resources explicitly shared via that group. The server still applies the caller's visibility; these methods neither grant access nor infer group membership or ownership. Existing interfaces and listing methods are unchanged.

An optional `fields` array is copied into the request before authorization begins. Null keeps server default fields; request `has-folders` or `has-subfolders` to obtain optional existence metadata without fetching children. False means known absence and null means unknown. Folders visible only via public collections are excluded by the default server policy of the group-folder method. Hidden owner/sharing identities remain controlled by the server.

`FoldersRestClient.SearchFoldersByName[Async]` sends a `text` query for partial name matches. The manufacturer permits only `fields` alongside `text`, so this method sends no parent, collection, group, document or public/shared-folder filters. Text and group values remain single URL-encoded query values, including embedded separators and Unicode.

Both folder methods reject `full-path`, which requires a top-most shared-folder query, and `inherited-sharings`, which is unsupported for bulk folder retrieval. Missing filters or invalid comma-containing field names fail before authorization. No-content responses return known empty lists; malformed successful listings are rejected rather than fabricated as empty. Cancellation reaches authorization and HTTP I/O, and original transport causes remain inner exceptions.

Isolated fixtures exercise the actual default RestSharp serializer and URI builder, sync/native async request contracts, field snapshots, child metadata, hidden identities, cancellation, visibility failures and malformed/no-content results. These new query variants have not received live Teamwork or direct CenterDevice verification. General combined listing filters remain separate work in issue #19.
