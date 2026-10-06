# Native asynchronous REST I/O

The concrete REST clients provide additive `*Async` methods using RestSharp's native asynchronous HTTP operations. Existing synchronous methods and interfaces remain available. Upload overloads that already accept a cancellation token now forward it to the HTTP operation. `IAsyncOAuthInfoProvider` and `IAsyncRestClientErrorHandler` are optional contracts; existing implementations of the synchronous interfaces need no new members.

## Request policy

REST calls, OAuth calls, connectivity checks, document downloads and previews share one allowance per HTTP origin in this process. The local defaults are **one active request** and **30 admissions per rolling minute**. These are conservative client defaults, not verified CenterDevice or Teamwork quotas. A separate process or repository does not share this in-memory allowance; shared integration servers still require a coordinated exclusive window.

A streaming download keeps its concurrency allowance until its body reaches EOF or its response is disposed. The returned stream owns the HTTP response and must be disposed. The cancellation token used to open it remains effective during subsequent reads. Downloads use response headers followed by incremental stream reads; opening a download does not allocate the whole file.

Read requests (`GET`, `HEAD`) may make at most three total attempts after HTTP 408, 429, 500, 502, 503 or 504, within a 30-second retry budget. `Retry-After` accepts both seconds and HTTP dates; otherwise retries use bounded exponential delay with jitter. A server's `Retry-After` on 429 or 503 also defers other requests sharing the origin allowance. The final HTTP response is retained for diagnostics when the retry budget is exhausted. Transport exceptions propagate rather than causing an automatic replay.

Mutations are not automatically replayed after timeouts or uncertain server errors. Expired-token HTTP 401 responses with the existing explicit rejection texts permit one authorization refresh and resend. An upload stream factory must provide a readable stream positioned at the beginning with an available `Length`, and another fresh stream if this explicit refresh requests one. The stream opened for length inspection is reused for the first upload. The client disposes upload streams on success or failure. Filenames are escaped in version metadata and sizes use 64-bit lengths. Backend size limits and actual large transfers remain unverified.

## Authentication and remaining integration work

Asynchronous authentication implementations can interrupt their own active requests. Legacy synchronous authentication callbacks are serialized on a worker; cancellation can stop waiting, but cannot interrupt an active legacy callback. The high-level `CenterDevice.IO` facade now has native async directory/file listing, path navigation, streaming file download, upload/version/delete/rename/move, link lookup, and disk-staged file copy. Other directory mutations, principal lookups, sharing operations, and DMS consumption still need native composition; the separate Teamwork authorization adapter is being developed against unpublished dependency sources. This change does not claim that those paths are already fully asynchronous.

`CenterDeviceHttpTransport.CreateHttpClient` allows an external authorization/account client to share this same per-origin policy. Configure its HTTP client before its first request; the caller owns the returned client and inner handler. Existing unrelated transports are not retroactively limited.

Direct CenterDevice authentication is still subject to the limitations of the existing provider. No new support is claimed for previously unsupported server operations.

## High-level cache and copy contracts

Async listings serialize calls per directory, reuse a successful cache, and preserve parent/metadata references. Failed or canceled results are not cached. Resetting a cache during an active async listing prevents that older response from republishing the cache. Concurrent direct synchronous operations on the same directory remain outside this guarantee.

`DirectoryInfo.AddCopyAsync` stages a document on temporary disk using bounded asynchronous copying. It fully consumes and disposes the download before beginning upload so that the single shared origin permit does not deadlock the copy. Its temporary file is removed after success, cancellation, or failure; a cleanup failure is attached to the primary exception's `Data["TemporaryFileCleanupFailure"]` rather than replacing it. It needs sufficient local disk space, has no client-side two-gigabyte array limit, and does not establish actual backend transfer limits. Legacy synchronous `AddCopy` retains its previous memory-buffer limitation.

Async mutations invalidate file caches even after uncertain failures so callers can reconcile state before retrying. Renamed names and moved parent references change only after a successful response. Direct-to-path async downloads preserve the existing overwrite contract: cancellation or failure may leave a partial target, and timestamps update only after success. Simulated 5-GB REST streams and smaller real temporary-disk copy fixtures verify different parts of this implementation; no live 5-GB transfer was performed.

## Isolated verification

Run only the dedicated isolated test project when shared servers are unavailable:

```powershell
dotnet test CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj --framework net8.0 -c CI_CD -p:GeneratePackageOnBuild=false
dotnet test CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj --framework net48 -c CI_CD -p:GeneratePackageOnBuild=false
```

The current suite passes **35 tests on each framework**, with fake HTTP handlers and clocks. Coverage includes separate-client admission, active request cancellation, unread-response disposal, rate windows, shared Retry-After cooldown, retry budgets, unreplayed writes, native authorization dispatch, a simulated 5-GB download and upload, filename escaping, and stream ownership/error paths. The library builds for `netstandard2.0`, `net6.0` and `net48`. The existing `log4net` package audit warning remains unchanged.

No local remote integration tests were run. The parallelized remote regression in issue #8 remains ignored until its original acceptance criteria are verified with coordinated server access. A green isolated suite does not complete that issue.
