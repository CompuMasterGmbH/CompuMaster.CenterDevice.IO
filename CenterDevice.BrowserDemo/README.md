# CenterDevice embedded-browser session demo

This **experimental Windows sample** addresses [issue #1](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO/issues/1).
It opens the normal CenterDevice web login in an embedded Microsoft Edge WebView2,
observes tokens from the signed-in user's own browser session, and lets the user
check the captured access token with the existing SDK. It does not require an
application-specific API client ID, client secret or registered redirect URI.

This web-session experiment is separate from the documented, registered-client
OAuth authorization-code flow implemented by `OAuthRestClient` in #16. Web-client
internals can change, a web login may expose no reusable API token, and a captured
web token may not be accepted by the public API. Successful browser login alone
does not establish successful SDK authentication.

## Run on Windows

Requirements: Windows, the .NET 8 Desktop Runtime (or .NET 8+ SDK for building),
and the [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).

From the repository root:

```powershell
dotnet run --project CenterDevice.BrowserDemo/CenterDevice.BrowserDemo.csproj
```

Or open `CenterDevice.BrowserDemo/CenterDevice.BrowserDemo.sln` in Visual Studio.
This Windows-only project is deliberately separate from `CenterDevice.sln`, which
is also built on Linux and macOS.

1. Sign in with your own CenterDevice account in the embedded login page. Complete
   any required second factor or tenant selection in that page.
2. Wait for the status to report an access token. If necessary, navigate within
   the web application so it sends an authenticated API request.
3. Select **SDK-Zugriff prüfen (nur lesen)**. The demo calls
   `GET https://api.centerdevice.de/v2/user/current` using
   `CenterDeviceClient.User.GetLoggedInUserDataAsync`.
4. Only after that request confirms a user ID and tenant ID does the demo create
   `CenterDeviceIOClient`. The status shows the confirmed identity. No document
   upload, deletion, token swap or token refresh is performed.
5. Select **Tokenwerte anzeigen** to inspect or manually copy the tokens if needed.
   A blank refresh field means that no matching refresh token was observed.
6. **Neue Anmeldung / Sitzung löschen** clears the demo profile's browsing data and
   its in-memory tokens. It does not revoke tokens on the server. Closing the demo
   ends the private browser session.

## How acquisition works

`LoginForm` listens to WebView2 `WebResourceRequested` and
`WebResourceResponseReceived`. It recognizes:

- `Authorization: Bearer ...` on HTTPS requests to the exact
  `api.centerdevice.de` origin.
- Successful JSON responses at `/token`, `/oauth/token`, `/login` and `/weblogin`
  on exact `auth.centerdevice.de`, `app.centerdevice.de` and
  `api.centerdevice.de` origins.
- Named `access_token` / `accessToken` / `access-token` and corresponding refresh
  fields in the current trusted page's local/session storage and in cookies
  available to those three origins. Cookie pairs must share domain and path;
  nested storage pairs must come from one object. Arbitrary session cookies are
  not treated as API tokens. The marketing site's storage and third-party SSO
  storage are excluded.

The capture is bounded, rejects malformed/ambiguous JSON, and never reads login
form fields, password request bodies, other browser profiles or arbitrary API
response bodies. A newly observed access token invalidates the old refresh-token
association and confirmed identity. Stale storage cannot replace a token already
observed on the network. The SDK identity is obtained from the API, never inferred
from a preferred tenant, an email entered at login or decoded token claims.

The demo uses a unique InPrivate WebView2 profile per process. WebView2 runtime
files may remain under `%LOCALAPPDATA%/CompuMaster/CenterDevice.BrowserDemo/WebView2`;
the app does not write tokens to configuration, logs or export files, and password
autosave/autofill are disabled. Displayed token values are masked initially and
again after a capture change. The sample implements no automatic renewal; sign in
again after expiry. Do not distribute web-session credentials as application keys.

`CapturedTokenProvider` demonstrates the SDK's `IOAuthInfoProvider` bridge with the
captured `OAuthInfo`. The library's public API and DMS dependency versions are
unchanged. This sample does not implement `CenterDeviceDmsProvider.Authorize`.

## Verification

The browser-independent parsing and lifecycle regressions are part of the existing
isolated test project; they do not contact a DMS server or open windows:

```powershell
dotnet test CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj -f net8.0 --filter FullyQualifiedName~BrowserSessionTokenTests
dotnet test CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj -f net48 --filter FullyQualifiedName~BrowserSessionTokenTests
dotnet build CenterDevice.BrowserDemo/CenterDevice.BrowserDemo.csproj
```

Actual account login, token acquisition and public-API acceptance require a manual
end-to-end check with an authorized account. Isolated tests and a visible login
page do not prove those checks. Issue #1 remains open until its working-sample
acceptance is established.

### Live trial observation (2026-10-20)

The normal trial-account login was completed in the embedded browser, which then
displayed the authenticated tenant's web application. The observed browser state
contained two cookies, including a recognized server-session cookie, no cookie
with a token/OAuth name, and no direct requests to `api.centerdevice.de`. The
capture did not find a usable access/refresh token in the inspected sources.
The read-only SDK action therefore remained disabled and no SDK identity call
was made. No documents, collections or account settings were changed.

This result is consistent with a server-managed web session. It does not prove
that every CenterDevice web flow or account exposes the same state. Session
cookies are not interchangeable with OAuth bearer tokens and are never passed
to the SDK as such. Registered-client OAuth acceptance remains separate.

The demo distinguishes reaching a tenant web page from acquiring an API token.
Its diagnostic line shows cookie counts, known session-cookie/token-name flags,
and the number of public-API requests. It never displays cookie names, cookie
values, session IDs or request URLs in that diagnostic line. Counts describe
only the three configured origins and are not a complete network inventory.
