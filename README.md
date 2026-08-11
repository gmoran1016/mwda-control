# MWDA Control

MWDA Control is a standalone Windows utility for configuring a Microsoft Wireless Display Adapter (MWDA) when the original Microsoft Wireless Display Adapter app is unreliable on Windows 11.

It talks to the adapter's local control endpoint and provides the adapter-settings workflow without Microsoft Store services. Windows still owns Miracast projection: use Windows' **Connect** experience to start or reconnect the display, then use MWDA Control to inspect or configure the adapter.

Firmware updates are intentionally not supported.

## Download for Windows

Download the latest self-contained executable from the [latest GitHub release](https://github.com/gmoran1016/mwda-control/releases/latest):

1. Download `Mwda.Control.exe`.
2. Save it somewhere convenient, such as `Downloads` or a tools folder.
3. Run it on Windows 10 or Windows 11 x64.

The release is a single `.exe`; no .NET installation, Microsoft Store account, or installer is required. Windows Defender SmartScreen may show an initial warning because the executable is not code-signed. Verify that the file came from this repository's release page, then choose **More info** and **Run anyway** if you trust the download.

## Requirements

- Windows 10 or Windows 11 x64 with Wi-Fi Direct support.
- A Microsoft Wireless Display Adapter connected through Windows' normal wireless-display flow.
- Network access from this computer to the adapter's local control endpoint.

Normal use does not require administrator rights, a Microsoft account, an internet connection, or the Microsoft Store. The app does not send telemetry. Adapter settings traffic stays on the local network path.

## What it can do

MWDA Control probes the selected adapter and enables controls only when the adapter reports that capability. Supported operations include:

- Discover adapters and refresh the connection state.
- Read adapter identity, firmware version, and other diagnostic information.
- Read and change the adapter name.
- Read and change pairing PIN protection (PIN-only mode); the app does not change the PIN value itself.
- Read and change overscan, including automatic adjustment.
- Read and change supported wallpaper settings.
- Read and change supported adapter Wi-Fi settings, including connect and forget operations.
- Read and change the adapter language when supported.
- Copy redacted diagnostics for troubleshooting.
- Open Windows wireless-display settings for projection and reconnection.

The exact controls depend on the adapter generation and the capabilities it exposes. HDCP protocol support is detected where available but is not exposed as a user control in this release.

## Connect an adapter

1. Open Windows' wireless-display/Connect settings and connect to the Microsoft Wireless Display Adapter.
2. Start `Mwda.Control.exe`.
3. Select the discovered adapter and choose **Refresh** if it was connected after the app opened.
4. Make configuration changes from the available pages and save them individually.

If the app reports **Disconnected**, reconnect the adapter through Windows first. The app cannot replace Windows' Miracast projection path. Then use **Refresh** so discovery and the local control endpoint are checked again.

## Firmware boundary

Firmware update functionality is deliberately absent. MWDA Control does not check for, download, upload, flash, or automatically install firmware. A firmware version may be shown as read-only adapter information. Use Microsoft's supported firmware process if the adapter itself requires an update.

## Build from source

The repository targets .NET 8 WPF and pins SDK `8.0.423` in `global.json`. A compatible .NET 8 SDK must be available through the repository-local `.tools\dotnet\dotnet.exe` or as a real `dotnet.exe` on `PATH`.

From the repository root in PowerShell:

```powershell
dotnet restore .\MWDA.Control.sln
dotnet build .\MWDA.Control.sln --configuration Release --no-restore
dotnet run --project .\src\Mwda.Control\Mwda.Control.csproj --configuration Debug --no-restore
```

If the SDK is not on `PATH`, replace `dotnet` in the commands above with the full path to the repository-local SDK executable.

## Tests

The default suite is safe to run without an adapter and excludes hardware-mutating tests:

```powershell
dotnet test .\MWDA.Control.sln --configuration Release --filter "Category!=LiveAdapter"
```

Opt-in live tests require a reachable adapter and both environment variables below. They perform reversible settings writes and restore the original values during cleanup; do not run them while another tool is changing the adapter.

```powershell
$env:MWDA_RUN_LIVE_TESTS = "1"
$env:MWDA_ADAPTER_IP = "<adapter-ip>"
dotnet test .\tests\Mwda.Control.IntegrationTests\Mwda.Control.IntegrationTests.csproj --configuration Release --filter "Category=LiveAdapter"
Remove-Item Env:MWDA_RUN_LIVE_TESTS -ErrorAction SilentlyContinue
Remove-Item Env:MWDA_ADAPTER_IP -ErrorAction SilentlyContinue
```

## Create a local single-file executable

Run the included publish script:

```powershell
.\publish.ps1
```

The output is:

```text
artifacts\publish\win-x64\Mwda.Control.exe
```

The script publishes a self-contained Windows x64 executable with native libraries bundled and debug symbols removed. The output directory is expected to contain only `Mwda.Control.exe`.

## GitHub Actions releases

The workflow in `.github/workflows/release.yml`:

- Runs restore and the non-live test suite on pushes to `master`, pull requests, and manual runs.
- Builds a self-contained single executable on a manual workflow run.
- Builds the same executable and attaches it to a GitHub Release when a `v*` tag is pushed.
- Verifies that the publish directory contains exactly one file: `Mwda.Control.exe`.

To publish a release from a checked-out repository with GitHub CLI:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

The completed release asset can then be downloaded from the repository's Releases page. Manual workflow runs expose the executable as an Actions artifact instead.

## Repository layout

```text
src/Mwda.Control/                         WPF application
tests/Mwda.Control.Tests/                 Unit and UI tests
tests/Mwda.Control.IntegrationTests/      Opt-in live adapter tests
publish.ps1                               Local single-file publish script
.github/workflows/release.yml             CI, artifact, and release workflow
```

## Troubleshooting

**The adapter is not found**

Connect it in Windows' wireless-display settings, confirm the computer is on the same usable network path, and press **Refresh** in MWDA Control. Corporate VPNs, firewall rules, and Wi-Fi isolation can prevent the local control endpoint from being reached.

**The app opens but settings are unavailable**

The adapter may be disconnected, still negotiating its connection, or may not advertise that capability. Reconnect in Windows and refresh. Unsupported controls remain unavailable rather than sending an unverified protocol request.

**A setting does not persist**

Read the diagnostic page and retry after reconnecting the adapter. Different adapter generations expose different protocol behavior; include the copied redacted diagnostics when reporting an issue.
