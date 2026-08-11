# Legacy Adapter Reporting and Overlay Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct legacy Microsoft Wireless Display Adapter capability detection and prevent the disconnected status surface from showing through the settings page.

**Architecture:** Keep the existing capability-probing design, but support the legacy case-sensitive `GetWallpaperID` route before falling back to the modern `GetWallpaperId` schema. A successful legacy wallpaper probe identifies the WDA2 / Microsoft Four Square-logo adapter, supplies its known built-in wallpaper IDs, and enriches the identity shown by diagnostics without fabricating firmware or MAC data. Make the disconnected view an opaque `Border`-backed overlay so it cannot visually overlap the underlying page.

**Tech Stack:** .NET 8 WPF, C#, xUnit, adapter HTTP protocol, XAML source-level UI regression checks.

## Global Constraints

- Preserve firmware exclusion and do not add firmware-update behavior.
- Preserve the existing core read/write protocol and exact read-back validation.
- Treat HTTP 404/501 optional operations as unsupported, but do not misclassify a successful legacy wallpaper probe.
- Keep unknown firmware version and MAC address as `Unavailable` unless the adapter actually reports them.
- Do not send mutating requests during live verification.
- Preserve the existing .NET SDK pin at `8.0.423` and self-contained Windows publish configuration.

---

### Task 1: Add failing protocol regression tests for the Four Square-logo adapter

**Files:**
- Modify: `tests/Mwda.Control.Tests/Protocol/AdvancedAdapterClientTests.cs`
- Modify: `tests/Mwda.Control.Tests/Session/CapabilityDetectorTests.cs`

**Interfaces:**
- `AdvancedAdapterClient.GetWallpaperInfoAsync` must accept the legacy `GetWallpaperID` response `{"WallpaperID":0}`.
- A modern full wallpaper response remains supported.
- The legacy response must identify the WDA2/Four Square-logo profile and expose built-in IDs `0` through `4` without claiming custom-image support.

- [x] **Step 1: Add the failing legacy-route test**

Add a test whose handler returns HTTP 200 only for `Action=GetWallpaperID`, with `{"WallpaperID":0}`, and assert that `GetWallpaperInfoAsync` returns current ID `0`, IDs `0`–`4`, and `SupportsCustomWallpaper == false`.

- [x] **Step 2: Add the failing capability-profile test**

Add a capability-detector test where `GetWallpaperID` succeeds and the modern `GetWallpaperId` route would be unsupported; assert that the returned identity is generation `Generation2`, has model `Microsoft Wireless Display Adapter (with Microsoft 4 Square logo)`, and enables `GetWallpaperInfo`/`SetWallpaper`.

- [x] **Step 3: Run only the new tests and verify RED**

Run:

```powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Legacy"
```

Expected: the tests fail because the current client asks for `GetWallpaperId`, rejects the minimal response schema, and leaves generation/model unknown.

### Task 2: Implement legacy protocol compatibility and identity enrichment

**Files:**
- Modify: `src/Mwda.Control/Protocol/ProtocolRequestCatalog.cs`
- Modify: `src/Mwda.Control/Protocol/AdapterModels.cs`
- Modify: `src/Mwda.Control/Protocol/AdvancedAdapterClient.cs`
- Modify: `src/Mwda.Control/Session/CapabilityDetector.cs`
- Test: `tests/Mwda.Control.Tests/Protocol/AdvancedAdapterClientTests.cs`
- Test: `tests/Mwda.Control.Tests/Session/CapabilityDetectorTests.cs`

**Interfaces:**
- Add a legacy wallpaper protocol variant to `WallpaperInfo` with an optional default so existing callers remain source-compatible.
- `GetWallpaperInfoAsync` tries `GetWallpaperID` first, falls back to `GetWallpaperId` only when the legacy route is explicitly unsupported, and remembers the successful write route for the session.
- Legacy predefined wallpaper writes use `SetDisplayWallpaper` with the existing `WallpaperID` JSON field; modern writes retain `SetPredefinedWallpaper`.
- `CapabilityDetector` captures the wallpaper probe result and enriches the identity only for the known legacy variant.

- [x] **Step 1: Implement the smallest request-catalog additions**

Add separate read/write request creation for the case-sensitive legacy actions while leaving the existing modern request methods unchanged for non-legacy adapters.

- [x] **Step 2: Implement legacy response parsing and fallback**

Parse a numeric or string `WallpaperID`, return the known legacy IDs `0`, `1`, `2`, `3`, `4`, and set custom-image support to `false`. Catch only `UnsupportedAdapterOperationException` around the legacy read before using the modern route.

- [x] **Step 3: Implement generation/model enrichment**

When the captured wallpaper result is the legacy variant, set `AdapterIdentity.Generation` to `Generation2` and `Model` to `Microsoft Wireless Display Adapter (with Microsoft 4 Square logo)`. Leave firmware and MAC fields null.

- [x] **Step 4: Run the focused protocol/session tests and verify GREEN**

Run:

```powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AdvancedAdapterClientTests|FullyQualifiedName~CapabilityDetectorTests"
```

Expected: all focused tests pass, including the existing modern-schema coverage.

### Task 3: Add failing UI regression tests for the overlay and capability copy

**Files:**
- Modify: `tests/Mwda.Control.Tests/ViewModels/ViewModelTests.cs`

**Interfaces:**
- The main-window disconnected surface must be hosted by an opaque element with `Panel.ZIndex="10"` and a window-background brush.
- Pairing copy must distinguish enabling/disabling PIN protection from changing the PIN value.

- [x] **Step 1: Add the failing XAML overlay assertion**

Read `src/Mwda.Control/MainWindow.xaml` using the existing `ReadSource` helper and assert that the z-indexed disconnected surface is a `Border` with `Background="{DynamicResource WindowBackgroundBrush}"` containing the connection `ContentControl`.

- [x] **Step 2: Update the failing copy assertion**

Change the existing pairing-settings assertion to require the new wording: `The adapter's PIN can be enabled or disabled here. This app does not change the PIN value itself.`

- [x] **Step 3: Run the focused UI tests and verify RED**

Run:

```powershell
dotnet test .\tests\Mwda.Control.Tests\Mwda.Control.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ViewModelTests.PairingSettingsExposeOnlyTheCharacterizedBooleanOperation|FullyQualifiedName~ViewModelTests.DisconnectedSurfaceUsesOpaqueBackground"
```

Expected: the overlay assertion and updated-copy assertion fail against the current XAML/copy.

### Task 4: Implement the opaque overlay and truthful pairing language

**Files:**
- Modify: `src/Mwda.Control/MainWindow.xaml`
- Modify: `src/Mwda.Control/Views/AdapterView.xaml`
- Test: `tests/Mwda.Control.Tests/ViewModels/ViewModelTests.cs`

**Interfaces:**
- The disconnected `ConnectionViewModel` remains the overlay content and retains its existing visibility trigger.
- The overlay paints the full content column with an opaque background before presenting `DisconnectedView`.
- PIN-protection controls remain bound to the existing core capability operations.

- [x] **Step 1: Wrap the disconnected content in an opaque Border**

Move the existing visibility style and `Panel.ZIndex="10"` onto a `Border` with `Background="{DynamicResource WindowBackgroundBrush}"`; put `<ContentControl Content="{Binding Connection}" />` inside it.

- [x] **Step 2: Replace misleading pairing copy**

Replace the always-visible sentence in `AdapterView.xaml` with the approved wording that the adapter PIN can be enabled/disabled here and the app does not change the PIN value.

- [x] **Step 3: Run the focused UI tests and verify GREEN**

Run the Task 3 command again. Expected: all focused UI tests pass and the original binding/source assertions remain green.

### Task 5: Verify against the live adapter and complete the change

**Files:**
- Verify: `src/Mwda.Control/Protocol/AdvancedAdapterClient.cs`
- Verify: `src/Mwda.Control/Session/CapabilityDetector.cs`
- Verify: `src/Mwda.Control/MainWindow.xaml`
- Verify: `src/Mwda.Control/Views/AdapterView.xaml`

**Interfaces:**
- Read-only live checks may use `192.168.137.247`; no write action is allowed.
- The non-live test suite, Release build, and publish output must remain clean.

- [x] **Step 1: Run the complete non-live test suite**

Run `dotnet test .\MWDA.Control.sln --configuration Release --no-restore --filter "Category!=LiveAdapter"` and require zero failures.

- [x] **Step 2: Build and publish Release**

Run `dotnet build .\MWDA.Control.sln --configuration Release --no-restore` and the existing `publish.ps1`; require the one-EXE output check to pass.

- [x] **Step 3: Run read-only live protocol checks**

Confirm the adapter still returns 200 for `GetDeviceName`, `GetOverscanSetting`, `GetPasswordProtectState`, and `GetWallpaperID`, and that the application-side profile reports Generation2/Four Square-logo model and wallpaper support without issuing any setter action.

- [x] **Step 4: Review and commit the focused change**

Run `git diff --check`, confirm `.superpowers/` is not staged, then commit with `fix: support legacy adapter reporting and overlay`.
