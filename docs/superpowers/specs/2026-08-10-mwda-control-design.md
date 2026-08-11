# MWDA Control Design Specification

**Date:** 2026-08-10  
**Status:** Approved design baseline for implementation

## Goal

Build a standalone Windows desktop utility that replaces the Microsoft Wireless Display Adapter Store app for adapter configuration and diagnostics. It must work while the PC is connected to a Microsoft Wireless Display Adapter, support the original app's non-firmware capabilities when the detected adapter exposes them, and contain no firmware download or update workflow.

## Context and evidence

- The workspace was empty when this work began; the replacement owns its project structure.
- The installed Microsoft package is `Microsoft.SurfaceWirelessDisplayAdapter` version `4.232.137.0`.
- The installed package's configuration lists the original non-firmware functions: device naming, language, PIN/password protection, display wallpaper, connection settings, quick connect, overscan, Wi-Fi connection, adapter basics, and HDCP settings.
- The current PC has an active Wi-Fi Direct interface at `192.168.137.1`, with the adapter reachable at `192.168.137.247`.
- The adapter currently responds to these read-only control requests:
  - `GET /cgi-bin/msupload.sh?Action=GetDeviceName` → `{"DeviceName":"WeightRoom-AD"}`
  - `GET /cgi-bin/msupload.sh?Action=GetOverscanSetting` → `{"IsAutoAdjust":false,"OverscanSettingValue":0}`
  - `GET /cgi-bin/msupload.sh?Action=GetPBCMode` → `{"PBCModeStatus":"Disabled"}`; `Disabled` is the adapter's PIN-only state.
- The original Store app launches an `ApplicationFrameHost` window but does not expose a usable process-backed window on this Windows 11 system, matching the reported crash behavior.

## Scope

### Included adapter controls

The app presents only controls supported by the connected adapter's capability profile.

| Original capability | Replacement behavior |
| --- | --- |
| `ChangeDeviceName` | Read and save the adapter display name with validation matching the adapter's allowed character set. |
| `SetPinCode` / `SetPBCMode` | Read and change whether pairing requires a PIN; the app changes PIN-only mode and does not change the PIN value itself. |
| `ChangePassword` | Change the adapter's management password when the adapter reports password protection support. |
| `SetOverscan` | Set the manual overscan value from 0 through 15. Automatic adjustment is shown only for generations that persist that setting; the live Four Square-logo Generation 2 adapter accepts the legacy manual range but does not persist the automatic flag. |
| `SetDisplayWallpaper` / predefined wallpaper | Show supported built-in wallpapers and apply the selected wallpaper. |
| Custom wallpaper | Select a local image, validate its size/type, and upload it only when the adapter reports custom-wallpaper support. |
| `WifiConnection` / `SetConfigureWiFiAP` / `ForgetWiFi` | Scan, select, configure, and forget the adapter's infrastructure Wi-Fi network on generations that expose these operations. |
| `SetConnectionSettings` / quick connect | Show connection mode/status and open Windows' built-in wireless-display discovery surface for projection. |
| `HdcpSetting` | Read and change HDCP only when the adapter exposes that capability. |
| `ChangeLanguage` | Show the adapter/app language selector only when the adapter reports language support. |
| About and adapter basics | Show generation/model, firmware version as read-only information, MAC address when available, current IP, and support/diagnostic details. |
| Recovery/restart | Offer a non-firmware adapter restart only when the detected protocol exposes it; require an explicit confirmation because it interrupts projection. |

### Explicitly excluded

- Firmware version downloads.
- Firmware update checks that trigger a download.
- Firmware upload, flashing, signing, reboot-for-update, or automatic-update settings.
- Telemetry or analytics sent to Microsoft or any other external service.
- Changes to Windows security, firewall, Wi-Fi credentials outside the adapter workflow, or system-wide display settings.

## User experience

The main window is a compact native settings utility with:

1. A connection header showing the selected adapter, current IP, connection state, and a `Refresh` action.
2. A left navigation rail with `Adapter`, `Display`, `Network` (only when supported), `Connection`, `About`, and `Diagnostics`.
3. Each page shows current values loaded from the adapter, an explicit `Save` action for grouped edits, inline validation, and a result banner that says whether the change was applied, rejected by the adapter, or unavailable.
4. A first-run/disconnected state that explains how to connect through Windows wireless-display settings without pretending that the replacement app performs the Miracast projection itself.
5. A capability badge on each unsupported page/control rather than a dead button. Firmware is not shown as a disabled page; it is absent from navigation.

The app is offline-first. It talks to the adapter over the local Wi-Fi Direct/infrastructure path and does not require a Microsoft account, Store services, or internet access for configuration.

## Architecture

### Application shell

Use .NET 8 WPF targeting Windows 10/11 x64. Publish a self-contained single-file executable so the replacement is not dependent on the failing Store package or an external runtime installer. The app does not require administrator rights for normal operation.

Use MVVM with focused view models:

- `ConnectionViewModel` owns discovery, selection, refresh, and connection state.
- `AdapterSettingsViewModel` owns name, pairing/password, language, and basic information.
- `DisplaySettingsViewModel` owns overscan and wallpaper state.
- `NetworkSettingsViewModel` owns the optional Gen 3/4K infrastructure Wi-Fi workflow.
- `ConnectionSettingsViewModel` owns quick-connect and connection preferences.
- `DiagnosticsViewModel` owns read-only protocol checks and exportable local diagnostics.

### Discovery and capability detection

`AdapterDiscovery` enumerates active Windows network interfaces whose description or address indicates Wi-Fi Direct, then considers current ARP neighbors and previously successful adapter addresses stored in the app's local settings. It probes candidates with a short, cancellable request to `GET /cgi-bin/msupload.sh?Action=GetDeviceName`. A candidate is accepted only after it returns valid adapter JSON containing a device name.

The discovery layer tries the adapter's supported HTTP/HTTPS modes without disabling certificate validation globally. It creates a `CapabilityProfile` by probing read-only operations and interpreting HTTP status plus response schema. Unsupported operations are represented explicitly and never inferred from a missing UI control.

### Protocol layer

`IWirelessDisplayAdapterClient` is the only component allowed to communicate with the adapter. It exposes typed operations such as:

```text
GetIdentity()
GetOverscan()
SetOverscan(value)
GetPasswordProtection()  // backed by GetPBCMode; Disabled means PIN-only
SetPasswordProtection(enabled)  // backed by SetPBCMode
GetWallpaperInfo()
SetWallpaper(wallpaper)
GetWiFiSettings()
SetWiFiSettings(settings)
ForgetWiFi()
GetHdcpStatus()
SetHdcpStatus(enabled)
Restart()
```

The concrete client isolates request paths, query/form/file encoding, JSON parsing, timeouts, and response validation. The UI never constructs URLs or request bodies. Read operations are idempotent. Every write is cancellable, serialized per adapter, and followed by a read-back verification where the device supports it.

The current adapter's verified control root is `/cgi-bin/msupload.sh`; the client uses the device generation/capability profile to select the correct operation route and transport. Exact write encodings are covered by protocol fixtures and a reversible live integration test before a setting is exposed in the UI.

### Local state

Store only:

- Last successful adapter IP and display name.
- Window and navigation preferences.
- Non-secret capability cache with a short expiration.

Adapter passwords/PINs are not persisted. No firmware files or external telemetry are stored.

## Data flow

```text
Network interfaces + ARP neighbors
        ↓
AdapterDiscovery ── probe/read-only capability requests ── AdapterClient
        ↓                                                   ↓
ConnectionViewModel ← typed settings/state ← protocol responses
        ↓
Settings pages ── validated write ── AdapterClient ── read-back verification
```

When the adapter disappears, in-flight requests are cancelled, the UI changes to `Disconnected`, unsaved edits remain local to the page, and discovery retries on explicit refresh or a bounded background interval. A failed write never reports success based only on an HTTP 200; the response must parse and the post-write value must match when read-back is available.

## Error handling

- Network timeouts and connection loss become actionable messages such as `Adapter not reachable; reconnect through Windows wireless display settings and try again.`
- Malformed or unexpected adapter responses are recorded in the local diagnostics view with sensitive fields redacted.
- Unsupported operations are shown as unavailable for the detected generation.
- Invalid names, passwords, PINs, overscan ranges, image types, and image sizes are rejected before any request is sent.
- Restart is confirmation-gated in the app because it interrupts an active projection.
- Firmware-related URLs, actions, and file extensions are rejected by the protocol layer rather than merely hidden by the UI.

## Testing and acceptance criteria

### Unit tests

- Parse every supported read response, including malformed JSON, missing fields, and unexpected status codes.
- Validate adapter names, pairing/password input, overscan bounds, and wallpaper files.
- Encode and decode each supported write operation from deterministic fixtures.
- Build capability profiles from positive and negative probes.
- Verify firmware operations have no public client method and no request route.

### Integration tests

- Discover the currently connected adapter at `192.168.137.247` through the normal discovery path, not a hard-coded production address.
- Read its current name, overscan, and password-protection state.
- Apply a reversible temporary name and restore `WeightRoom-AD` after the test.
- Apply a reversible overscan change and restore the original value.
- Toggle password protection only if the adapter exposes the safe write/read-back flow; restore its original state.
- Confirm unsupported-generation pages remain unavailable rather than issuing incorrect requests.

### Manual acceptance

- Launch the published executable on Windows 11 with the Microsoft Store app closed.
- Configure the connected adapter without the Store app crashing or being required.
- Disconnect/reconnect the adapter and recover through `Refresh`.
- Verify no firmware page, firmware download, or automatic-update control appears.
- Verify the app remains usable without internet access once the adapter is connected.

## Delivery

The repository will contain the source, tests, a self-contained publish command, and a short `README.md` explaining how to build and run the utility. The first implementation targets the live adapter protocol and uses capability detection so later adapter generations can be added without changing the UI contract.
