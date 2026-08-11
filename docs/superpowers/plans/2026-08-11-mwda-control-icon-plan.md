# MWDA Control Application Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a crisp MWDA Control icon to the WPF project and verify that local and GitHub-built single-file executables carry the icon.

**Architecture:** Keep an editable SVG source beside a committed multi-resolution ICO. Set the WPF `ApplicationIcon` property and explicitly include the ICO as a resource so MSBuild embeds the icon in the Windows executable without runtime image loading.

**Tech Stack:** SVG, Windows ICO, .NET 8 WPF, MSBuild `ApplicationIcon`, PowerShell verification, GitHub Actions.

## Global Constraints

- Preserve the existing .NET SDK pin at `8.0.423`.
- Preserve the existing self-contained Windows x64 single-file publish.
- Do not add a runtime image-loading dependency.
- Keep the icon original and free of Microsoft logos, Store artwork, and text.
- Include ICO image sizes 16, 24, 32, 48, 64, 128, and 256 px.
- Keep the firmware exclusion and all adapter behavior unchanged.

---

### Task 1: Create the icon assets

**Files:**
- Create: `src/Mwda.Control/Assets/Mwda.Control.svg`
- Create: `src/Mwda.Control/Assets/Mwda.Control.ico`

**Interfaces:**
- Produces the SVG artwork and multi-resolution ICO consumed by Task 2.
- The SVG viewBox is `0 0 256 256`; artwork uses a deep-blue rounded square, a white display outline, cyan wireless arcs, and a small cyan control accent.
- The ICO contains the same opaque artwork at 16, 24, 32, 48, 64, 128, and 256 px.

- [x] **Step 1: Add the vector source**

Create the SVG with simple paths and rounded geometry, no text, no external references, and no transparency requirement.

- [x] **Step 2: Convert the source to ICO**

Render the SVG to each required square size and package the PNG frames into `Mwda.Control.ico`. Preserve the exact seven image sizes and use opaque corners for reliable Windows shell rendering.

- [x] **Step 3: Inspect the asset**

Confirm the SVG contains no text or external links and enumerate the ICO frames to verify the seven required dimensions.

---

### Task 2: Wire the WPF project to the ICO

**Files:**
- Modify: `src/Mwda.Control/Mwda.Control.csproj`

**Interfaces:**
- MSBuild consumes `Assets\\Mwda.Control.ico` through `ApplicationIcon`.
- The ICO is explicitly included as a WPF `Resource`.
- No application code or runtime behavior changes.

- [x] **Step 1: Set the application icon**

Add `<ApplicationIcon>Assets\\Mwda.Control.ico</ApplicationIcon>` to the existing property group.

- [x] **Step 2: Include the ICO resource**

Add an item group containing `<Resource Include="Assets\\Mwda.Control.ico" />`.

- [x] **Step 3: Check the project diff**

Confirm only icon metadata and the new asset resource are added; do not alter target framework, publish mode, trimming, or nullable settings.

---

### Task 3: Verify the executable icon

**Files:**
- Verify: `publish.ps1`, `src/Mwda.Control/Mwda.Control.csproj`, `artifacts/publish/win-x64/Mwda.Control.exe`

**Interfaces:**
- The existing publish script remains the source of truth for local single-file output.
- The final publish directory contains exactly `Mwda.Control.exe`.
- Windows icon extraction returns an associated icon from the published executable.

- [x] **Step 1: Run the existing test suite**

Run the non-live suite with the repository-local .NET 8.0.423 SDK and require zero failures.

- [x] **Step 2: Build and publish Release**

Run `publish.ps1` and require the existing one-file output check to pass.

- [x] **Step 3: Verify the embedded icon**

Use Windows icon extraction against `artifacts\\publish\\win-x64\\Mwda.Control.exe` and confirm a non-null icon with a non-zero frame size.

- [x] **Step 4: Review the final diff**

Run `git diff --check`, inspect the staged paths, and confirm no unrelated files are staged.

---

### Task 4: Commit and publish the icon release

**Files:**
- Commit: icon assets, `Mwda.Control.csproj`, and any focused verification updates.

**Interfaces:**
- The existing `.github/workflows/release.yml` builds tags matching `v*`.
- A new version tag after the icon commit produces a release containing `Mwda.Control.exe`.

- [x] **Step 1: Commit the focused change**

Use commit message `feat: add application icon`.

- [x] **Step 2: Push the master branch**

Push the icon commit to `origin/master`.

- [x] **Step 3: Tag the release**

Create and push the next patch tag, `v1.0.3`, so GitHub Actions builds the icon-bearing executable.

- [x] **Step 4: Verify the GitHub release**

Require the tagged workflow to pass its tests, single-file check, artifact upload, and release creation. Confirm the release asset is named `Mwda.Control.exe`.
