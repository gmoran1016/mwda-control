# Custom Wallpaper and Version Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable Microsoft-compatible custom wallpaper uploads for the connected WDA2/Gen-1.5 adapter, correct legacy wallpaper capability and preset routing, and show the running application version in the About view.

**Architecture:** Keep image preparation local and isolated in a `WallpaperImagePreparer` component. The advanced client validates and prepares the user image, then delegates the exact two-part legacy multipart request to `ProtocolRequestCatalog`. Legacy and modern wallpaper protocols remain selected from the existing wallpaper read path. The About view binds to an assembly-sourced version helper so `Directory.Build.props` remains the only version source.

**Tech Stack:** C#/.NET 8, WPF image APIs (`BitmapImage`, `RenderTargetBitmap`, `PngBitmapEncoder`), `HttpClient`, xUnit, existing adapter transport and MVVM layers.

## Global Constraints

- Follow test-driven development: add or revise a failing test, run it and observe the expected failure, then implement the smallest change and rerun the focused tests.
- Execute inline in the current checkout as requested; do not create a subagent or separate worktree.
- Preserve the no-firmware-update scope. Do not add firmware discovery, download, or update controls.
- Keep modern-generation wallpaper behavior unchanged unless a test proves the shared code must be adjusted for the new legacy path.
- Never stage or modify the pre-existing `.superpowers/` directory.
- Keep upload inputs bounded and allow-listed. Do not send the original user image to the adapter.
- Preserve the existing write serialization and read-back verification behavior.
- Do not run opt-in live tests automatically in CI. Live tests must use deterministic test images and restore a known built-in wallpaper in `finally`.
- The design commit is version `1.0.11`; the implementation commit must bump `Directory.Build.props` to `1.0.12` to honor the repository rule that every GitHub commit changes the version.

## File Map

- `src/Mwda.Control/Protocol/ProtocolRequestCatalog.cs` — legacy preset route and exact two-part multipart request construction.
- `src/Mwda.Control/Protocol/WallpaperImagePreparer.cs` — new local decode, crop, resize, tint, blur, and PNG encoding component.
- `src/Mwda.Control/Protocol/AdvancedAdapterClient.cs` — preparation orchestration, legacy capability, upload error-code mapping, and upload read-back.
- `src/Mwda.Control/Protocol/AdapterModels.cs` — prepared wallpaper image value object if needed by the production boundary.
- `src/Mwda.Control/Versioning/ApplicationVersion.cs` — new assembly-sourced version formatter.
- `src/Mwda.Control/ViewModels/AboutViewModel.cs` — expose the normalized application version.
- `src/Mwda.Control/Views/AboutView.xaml` — small version label bound to the view model.
- `src/Mwda.Control/Views/DisplayView.xaml` and `src/Mwda.Control/ViewModels/DisplaySettingsViewModel.cs` — capability and firmware-requirement wording for custom wallpaper.
- `Directory.Build.props` — bump implementation version from `1.0.11` to `1.0.12` in the final implementation commit.
- `tests/Mwda.Control.Tests/Protocol/ProtocolRequestCatalogTests.cs` — exact legacy route and multipart contract tests.
- `tests/Mwda.Control.Tests/Protocol/WallpaperImagePreparerTests.cs` — new image transformation tests.
- `tests/Mwda.Control.Tests/Protocol/AdvancedAdapterClientTests.cs` — capability, upload orchestration, error-code mapping, and legacy preset tests.
- `tests/Mwda.Control.Tests/Versioning/ApplicationVersionTests.cs` — version normalization tests.
- `tests/Mwda.Control.Tests/ViewModels/ViewModelTests.cs` — About view model version binding test.
- `tests/Mwda.Control.IntegrationTests/OptionalCapabilitiesLiveTests.cs` or a focused companion file — opt-in upload/read-back/restore smoke test.

## Tasks

### 1. Establish failing protocol and capability tests

- [x] Update the legacy wallpaper capability test so a valid `GetWallpaperID` response for the Four Square/WDA2 route expects `SupportsCustomWallpaper == true` and retains the legacy protocol variant.
- [x] Replace the legacy preset test’s `POST SetDisplayWallpaper` expectation with `GET SetPredefinedWallpaper&WallpaperID=<id>` and exact read-back verification.
- [x] Replace the one-part upload assertion with an exact two-part assertion: `POST ...?Action=UploadWallpaper`, fields `WallpaperBlackTint` and `WallpaperBlur`, filenames `WallpaperBlackTint.png` and `WallpaperBlur.png`, `image/png`, `Content-Encoding: binary`, and deterministic byte payloads.
- [x] Add a client test proving `{"ErrorCode":-8}` from a custom upload becomes an `UnsupportedAdapterOperationException` whose message names firmware `2.0.8442`.
- [x] Add a client test proving successful custom upload verifies the legacy read-back as wallpaper ID `0`.
- [x] Run only the affected protocol tests and confirm they fail for the current implementation for the intended reasons.

### 2. Establish failing image-preparation tests

- [x] Add a valid in-memory source PNG fixture and tests for `WallpaperImagePreparer` that require both output images to be decodable PNGs at exactly `1920x1080`.
- [x] Add a deterministic-output test using the same source bytes twice and requiring identical black-tint and blur bytes.
- [x] Add a crop/resize test with a non-16:9 source image and verify the output dimensions remain exactly `1920x1080`.
- [x] Run the new image-preparation tests and confirm they fail because the production component does not yet exist.

### 3. Establish failing version and UI-facing tests

- [x] Add version formatter tests for informational versions with and without build metadata, an assembly fallback, and an unknown fallback.
- [x] Add an `AboutViewModel` test asserting its exposed version equals the production helper’s normalized value.
- [x] Add or update a view-source test to require a small About-view binding to `ApplicationVersion` and no hard-coded numeric version string in the XAML.
- [x] Run the focused version/view-model tests and confirm they fail before production implementation.

### 4. Implement image preparation and exact legacy protocol

- [x] Implement `WallpaperImagePreparer` using WPF `BitmapImage`/`RenderTargetBitmap`, with bounded source bytes, center-crop preserving aspect ratio, 1920×1080 output, subtle black tint, deterministic lightweight blur, and PNG encoding.
- [x] Preserve the existing JPG/PNG extension/content-type/signature validation before decoding, and ensure malformed image data produces a user-facing argument/protocol error without an HTTP request.
- [x] Add a request-builder method for two generated PNG byte arrays. Use the exact Microsoft-compatible multipart names, filenames, media types, binary content-encoding headers, action query, and POST method.
- [x] Change only the legacy preset branch to the observed GET query route. Keep modern preset JSON behavior unchanged.
- [x] Run the focused protocol and image tests until green.

### 5. Integrate advanced client behavior and UI version/capability messaging

- [x] Inject or instantiate the image preparer at the advanced client boundary, prepare the user stream, send the generated pair, and verify successful custom uploads by reading back wallpaper ID `0` on legacy adapters.
- [x] Parse write-response `ErrorCode` values. Map legacy custom-upload `-8` to `UnsupportedAdapterOperationException` with the firmware `2.0.8442+` requirement; retain classified protocol failures for other nonzero errors and malformed responses.
- [x] Update legacy capability to model-level custom wallpaper support and adjust display copy so supported WDA2 adapters are enabled while firmware-specific rejection is explained by the error.
- [x] Add the assembly-sourced version helper, About view model property, and small About-view label.
- [x] Run all unit tests and fix regressions without weakening protocol assertions.

### 6. Add restoration-safe live smoke coverage and validate the connected adapter

- [x] Add an opt-in live test gated by `MWDA_RUN_LIVE_TESTS=1` and `MWDA_ADAPTER_IP`, using deterministic generated 1920×1080 PNGs rather than a user image.
- [x] Have the live test record current wallpaper state, select a known built-in restoration ID (`1` when the current ID is custom `0`), upload the pair, assert custom ID `0`, and restore/read back the selected preset in `finally`.
- [x] Run the normal automated test suite, then run the live test explicitly against `192.168.137.247` only if the adapter is reachable. Leave the adapter on the restored built-in preset and report any limitation honestly.
- [x] Build the WPF application and inspect the produced assembly version and About binding.

### 7. Review, version, and commit the implementation

- [x] Review the diff against the approved design and scan for hard-coded duplicate version values, firmware-update behavior, accidental `.superpowers/` staging, and unbounded upload paths.
- [x] Run `git diff --check`, the complete automated test suite, and the final build.
- [x] Bump `Directory.Build.props` to `1.0.12`, verify the application version tests/build metadata, and commit the implementation with an intentional message. Do not push until explicitly requested.

## Verification Commands

Use the workspace-provided .NET runtime if the system `dotnet` command cannot satisfy `global.json`:

```powershell
dotnet test --no-restore
dotnet build --no-restore
git diff --check
```

For the explicit live run, set the opt-in variables only for that command/session:

```powershell
$env:MWDA_RUN_LIVE_TESTS = "1"
$env:MWDA_ADAPTER_IP = "192.168.137.247"
dotnet test tests\Mwda.Control.IntegrationTests\Mwda.Control.IntegrationTests.csproj --filter FullyQualifiedName~Wallpaper
```
