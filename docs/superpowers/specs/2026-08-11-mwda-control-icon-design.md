# MWDA Control Icon Design

**Date:** 2026-08-11
**Status:** Approved design; implementation pending written-spec review

## Goal

Give MWDA Control a recognizable application icon and ensure the icon is embedded in the Windows executable produced locally and by GitHub Actions.

## Visual direction

Use a clean vector-style mark with no text:

- Deep-blue rounded-square background for a strong Windows taskbar and Explorer silhouette.
- White display outline as the primary subject.
- Cyan wireless signal arcs above the display to communicate wireless display control.
- A small cyan control accent inside the display area to distinguish the app from a generic cast icon.
- High contrast, generous padding, and simple geometry so the mark remains legible from 16 px through 256 px.

The design is original and avoids reproducing Microsoft logos or Store artwork.

## Asset strategy

- Keep a readable vector source at `src/Mwda.Control/Assets/Mwda.Control.svg` for future editing.
- Commit a multi-resolution `src/Mwda.Control/Assets/Mwda.Control.ico` for Windows application metadata.
- Include 16, 24, 32, 48, 64, 128, and 256 px image entries in the ICO.
- Use opaque artwork with rounded corners; no runtime image loading is required.

## Project wiring

Update `src/Mwda.Control/Mwda.Control.csproj` with `ApplicationIcon` pointing to the committed ICO and an explicit WPF resource include. The existing WPF application and self-contained single-file publish remain unchanged apart from the executable icon metadata.

## Verification

1. Confirm the SVG and ICO assets exist and the ICO contains the expected image sizes.
2. Run the existing non-live test suite.
3. Build Release.
4. Publish the self-contained Windows x64 executable.
5. Confirm the publish directory still contains exactly `Mwda.Control.exe`.
6. Confirm the built executable exposes an associated icon using Windows icon extraction.
7. Push the change and rely on the existing tag workflow to repeat the same publish verification on GitHub Actions.

## Scope exclusions

This change does not alter the application UI, adapter protocol, firmware boundary, versioning policy, or release workflow behavior.
