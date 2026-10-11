# Streaming representations, previews and ZIP downloads

API 2.29 sections 5.2.13–16 and 5.2.19 are provided by additive concrete-client methods. Existing interfaces, original-document downloads, legacy preview defaults and optional parameter values are unchanged.

- `Document.DownloadPdf[Async]` and `DownloadMp4[Async]` negotiate their representation through Accept on the selected document URI. They support an optional positive historical version, initial byte offset and server generation wait of 1–30 seconds.
- `Document.DownloadFulltext[Async]` negotiates text/plain. A successful empty body is valid and is different from pending extraction. A plain-text original is returned as its fulltext.
- `Document.DownloadPreviews[Async]` accepts a supported PreviewSize, one-based page/range expression or all, optional version, optional generation wait and optional permanent-error information. It returns a disposable DocumentPreviewDownload containing an unbuffered Content stream and the negotiated ContentType.
- `Documents.DownloadDocumentsAsZip[Async]` uses POST documents/action=download with a snapshot of the selected IDs. DocumentDownloadRepresentation selects Original or Pdf. The optional stream flag defaults to false as in the manufacturer contract; true asks the server to create the archive while streaming.

All new sync and async methods accept cancellation. The caller must dispose the returned stream/download. Cancellation remains active after headers and aborts body reads. Custom stream-wrapper failures also dispose the underlying response. Shared origin admission remains held until the body is consumed or disposed. Diagnostic error-body reads are bounded. A clearly rejected expired token can refresh once, releasing the old response first; uncertain transport failures and generation requests are not automatically replayed.

## Generation and representation contracts

A 307 suggestion or 204 generation-wait expiry throws DocumentRepresentationNotReadyException. It preserves the status and, when provided on a valid same-origin/same-resource Location, the suggested wait of 1–30 seconds. Redirects are not followed and credentials are not forwarded. The caller decides whether and when to make another request.

HTTP 406 throws the existing NotAcceptableException: the server cannot produce the requested format and repeating it is not useful. Visibility, guest-version and tenant-generation restrictions remain server-enforced. PDF conversion can add OCR and need not return the original PDF bytes. MP4 conversion can take longer than the bounded server wait. The SDK does not infer support from a document filename.

A successful response must have the requested MIME type; partial PDF/MP4 responses must have 206 and a matching byte Content-Range. Missing or incorrect MIME/range metadata is rejected with InvalidResponseDataException instead of handing a wrong or empty representation to the caller. This is especially relevant to a bridge that loses the upstream status or MIME metadata.

## Multipart previews

The actual ContentType determines whether the body is PNG/JPEG or raw multipart/form-data. A broader selection such as all can yield one available image; that does not establish all requested page identities. Multipart ContentType includes its validated boundary and each part's Content-Disposition names its page number. Consume or parse Content while the download is alive. Partial availability does not establish that every requested image exists: inspect the actual parts. The SDK preserves the streamed body without synthesizing missing pages or buffering all images. A permanent generation failure with include-error-info can return 404/error 307, preserved by the existing typed error contract.

## ZIP semantics

Only current versions of server-visible documents are included. Hidden selected documents can be omitted; the client does not invent entries or reinterpret ownership. The server renames duplicate filenames. PDF ZIP downloads require all PDF representations already to exist and never trigger conversion. A streamed ZIP has no promised content length; a standard ZIP may take time to assemble. The modern POST operation supersedes deprecated GET routes; no existing deprecated API member is removed or renamed.

## Verification scope

The isolated regression project verifies selected identities, MIME negotiation, generation/default options, positive argument validation, partial range metadata, empty fulltext, multipart boundaries and unaltered parts, ZIP numeric/boolean/ID payloads and snapshot timing, cancellation, response disposal, bounded error reads, one expired-token refresh and published-baseline API compatibility. A loopback HTTP regression verifies that the actual default transport does not follow a generation redirect.

The explicit Teamwork integration project uses a uniquely named owned collection and tiny text/PDF fixtures under a coordinated account/CI lease, with independent cleanup. Live outcomes are recorded individually; a ready PDF or preview, a pending generation result and an unsupported/invalid bridge response are different evidence. A Teamwork test does not verify direct CenterDevice OAuth or all roles/tenant configurations. No production package publication is implied.
