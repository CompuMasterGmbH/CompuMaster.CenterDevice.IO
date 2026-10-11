# Direct OAuth authorization-code foundation

Implements the protocol foundation tracked in [#16](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/16), part of [#15](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/15). This addition is unpublished until its own merge, post-merge verification and release. It does not complete the sample/application acceptance in #1 or import older PR #2.

`OAuthRestClient` now creates the authorization URI and exchanges a validated callback. The caller owns browser navigation, callback acquisition and one-time state consumption. The library does not start a browser, open a callback listener, store tokens or accept account passwords.

Use an existing `IOAuthClientConfiguration` implementation with manufacturer-registered client ID/secret and the exact registered redirect URI. `BaseAddress` is the HTTPS directory containing `authorize` and `token`, normally `https://auth.centerdevice.de/`; distributor installations use their own auth address. Query, fragment and user information are not accepted in the auth address. HTTP/HTTPS redirect URIs are supported as described by the manufacturer; preserve host, path, trailing slash and registered query parameters.

```csharp
var oauth = new OAuthRestClient(configuration);
// Generate and retain a cryptographically unpredictable state for this attempt.
var authorizationUri = oauth.CreateAuthorizationUri(expectedState, preferredTenantId);
// The application owns this browser/callback operation.
Uri callback = await OpenBrowserAndReceiveCallbackAsync(authorizationUri, cancellationToken);
var response = await oauth.ExchangeAuthorizationCodeAsync(callback, expectedState, cancellationToken);
// Consume state once, validate response.IsSuccessful and response.Data, then retain
// tokens in the application's secure token provider. Do not log tokens or callbacks.
```

The acquisition function and `expectedState` above are application responsibilities, not SDK methods. Keep configuration unchanged for the whole attempt. A state that has been consumed must not be reused, even after a failed or cancelled exchange. The helper requires state although API 2.29 makes it optional; this affects only the new API. The optional tenant is a login preference, not authenticated identity or a permission assertion.

The exchange rejects another redirect identity, missing/mismatched state, fragments, duplicate decoded query keys, missing code and authorization errors before token HTTP I/O. It retains fixed registered query parameters and submits the exact configured redirect string in form data. It uses UTF-8 Basic client credentials and form encoding through the existing token transport and request-admission policy. Token POSTs are not automatically replayed. The synchronous exchange delegates to this asynchronous implementation.

The result is the existing `RestResponse<OAuthInfo>` contract. Server errors remain in the response status/content; the helper does not replace them with a successful token or overwrite diagnostics. Cancellation raises `OperationCanceledException`. Callback rejection raises `AuthorizationException` with a diagnostic that does not include the callback/code. Cancellation or a transport error does not prove the server did not consume the code.

`OAuthInfo.UserId`, `TenantId` and `Email` are not inferred from the callback or tenant preference. Resolve the authenticated identity through the user/tenant API and compose the application's existing `IOAuthInfoProvider`/`IAsyncOAuthInfoProvider`. Explicitly selected users and tenants remain the caller's responsibility.

New `RefreshToken(info, force, prolong)` and `RefreshTokenAsync(info, force, prolong, cancellationToken)` overloads expose API 2.29's lifecycle options:

- `force=true` requests a new access token before expiry.
- `prolong=true` extends an existing access token's expiry; the server can reject it if no existing token remains.
- Both true are rejected before HTTP I/O. False options are omitted.

Existing refresh signatures retain their absent-option request bodies and server defaults. Swap/destroy signatures and all existing interfaces are unchanged. Refresh does not mutate the supplied `OAuthInfo`, change selected identities or cache the returned tokens. Callers decide when to replace tokens and how to synchronize their token provider.

Validation uses fake HTTP handlers only, including code exchange/encoding, callback rejections, exact redirect queries, distributor address paths with/without trailing slash, identity, refresh compatibility, active/pre-cancellation and unreplayed failures. It does not verify actual manufacturer authorization, callback state echo, registration, tenant availability or server permissions. Direct live verification needs registered client/redirect credentials and coordinated exclusive test access; Teamwork bridge access is separate evidence.
