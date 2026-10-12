# Authenticated-user statistics

`CenterDeviceClientBase.Statistics` supplies API2.29 section 5.17.1 through `StatisticsRestClient.GetStatistics[Async]`. Every user can retrieve their own statistics; the authentication-context ID does not select another statistics owner.

```csharp
var stats = await client.Statistics.GetStatisticsAsync(contextId,
    new[] { StatisticsRestClient.DocumentsBytesUsed }, cancellationToken);
long? bytes = stats.DocumentsBytesUsed;
```

The explicit optional selection is enumerated once and snapshotted before authorization. Null omits the `includes` query parameter rather than silently choosing a statistic; the documented response is bodyless HTTP204, and `DocumentsBytesUsed` remains null. A known zero stays distinct. Explicit empty or unsupported selections are rejected before authorization. Duplicate supported names remain explicit in the comma-separated query.

`documents-bytes-used` counts all document versions created by the caller, including versions produced by uploads, reverts and renames. It is not a live-document-only storage total or an inferred tenant quota. The supplied count is preserved as a nonnegative 64-bit integer; missing, textual, fractional, negative, overflowing or malformed successful responses are rejected with the original parsing exception retained.

The native async call propagates cancellation through authorization, admission and HTTP I/O; the sync method uses that same implementation. Existing token refresh and bounded GET-read retry behavior are retained. There are no new public interfaces, defaults for other clients, caller ownership guesses or server writes.

Isolated tests cover the request snapshot, omitted versus zero, large counts, original errors, cancellation and compatibility. Actual private-loopback tests exercise the public factory, bodyless GET with or without includes, HTTP200/204 metadata and the existing three-attempt transient GET policy. Real account/role enforcement, direct manufacturer authorization, package publication and default DMS adoption remain separate checks.
