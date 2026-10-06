# Native asynchronous REST I/O

The concrete REST clients provide additive `*Async` methods using RestSharp's native asynchronous HTTP operations. Existing synchronous methods and interfaces remain available. Upload overloads that already accept a cancellation token now forward it to the HTTP operation. `IAsyncOAuthInfoProvider` and `IAsyncRestClientErrorHandler` are optional contracts; existing implementations of the synchronous interfaces need no new members.

## Request policy

REST calls, OAuth calls, connectivity checks, document downloads and previews share one allowance per HTTP origin in this process. The local defaults are **one active request** and **30 admissions per rolling minute**. These are conservative client defaults, not verified CenterDevice or Teamwork quotas. A separate process or repository does not share this in-memory allowance; shared integration servers still require a coordinated exclusive window.

A streaming download keeps its concurrency allowance until its body reaches EOF or its response is disposed. The returned stream owns the HTTP response and must be disposed. The cancellation token used to open it remains effective during subsequent reads. Downloads use response headers followed by incremental stream reads; opening a download does not allocate the whole file.

Read requests (`GET`, `HEAD`) may make at most three total attempts after HTTP 408, 429, 500, 502, 503 or 504, within a 30-second retry budget. `Retry-After` accepts both seconds and HTTP dates; otherwise retries use bounded exponential delay with jitter. A server's `Retry-After` on 429 or 503 also defers other requests sharing the origin allowance. The final HTTP response is retained for diagnostics when the retry budget is exhausted. Transport exceptions propagate rather than causing an automatic replay.

Mutations are not automatically replayed after timeouts or uncertain server errors. Expired-token HTTP 401 responses with the existing explicit rejection texts permit one authorization refresh and resend. An upload stream factory must provide a readable stream positioned at the beginning with an available `Length`, and another fresh stream if this explicit refresh requests one. The stream opened for length inspection is reused for the first upload. The client disposes upload streams on success or failure. Filenames are escaped in version metadata and sizes use 64-bit lengths. Backend size limits and actual large transfers remain unverified.

## Authentication and remaining integration work

Asynchronous authentication implementations can interrupt their own active requests. Legacy synchronous authentication callbacks are serialized on a worker; cancellation can stop waiting, but cannot interrupt an active legacy callback. The synchronous high-level `CenterDevice.IO` facade still needs native composition; the separate Teamwork authorization adapter is being developed against the unpublished dependency sources. This change does not claim that those paths are already fully asynchronous.

`CenterDeviceHttpTransport.CreateHttpClient` allows an external authorization/account client to share this same per-origin policy. Configure its HTTP client before its first request; the caller owns the returned client and inner handler. Existing unrelated transports are not retroactively limited.

Direct CenterDevice authentication is still subject to the limitations of the existing provider. No new support is claimed for previously unsupported server operations.

## Isolated verification

Run only the dedicated isolated test project when shared servers are unavailable:

```powershell
dotnet test CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj --framework net8.0 -c CI_CD -p:GeneratePackageOnBuild=false
dotnet test CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj --framework net48 -c CI_CD -p:GeneratePackageOnBuild=false
```

The current suite passes **24 tests on each framework**, with fake HTTP handlers and clocks. Coverage includes separate-client admission, active request cancellation, unread-response disposal, rate windows, shared Retry-After cooldown, retry budgets, unreplayed writes, native authorization dispatch, a simulated 5-GB download and upload, filename escaping, and stream ownership/error paths. The library builds for `netstandard2.0`, `net6.0` and `net48`. The existing `log4net` package audit warning remains unchanged.

No local remote integration tests were run. The parallelized remote regression in issue #8 remains ignored until its original acceptance criteria are verified with coordinated server access. A green isolated suite does not complete that issue.
