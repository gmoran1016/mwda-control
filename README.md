# MWDA Control

MWDA Control is a standalone Windows utility for configuring a Microsoft Wireless Display Adapter over its local control endpoint. It is an offline-first replacement for the Microsoft Store app's adapter-settings workflow.

The utility configures the adapter; Windows performs the Miracast projection. Use the **Open Windows wireless display settings** action in the app when projection needs to be started or reconnected. The app does not implement the projection path itself.

## Prerequisites

- Windows 10 or Windows 11 x64 with Wi-Fi Direct support.
- The repository's pinned .NET 8.0.423 SDK for building and testing. When the repository-local `.tools\dotnet\dotnet.exe` is present, it is preferred. Otherwise, install a compatible .NET SDK and make a real `dotnet.exe` application available on `PATH`; PowerShell aliases are not used for SDK selection.
- A Microsoft Wireless Display Adapter connected through the normal Windows wireless-display flow for live use.
- No Microsoft account, Microsoft Store package, administrator rights, or internet connection is required for normal adapter configuration.

The published executable is self-contained and does not require a separate .NET runtime on the target Windows machine.

From a fresh PowerShell session at the repository root, select and validate the SDK path once before running the build or test commands below:

```powershell
$mwdaDotnetPath = $null
$mwdaLocalDotnetPath = Join-Path (Get-Location) '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $mwdaLocalDotnetPath -PathType Leaf) {
    $mwdaDotnetPath = (Resolve-Path -LiteralPath $mwdaLocalDotnetPath).Path
} else {
    $mwdaDotnetCommand = Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $mwdaDotnetCommand) {
        throw "No usable .NET SDK was found. Provide $mwdaLocalDotnetPath or install a .NET 8 SDK and make dotnet.exe available on PATH."
    }

    $mwdaDotnetPath = $mwdaDotnetCommand.Path
}

$mwdaSdkVersion = (& $mwdaDotnetPath --version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($mwdaSdkVersion)) {
    throw "The selected dotnet executable is not a usable .NET SDK: $mwdaDotnetPath."
}

Write-Output "Using dotnet SDK $mwdaSdkVersion from $mwdaDotnetPath"
```

## Build and run

From the repository root:

```powershell
& $mwdaDotnetPath restore .\MWDA.Control.sln
& $mwdaDotnetPath build .\MWDA.Control.sln --configuration Debug
& $mwdaDotnetPath build .\MWDA.Control.sln --configuration Release
& $mwdaDotnetPath run --project .\src\Mwda.Control\Mwda.Control.csproj --configuration Debug
```

The app starts disconnected, discovers adapters through the local network path, and supports an explicit **Refresh** when the adapter is disconnected or reconnected. Settings remain capability-driven: controls that the selected adapter does not report are unavailable rather than sending an unverified request.

## Tests

The default test command excludes hardware-mutating tests and is safe to run without an adapter:

```powershell
& $mwdaDotnetPath test .\MWDA.Control.sln --configuration Release --filter "Category!=LiveAdapter"
```

Live adapter tests are opt-in and require both environment variables. Set the adapter address discovered through the normal discovery path; do not replace discovery with a production hard-coded address:

```powershell
$env:MWDA_RUN_LIVE_TESTS = "1"
$env:MWDA_ADAPTER_IP = "<adapter-ip>"
& $mwdaDotnetPath test .\tests\Mwda.Control.IntegrationTests\Mwda.Control.IntegrationTests.csproj --configuration Release --filter "Category=LiveAdapter"
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
`publish.ps1` independently prefers `.tools\dotnet\dotnet.exe`, otherwise requires a usable `dotnet.exe` application on `PATH`, and reports a clear error before publishing if neither SDK path works. It does not depend on a caller-created PowerShell alias.

## Supported operations

The app probes the selected adapter and exposes only the capabilities it reports. The non-firmware operation set includes:

- Adapter discovery, identity/device-name read and save, and read-only adapter details.
- Pairing/password-protection state read and save.
- Overscan read and save, including automatic adjustment.
- Optional built-in or custom wallpaper controls when wallpaper support is reported.
- Optional adapter Wi-Fi connect/save and forget operations when Wi-Fi support is reported.
- Optional language controls when language support is reported.
- HDCP is a protocol-level optional capability, but it is not exposed by this UI in this release.
- Connection state, local redacted diagnostics, and a link to Windows wireless-display settings for projection.

The adapter protocol and UI are offline-first. Network traffic is limited to the adapter's local Wi-Fi Direct or infrastructure endpoint; the app does not send telemetry or depend on Store services or internet access.

## Firmware boundary

Firmware update functionality is intentionally absent. MWDA Control has no firmware update page, update check, download, upload, flashing, signing, reboot-for-update, or automatic-update control. Firmware version information, when available, is read-only adapter information only.
