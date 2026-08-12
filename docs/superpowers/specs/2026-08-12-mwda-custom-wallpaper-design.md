# MWDA Control custom wallpaper and version display design

## Context

The Microsoft Wireless Display Adapter with the Microsoft Four Square logo is the WDA2/Gen-1.5 family. The installed Microsoft app identifies wallpaper customization as a firmware 2.0.8442 feature. Live testing against the connected adapter at `192.168.137.247` confirmed that the feature works when the original request shape is replayed:

- `POST /cgi-bin/msupload.sh?Action=UploadWallpaper`
- two PNG parts named `WallpaperBlackTint` and `WallpaperBlur`
- each part is a 1920×1080 PNG with the corresponding `.png` filename
- the adapter responds with `{"ErrorCode":0}` and reports wallpaper ID `0`

The replacement app currently sends one part named `wallpaper` and hard-codes legacy wallpaper capability to false. Legacy preset writes also use the wrong request shape for the connected adapter.

## Goals

1. Allow custom JPG and PNG images to be uploaded to supported WDA2 adapters using the Microsoft-compatible protocol.
2. Automatically prepare ordinary user images for the adapter’s 1920×1080 requirement.
3. Report legacy WDA2 custom wallpaper capability accurately at the model level, while giving a firmware-specific message when an older adapter rejects the upload.
4. Apply built-in legacy wallpapers using the request shape verified on the live adapter.
5. Show the application version in a small, user-visible location without duplicating the version constant.
6. Preserve the existing no-firmware-update scope.

## Non-goals

- Implementing firmware discovery through Microsoft’s desktop bridge.
- Implementing firmware downloads or updates.
- Reproducing Microsoft’s exact proprietary blur/tint algorithm pixel-for-pixel.
- Changing the modern-generation wallpaper protocol.

## Design

### Image preparation

Introduce a focused wallpaper image preparation component. It accepts a readable JPG or PNG stream, decodes it with the existing Windows/WPF image stack, preserves the source aspect ratio, center-crops as necessary, and resizes to exactly 1920×1080.

The component produces two PNG byte arrays:

- `WallpaperBlackTint.png`: the prepared image with a subtle black overlay.
- `WallpaperBlur.png`: the prepared image with a lightweight blur/downsample-upsample treatment.

The transformations are deterministic and remain entirely local. The upload path does not transmit the original source file; it transmits only the two generated adapter images.

### Protocol

Extend the wallpaper request builder to create the legacy upload request with `Action=UploadWallpaper` in the query string and two multipart binary parts. The part names, filenames, `image/png` content types, and `binary` content encoding match the original Microsoft shared library.

The existing public upload method can continue accepting a stream, filename, and content type; preparation and protocol shaping remain behind the advanced client boundary.

For WDA2 legacy preset wallpapers, use `GET /cgi-bin/msupload.sh?Action=SetPredefinedWallpaper&WallpaperID=<id>` and verify the resulting `GetWallpaperID` response. Modern preset behavior remains unchanged.

### Capability and errors

Legacy WDA2 wallpaper metadata will report custom wallpaper as model-supported. If the adapter returns `ErrorCode:-8` from the upload, convert it to a clear operation error explaining that custom wallpaper requires firmware 2.0.8442 or newer. Other device error codes remain visible as protocol failures.

The UI will enable the custom image button for legacy WDA2 wallpaper-capable adapters and display the firmware requirement in the failure message when applicable. It will not offer firmware update controls.

### Version display

Add a small `vX.Y.Z` label to the About view. The value is read from the running assembly’s product/informational version and normalized to the three-part application version. `Directory.Build.props` remains the single version source; the view must not contain a second hard-coded version.

### Testing

Add unit tests for:

- exact legacy multipart field names, filenames, content types, and action URL;
- generated image dimensions and deterministic output constraints;
- legacy WDA2 custom capability reporting;
- legacy preset GET request and read-back verification;
- adapter error `-8` mapping;
- version formatting and About view binding.

Add or update a live integration smoke test that can be run explicitly against the configured adapter. It must verify a custom upload, read back wallpaper ID `0`, and restore a selected built-in preset in a `finally` path. Firmware update endpoints remain excluded.

## Acceptance criteria

- A normal JPG/PNG can be selected and uploaded without manual preprocessing.
- A supported WDA2 adapter returns success and reports custom wallpaper ID `0`.
- An older WDA2 adapter receives a specific firmware requirement instead of a generic “unsupported” capability label.
- Built-in legacy wallpapers apply successfully on the currently observed adapter.
- About shows the current project version, and changing `VersionPrefix` changes the displayed version automatically.
- Existing automated tests remain green.
