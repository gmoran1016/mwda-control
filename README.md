# MWDA Control

MWDA Control is a standalone Windows utility for configuring a Microsoft Wireless Display Adapter over its local control endpoint. It is an offline-first replacement for the Microsoft Store app's adapter-settings workflow.

The utility configures the adapter; Windows performs the Miracast projection. Use the **Open Windows wireless display settings** action in the app when projection needs to be started or reconnected. The app does not implement the projection path itself.

## Prerequisites

- Windows 10 or Windows 11 x64 with Wi-Fi Direct support.
- The .NET 8.0.423 SDK for building and testing. Check with `dotnet --version`.
- A Microsoft Wireless Display Adapter connected through the normal Windows wireless-display flow for live use.
- No Microsoft account, Microsoft Store package, administrator rights, or internet connection is required for normal adapter configuration.

The published executable is self-contained and does not require a separate .NET runtime on the target Windows machine.

## Build and run

From the repository root:

```powershell
dotnet restore .\MWDA.Control.sln
dotnet build .\MWDA.Control.sln --configuration Debug
dotnet build .\MWDA.Control.sln --configuration Release
dotnet run --project .\src\Mwda.Control\Mwda.Control.csproj --configuration Debug
```

The app starts disconnected, discovers adapters through the local network path, and supports an explicit **Refresh** when the adapter is disconnected or reconnected. Settings remain capability-driven: controls that the selected adapter does not report are unavailable rather than sending an unverified request.

## Tests

The default test command excludes hardware-mutating tests and is safe to run without an adapter:

```powershell
dotnet test .\MWDA.Control.sln --configuration Release --filter "Category!=LiveAdapter"
```

Live adapter tests are opt-in and require both environment variables. Set the adapter address discovered through the normal discovery path; do not replace discovery with a production hard-coded address:

```powershell
$env:MWDA_RUN_LIVE_TESTS = "1"
$env:MWDA_ADAPTER_IP = "<adapter-ip>"
dotnet test .\tests\Mwda.Control.IntegrationTests\Mwda.Control.IntegrationTests.csproj --configuration Release --filter "Category=LiveAdapter"
Remove-Item Env:MWDA_RUN_LIVE_TESTS -ErrorAction SilentlyContinue
Remove-Item Env:MWDA_ADAPTER_IP -ErrorAction SilentlyContinue
```

Live writes are reversible and restore the adapter's original device name, overscan, and password-protection state in cleanup. Do not enable the live suite while the adapter is disconnected or while another tool is changing its settings.

## Publish

The repository includes a self-contained single-file publish script:

```powershell
.\publish.ps1
Test-Path .\artifacts\publish\win-x64\Mwda.Control.exe
```

The executable is written to `artifacts\publish\win-x64\Mwda.Control.exe`. Run that executable on Windows without installing the .NET runtime separately.

## Supported operations

The app probes the selected adapter and exposes only the capabilities it reports. The non-firmware operation set includes:

- Adapter discovery, identity/device-name read and save, and read-only adapter details.
- Pairing/password-protection state read and save.
- Overscan read and save, including automatic adjustment.
- Optional built-in or custom wallpaper controls when wallpaper support is reported.
- Optional adapter Wi-Fi connect/save and forget operations when Wi-Fi support is reported.
- Optional HDCP and language operations when the adapter reports those capabilities.
- Connection state, local redacted diagnostics, and a link to Windows wireless-display settings for projection.

The adapter protocol and UI are offline-first. Network traffic is limited to the adapter's local Wi-Fi Direct or infrastructure endpoint; the app does not send telemetry or depend on Store services or internet access.

## Firmware boundary

Firmware update functionality is intentionally absent. MWDA Control has no firmware update page, update check, download, upload, flashing, signing, reboot-for-update, or automatic-update control. Firmware version information, when available, is read-only adapter information only.
