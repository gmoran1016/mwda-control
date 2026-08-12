# Password-Protection Route Fallback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MWDA Control negotiate modern and legacy password-protection routes so the Four Square-logo adapter exposes its PIN setting correctly.

**Architecture:** Keep `PBCModeStatus` and `GetPBCMode` as the modern protocol. Add explicit legacy request factories for `GetPasswordProtectState` and `SetPasswordProtect`, and let one `AdapterClient` remember which variant succeeds during a session. Convert only HTTP 404/501 into the existing `UnsupportedAdapterOperationException` so fallback is limited to an explicitly absent route; malformed 200 responses and other failures remain errors.

**Tech Stack:** .NET 8 WPF, C# nullable reference types, `System.Text.Json`, `HttpClient`, xUnit, PowerShell publishing, GitHub Actions single-file Windows x64 release.

## Global Constraints

- Preserve the public `PasswordProtectionSettings(bool Enabled)` model and do not transmit a password value.
- Preserve the modern `GetPBCMode`/`SetPBCMode` behavior and exact typed read-back validation.
- Use the observed legacy routes `GetPasswordProtectState` and `SetPasswordProtect` with the `PasswordProtect` field.
- Accept the observed case-insensitive legacy JSON property `passwordProtect` through the existing case-insensitive serializer configuration.
- Fall back only on HTTP 404 or 501; do not retry a malformed successful response or an unrelated HTTP failure.
- Preserve firmware exclusion and all existing wallpaper, discovery, and UI behavior.
- Bump the application version to `1.0.9` in the same GitHub-bound change.
- Do not stage or modify the pre-existing untracked `.superpowers/` directory.

---

### Task 1: Add failing protocol regression tests

**Files:**
- Modify: `tests/Mwda.Control.Tests/Protocol/AdapterClientTests.cs`
- Modify: `tests/Mwda.Control.Tests/Protocol/ProtocolJsonTests.cs`

**Interfaces:**
- Consumes: current `ProtocolRequestCatalog`, `AdapterClient`, and parser behavior.
- Produces: executable tests that require modern-to-legacy negotiation and preserve the captured legacy response.

- [x] **Step 1: Add the failing fallback test**

Add an `AdapterClientTests` handler that returns HTTP 404 for `GetPBCMode` and HTTP 200 with the exact captured body for `GetPasswordProtectState`:

```json
{"passwordProtect":false}
```

Call `GetPasswordProtectionAsync()` and assert `Enabled == false` and the request sequence is exactly modern read followed by legacy read. The current implementation compiles but must fail at runtime because it does not fall back after the modern 404.

- [x] **Step 2: Add the failing legacy write/read-back test**

After the client has negotiated the legacy route, return HTTP 200 for `SetPasswordProtect` and a matching legacy JSON read-back. Call `SetPasswordProtectionAsync(enabled: true, password: null)` and assert the request sequence is legacy set followed by legacy get, with `PasswordProtect=true` in the set query. This test must fail at the modern 404 before the fallback implementation exists.

- [x] **Step 3: Add the no-fallback failure test and lowercase parser fixture**

Add a test where `GetPBCMode` returns HTTP 500 and assert that the client throws without requesting `GetPasswordProtectState`. Add a parser test using the exact lowercase property name `{"passwordProtect":false}` so the captured response remains protected by a regression test.

- [x] **Step 4: Run the focused tests to verify RED**

Run:

```powershell
$sdk = 'C:\Users\Griffin\OneDrive\Documents\ChatGPT\MWDA - App\.worktrees\codex\mwda-control\.tools\dotnet\dotnet.exe'
& $sdk test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~ProtocolRequestCatalogTests|FullyQualifiedName~AdapterClientTests|FullyQualifiedName~ProtocolJsonTests'
```

Expected: the new fallback and legacy write tests fail at runtime on the modern 404; existing modern tests and the lowercase parser fixture may continue to pass.

### Task 2: Implement protocol route negotiation

**Files:**
- Modify: `src/Mwda.Control/Protocol/ProtocolRequestCatalog.cs`
- Modify: `src/Mwda.Control/Protocol/AdapterClient.cs`

**Interfaces:**
- Consumes: the failing tests from Task 1 and existing `UnsupportedAdapterOperationException`.
- Produces: `CreateLegacyPasswordProtectionReadRequest`, `CreateLegacySetPasswordProtectionRequest`, and session-scoped negotiation in `AdapterClient`.

- [x] **Step 1: Add the legacy request factories**

Add these public methods to `ProtocolRequestCatalog`:

```csharp
public static HttpRequestMessage CreateLegacyPasswordProtectionReadRequest(AdapterEndpoint endpoint)
```

which creates a GET for `Action=GetPasswordProtectState`, and:

```csharp
public static HttpRequestMessage CreateLegacySetPasswordProtectionRequest(
    AdapterEndpoint endpoint,
    bool enabled,
    ProtocolWriteEncoding? encoding = null)
```

which creates `Action=SetPasswordProtect` with the `PasswordProtect` boolean field and the recorded query encoding when no encoding is supplied.

- [x] **Step 2: Add the per-client protocol variant state**

Add a private nullable variant field to `AdapterClient` with `Modern` and `Legacy` values. Leave it unset until a password-protection read succeeds so every session starts by probing the modern route.

- [x] **Step 3: Allow explicit unsupported statuses to be caught**

Update `AdapterClient` success handling so HTTP 404 and 501 produce `UnsupportedAdapterOperationException` with the existing redacted failure message. Keep all other non-2xx responses as `AdapterProtocolException` and preserve the existing message text.

- [x] **Step 4: Make password reads negotiate and remember the route**

Have `ReadPasswordProtectionAsync` use the remembered variant when present. When unset, read with `GetPBCMode`; catch only `UnsupportedAdapterOperationException`, retry with `GetPasswordProtectState`, and set the variant only after parsing succeeds. Extend the generic read helper with a request-factory parameter so identity and overscan behavior remain unchanged.

- [x] **Step 5: Make password writes use the negotiated route**

If `SetPasswordProtectionAsync` is called before negotiation, perform the same read negotiation first. Then choose modern or legacy set request creation from the remembered variant, and use the same variant during exact read-back. Keep the password argument rejection and write-response validation unchanged.

- [x] **Step 6: Run the focused tests to verify GREEN**

Add direct route assertions to `ProtocolRequestCatalogTests` for the new legacy read and write factories, then run the Task 1 command again. Expected: all request-catalog, parser, modern-client, fallback, legacy-write, and no-fallback tests pass.

### Task 3: Apply the patch release version

**Files:**
- Modify: `Directory.Build.props`

**Interfaces:**
- Consumes: the approved patch-release policy.
- Produces: assembly/package version `1.0.9` for the GitHub-bound build.

- [x] **Step 1: Set the version prefix**

Add `<VersionPrefix>1.0.9</VersionPrefix>` to the shared property group without changing the target framework or publish settings.

- [x] **Step 2: Build the project and inspect metadata**

Run:

```powershell
$sdk = 'C:\Users\Griffin\OneDrive\Documents\ChatGPT\MWDA - App\.worktrees\codex\mwda-control\.tools\dotnet\dotnet.exe'
& $sdk build .\src\Mwda.Control\Mwda.Control.csproj --configuration Release --no-restore
```

Confirm the build succeeds and the generated assembly metadata reports version `1.0.9`.

### Task 4: Full verification and release preparation

**Files:**
- Verify: `MWDA.Control.sln`
- Verify: `publish.ps1`
- Verify: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: the completed route negotiation and versioned build.
- Produces: evidence suitable for a v1.0.9 GitHub release.

- [x] **Step 1: Run the complete non-live test suite**

Run:

```powershell
$sdk = 'C:\Users\Griffin\OneDrive\Documents\ChatGPT\MWDA - App\.worktrees\codex\mwda-control\.tools\dotnet\dotnet.exe'
& $sdk test .\MWDA.Control.sln --configuration Release --no-restore --filter 'Category!=LiveAdapter'
```

Require zero failures and zero unexpected test errors.

- [x] **Step 2: Run the available live adapter tests**

With the currently connected adapter environment configured, run the live integration filter. Confirm the current adapter continues to use the modern route and restores reversible settings. Do not attempt to mutate the remote adapter; its read-only response has already established the legacy route.

- [x] **Step 3: Publish and verify the single executable**

Run `& .\publish.ps1`, require exactly `artifacts\publish\win-x64\MWDA-Control.exe`, launch that exact file briefly, and record its SHA-256 and file metadata. If the local script selects a broken system SDK, run the same publish arguments with the pinned repository SDK and an isolated `artifacts\publish\manual-v1.0.9` output directory. Confirm no extra files are present in the verified output directory.

- [x] **Step 4: Review the diff and release notes**

Run `git diff --check`, inspect the diff for only the protocol fallback, tests, version, and approved design/plan documents, and confirm `.superpowers/` is not staged. Update README release instructions only if the versioned workflow needs clarification.

- [ ] **Step 5: Create one versioned commit**

Stage only the intended files and create:

```text
fix: support legacy password protection route
```

The commit contains the `1.0.9` version bump so the GitHub-bound commit and release remain aligned.

- [ ] **Step 6: Push and create the v1.0.9 release**

Push `master`, push annotated tag `v1.0.9`, wait for the tag workflow to pass, and verify the GitHub release contains exactly one downloadable `MWDA-Control.exe` asset. Report the release URL and SHA-256.
