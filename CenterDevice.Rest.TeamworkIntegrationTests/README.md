# Document version integration through Scopevisio Teamwork

This opt-in project verifies the CenterDevice document version commands through the Scopevisio Teamwork bridge. It references `CompuMaster.Scopevisio.Teamwork` 2026.10.8 and `CompuMaster.Scopevisio.OpenApi` 2026.10.7 together with the local CenterDevice project. It does not verify direct CenterDevice OAuth or imply that every API 2.29 resource is forwarded by the bridge.

The project deliberately remains outside the solution and default workflows. Both NUnit cases are explicit, nonparallel remote tests. A normal or explicitly filtered invocation without the environment opt-in skips before authorization. Build both frameworks first; execute `net8.0` and `net48` sequentially in one exclusive window.

Before running, coordinate the shared account with every relevant local session and all ecosystem repositories. Prevent new server workflows from starting for the entire preparation, execution and cleanup phase; a one-time check or identical concurrency names in different repositories is insufficient. Restore any temporarily disabled workflows to their recorded previous states after cleanup. The fixture's expiry guard records the operator's externally established lease; it does not establish an exclusive lock itself.

Set `CENTERDEVICE_TEAMWORK_LIVE=1` and `CENTERDEVICE_TEAMWORK_EXCLUSIVE_UNTIL_UTC` to the actual agreed UTC expiry only inside the test process. At least fifteen minutes must remain before writes, and five minutes before cleanup. Set credentials through `TEST_USERNAME`, `TEST_CUSTOMERNO`, and `TEST_PASSWORD`. On WKS08, existing `Scopevisio.Teamwork.Test` temporary credential caches can also be read directly; the fixture never writes or prints credentials.

```powershell
dotnet build CenterDevice.Rest.TeamworkIntegrationTests/CenterDevice.Rest.TeamworkIntegrationTests.csproj
dotnet test CenterDevice.Rest.TeamworkIntegrationTests/CenterDevice.Rest.TeamworkIntegrationTests.csproj -f net8.0 --no-build --filter "FullyQualifiedName~DocumentVersionBridgeTests"
dotnet test CenterDevice.Rest.TeamworkIntegrationTests/CenterDevice.Rest.TeamworkIntegrationTests.csproj -f net48 --no-build --filter "FullyQualifiedName~DocumentVersionBridgeTests"
```

Each case creates one unshared root collection named `ZZZ_UnitTests_CenterDevice_API229_Versions_<GUID>` and one uniquely named small text document. Tests check original and reverted payloads, preserved document identity/history, repeated revert creating another version, removal including the current version, selection of the largest remaining version, unavailable deleted versions, non-reused version numbers, repeated removal preserving state, rejection of deleting every remaining version, and pre-canceled commands leaving server state unchanged. Each mode exercises the corresponding synchronous or asynchronous new commands.

The temporary `CenterDevice.API229.Versions.owned-resources.json` ledger records only exact test resource identities and ownership. It records collection and upload intent before mutations and recovers only that scope after an interrupted run. Setup verifies absence; cleanup checks the actual CenterDevice owner separately from the Scopevisio authentication-context ID, rejects sharing or unexpected collection contents, removes the collection with its owned document, erases only recorded document IDs from the current user's trash, and verifies active and trash absence before removing the ledger. Keep a remaining ledger for recovery; never clear the server root or the entire trash. An independent bounded cleanup token runs in `finally` even after test failure. Failure and cleanup diagnostics are reported separately without emitting provider response bodies or authentication headers.

The extra trash client is test-only and follows API 2.29 sections 5.19.1 and 5.19.4 solely for complete owned-resource cleanup; it adds no public production SDK operations.
