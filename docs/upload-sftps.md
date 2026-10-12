# SFTP upload endpoint management

API 2.29 sections 5.23.1–5 are available through the additive `UploadSftps` factory and synchronous/native asynchronous methods on `UploadSftpsRestClient`.

| Operation | Route | Success |
|---|---|---|
| CreateUploadSftp | POST upload-sftps | 201, confirmed connection |
| GetUploadSftp | GET upload-sftp/id | 200, selected metadata and connection |
| GetUploadSftps | GET upload-sftps | 200, own endpoint list |
| UpdateUploadSftp | PUT upload-sftp/id | 204 |
| RemoveUploadSftp | DELETE upload-sftp/id | 204 |

Creation requires selected name and password and optionally a visible collection or folder and tags. Empty/whitespace password strings are preserved for server policy validation; the SDK does not invent password strength or length rules. At most ten endpoints may be created by a user. Administrators, internal and external users may create them; only their creator can retrieve/update/remove them. If a destination later becomes invisible, the endpoint remains valid but imported documents are no longer added to it. No ownership, permission, paging, total or completeness metadata is inferred.

Creation returns the complete confirmed `UploadSftpConnection`: id, hostname, TCP port 1–65535, username, password and original server-public-key text. The creation reply does not contain name/tags/destination; the SDK does not fabricate them. Retrieval returns immutable `UploadSftp` metadata with its connection; optional absent/null tags differ from immutable empty arrays. Exact names, IDs, credentials, public-key text, tags and order are retained without normalization. Malformed success replies and duplicate canonical GUID identities fail with original inner causes. New validation diagnostics do not embed credentials or response content; default model string conversion does not include credential values.

Options snapshot caller sequences once before authorization. Null omits unchanged update fields; an explicit empty password/name or empty tag array remains explicit. Updates do not substitute the current authenticated user, clear unspecified destinations or infer existing server state. Visibility, password policy, creator restrictions and uncertain outcomes remain server contracts. Removal preserves already imported documents.

Returned username/password are confidential import credentials. Keep them out of logs. The supplied public key supports caller-controlled host-key verification; receiving it over REST does not verify or trust a later SFTP connection. This client manages REST endpoints only: it does not connect to returned hosts, authenticate via SFTP, upload documents, parse key formats or claim successful host-key validation.

Native cancellation and the existing authorization/admission/transport defaults remain unchanged. Safe GET retries stay bounded; uncertain POST/PUT/DELETE writes are not replayed. Original server and transport diagnostics are preserved.

Isolated tests use synthetic credentials and local loopback HTTP, covering all five workflows, complete connection/metadata shapes and port bounds, creator errors/inner causes, exact UTF-8/partial updates/snapshots, cancellation, identity/list validation, GET retries and mutation no-replay. Real endpoint quota/roles/visibility, key acceptance and document import require a separate coordinated exact-owned server scope and cleanup. Full post-merge matrix, explicitly approved publication and exact downstream package adoption remain separate.
