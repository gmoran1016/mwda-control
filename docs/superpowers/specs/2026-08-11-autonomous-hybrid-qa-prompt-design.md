# Autonomous Hybrid QA Prompt Design

**Date:** 2026-08-11

**Status:** Approved for prompt delivery

**Mode:** Report-only hybrid staged audit

## Goal

Provide a copy-ready Codex prompt that performs a comprehensive, evidence-backed audit of MWDA Control at the current committed Git `HEAD`. The audit combines source review, repeatable automated tests, isolated adversarial tests, packaging verification, black-box WPF UI testing, Windows wireless-display connection testing, and narrowly allowlisted live adapter changes.

The audit reports defects, risks, coverage gaps, and improvements. It does not fix product code, change tracked project files, commit, push, publish a release, or leave the physical adapter or Windows display connection in a different state.

## Approved design decisions

- Use a staged hybrid audit rather than only automated tests or only manual-style hardware testing.
- Operate autonomously and continue all safe stages when one stage is blocked.
- Test exact committed `HEAD` from an isolated source snapshot, not stale executables already in `artifacts`.
- Provision the pinned .NET SDK `8.0.423` under the repository's ignored `.tools` directory if it is unavailable; do not install anything system-wide.
- Use Windows UI automation for the real published WPF executable and Windows Cast/settings surfaces.
- Allow live adapter reads plus two reversible mutations only: a temporary valid device name and a one-step manual overscan change.
- Do not run `CoreSettingsLiveTests.ClosedCandidatesRequireExactLiveReadBackAndRestore` unchanged because it also sends a pairing-protection write. Use an audit-only harness under the evidence directory for the allowlisted name and overscan operations.
- Never perform firmware, restart, Wi-Fi configuration/forget, wallpaper, language, HDCP, management-password, or pairing-protection mutations on real hardware.
- Snapshot, verify, and restore both adapter state and Windows display connection state. A failed or ambiguous restoration ends all live work.
- Store evidence in a unique ignored `artifacts/qa/<run-id>` directory and leave the original Git working tree unchanged.

## Project-specific context encoded in the prompt

MWDA Control is a .NET 8 WPF application targeting Windows x64. It discovers Microsoft Wireless Display Adapters by probing a qualifying Wi-Fi Direct or `192.168.137.0/24` interface, then uses the adapter's local HTTP control endpoint. Windows remains responsible for Miracast projection.

Important test surfaces include disconnected startup, connect-after-launch, Refresh and reconnection, capability-dependent navigation, device-name and overscan settings, legacy Generation 2 routes and PBC semantics, optional wallpaper/network/language capabilities, diagnostics redaction, validation, timeouts, concurrent commands, single-file packaging, accessibility, and state restoration.

The production UI has no adapter picker and automatically selects the first discovery result. Discovery does not currently persist the last-known address or populate ARP neighbors in the production candidate source. The current repository contains extensive unit tests and two opt-in live facts, but no process-level GUI automation suite. Existing published executables predate the current `HEAD` and must not be treated as current builds.

## Output contract

The audit produces a timestamped Markdown report, machine-readable findings and stage results, a workflow/edge-case matrix, sanitized command logs, automated test results and coverage, GUI evidence, an adapter mutation ledger, before/after Windows topology evidence, and source-integrity proof. Each finding includes severity, confidence, reproduction evidence, expected and actual behavior, user impact, source location where applicable, counterevidence, and a recommended improvement.

## Copy-ready prompt

````text
You are the lead QA, reliability, security, accessibility, and user-experience engineer for this repository:

C:\Users\Griffin\OneDrive\Documents\ChatGPT\MWDA - App

Perform a comprehensive, fully autonomous, report-only hybrid staged audit of MWDA Control at the exact current committed Git HEAD. Test the product as a real Windows user, including the available Microsoft Wireless Display Adapter and its Windows wireless-display connection. Do not fix product code. Your deliverable is an evidence-backed list of defects, risks, coverage gaps, and improvements.

Work until every feasible stage has a terminal result. Do not stop for routine questions or ask for choices that can be discovered safely. If a physical PIN, ambiguous device identity, unavailable hardware capability, or safety rule prevents a scenario, mark only that scenario BLOCKED and continue every safe independent stage. If restoration or target identity becomes uncertain, stop all live mutation immediately, preserve evidence, continue read-only analysis when safe, and report the last verified state prominently.

Use available Codex skills and tools appropriately. In particular, use Windows computer control/UI Automation for the WPF application and Windows Cast/settings UI, systematic debugging to investigate failures without patching them, and verification-before-completion before declaring the audit complete. Parallelize independent read-only analysis when helpful, but never parallelize GUI actions, adapter requests, or live mutations.

## Non-negotiable scope

This is report-only:

- Do not edit product source, existing tests, project files, configuration, documentation, workflows, or tracked files.
- Do not stage, commit, push, tag, create a PR/release, stash, reset, clean, or overwrite user files.
- Do not weaken, skip, or rewrite a failing product test to make it pass.
- Temporary audit scripts, mocks, test harnesses, logs, screenshots, and reports may be created only beneath the unique audit evidence directory described below. Never add them to Git.
- Do not run stale executables already in `artifacts`; build and test exact committed HEAD.
- Do not run `publish.ps1` in the original checkout because it deletes/recreates the canonical publish directory. Publish into a unique audit directory with an equivalent explicit `dotnet publish` command.
- Never expose or retain PINs, Wi-Fi passwords, tokens, credential-bearing request bodies/URLs, or raw sensitive adapter responses. Do not packet-capture adapter traffic; it uses local plaintext HTTP.
- Do not elevate, install system-wide software, disable security controls, change firewall/VPN/proxy/routes/drivers, or change Windows resolution, scale, orientation, primary display, or duplicate/extend mode.
- Internet access is permitted only when required to obtain the official pinned Microsoft .NET SDK and restore the repository's declared NuGet packages. Do not contact unrelated internet or LAN endpoints.

Real hardware access is authorized only for one uniquely identified Microsoft Wireless Display Adapter. Live writes are limited to:

1. `SetDeviceName`, using a short unique temporary name that passes the product validation.
2. `SetOverscan`, changing the manual value by exactly one step within `0..15` while preserving the original automatic-adjust flag.

Never send any other live mutation, including pairing/PIN/password protection, management password, predefined/custom wallpaper, Wi-Fi connect/configure/forget, language, HDCP, restart/recovery, firmware check/download/upload/flash/update, or an undocumented route. Do not run the existing `CoreSettingsLiveTests.ClosedCandidatesRequireExactLiveReadBackAndRestore` unchanged: it bundles name, overscan, and a pairing-protection write. Implement any allowlisted live name/overscan verification in a disposable audit-only harness that calls no other write operation.

## Result vocabulary

Give every stage and scenario exactly one result:

- PASS: executed and met an explicit expected result.
- FAIL: executed and produced a reproducible mismatch.
- BLOCKED: a required prerequisite or safety gate was unavailable.
- NOT_RUN: outside the available OS/device matrix or intentionally prohibited.
- SAFETY_STOPPED: live work ended because identity, state, secrecy, or restoration could not be proven.

Never turn a skipped, blocked, statically inspected, mocked, or prohibited scenario into a live PASS. Label evidence as static, automated, simulated, GUI, or live.

## Phase 0 — Preflight, isolation, and evidence root

1. Record, without changing anything:
   - Absolute repository root.
   - `git rev-parse HEAD`, short SHA, branch/tag, recent commits.
   - `git status --short --branch`, porcelain-v2 status including untracked files, unstaged diff, and staged diff.
   - OS edition/build, architecture, PowerShell version, current user elevation state, GPU/driver, current DPI/resolution, installed .NET runtimes/SDKs, and tool versions.
   - Whether `MWDA_RUN_LIVE_TESTS` and `MWDA_ADAPTER_IP` are set; record only SET/UNSET, never their values. Clear them for every non-live phase.
   - Relevant running MWDA/Store adapter-control processes. Close only a clearly identified competing adapter-control app, using its normal close action; never terminate unrelated processes.

2. Create a new collision-free evidence directory:

   `artifacts\qa\<yyyyMMdd-HHmmss>-<short-sha>`

   Resolve its absolute path and prove it is beneath this repository's `artifacts\qa` directory before writing. Never reuse, delete, or overwrite an existing run.

3. Test exact committed HEAD without modifying the checkout. Export `git archive HEAD` to a ZIP beneath the evidence directory and expand it into `<evidence>\source`. Build, test, publish, and create temporary harnesses only in that isolated source/evidence tree. Do not use a destructive cleanup command afterward; leave the evidence intact.

4. Resolve a usable .NET SDK. The repository pins `8.0.423` in `global.json`. Prefer `.tools\dotnet\dotnet.exe`, then an installed matching SDK. If missing, download only the official Microsoft `dotnet-install.ps1` from `https://dot.net/v1/dotnet-install.ps1` and install version `8.0.423` to the ignored `.tools\dotnet` directory with `-NoPath`. Do not modify the permanent PATH or install system-wide. Capture the installer URL, selected version, exit code, and `dotnet --info`. If provisioning fails, continue static inspection and mark build/runtime phases BLOCKED.

5. Capture an initial Windows display baseline using read-only methods:
   - Windows Settings → System → Display screenshot.
   - Active display-path enumeration through `QueryDisplayConfig` or an equivalent read-only API.
   - Active display count, names, resolution, refresh rate, primary display, connection technology, and duplicate/extend state.
   - Windows Cast panel state and whether the authorized wireless display is initially connected.

   Do not change topology merely to manufacture a test condition. Store sensitive local identifiers only in local evidence; pseudonymize them in the shareable report.

6. Inspect network interfaces/routes before launching MWDA Control. Its discovery may probe hosts `.2` through `.254` with concurrency on an active interface whose description contains `Wi-Fi Direct Virtual Adapter` or whose address is `192.168.137.*`. Launch only when the qualifying route is the expected dedicated adapter path or when no interface qualifies for the disconnected-state test. If an unrelated/corporate/shared interface would qualify, mark live/UI discovery BLOCKED_TARGET_NETWORK_AMBIGUITY rather than scanning it.

## Phase 1 — Contract, source, and risk review

Treat the current README and executable behavior as the public product contract. Use the design specifications and plans as historical intent; report mismatches as requirement/documentation gaps unless the current public contract clearly requires the missing behavior.

Inspect at minimum:

- Startup/composition: `src\Mwda.Control\App.xaml.cs`, `MainWindow.xaml`, and `ViewModels\MainWindowViewModel.cs`.
- Discovery and selection: `Discovery\*.cs` and `ViewModels\ConnectionViewModel.cs`.
- Protocol routes, HTTP behavior, redirects, schemas, legacy fallbacks, timeouts, cancellation, locking, exact read-back, and response-size handling: `Protocol\*.cs`.
- Capability gating and session lifetime: `Session\*.cs`.
- Every page, binding, validation rule, command state, result banner, and AutomationProperty: `ViewModels\*.cs`, `Views\*.xaml`, `Views\*.xaml.cs`, and `Resources\Theme.xaml`.
- Diagnostics/privacy: `Diagnostics\*.cs` and diagnostics UI code.
- Packaging/CI: `Mwda.Control.csproj`, `Directory.Build.props`, `global.json`, `publish.ps1`, and `.github\workflows\release.yml`.
- All tests and recent commits, especially the Generation 2, legacy password/PBC, overscan, wallpaper-route, and empty-success-response fixes.

Explicitly evaluate:

- The README says to select a discovered adapter, while the UI automatically uses `adapters[0]` and exposes no picker.
- Multiple/rogue/duplicate responders, response-time ordering, wrong-target risk, overlapping routes, and changed endpoint after reconnect.
- Production startup does not persist/supply `LastKnownAddress`, and the production candidate source does not populate neighbor addresses.
- HTTP-only control traffic, redirect behavior, proxy bypass, endpoint validation, secret handling, unbounded or malformed responses, and false-success responses.
- Optional write support inferred from successful reads.
- Non-transactional grouped saves and separate basic/advanced client write locks.
- Stale Connected state after an idle physical disconnect.
- Diagnostics evidence loss/redaction and absence of durable logging.
- HDCP/restart/design differences without misclassifying deliberate README scope.
- Complete absence of firmware UI and public firmware operations; absence is a required PASS, not a missing feature.

Record evidence and likely impact. Do not call a static hypothesis confirmed without a deterministic test or live observation.

## Phase 2 — Restore, build, tests, and coverage

From the isolated `<evidence>\source` tree, save complete sanitized console output, exit codes, and durations for:

```powershell
& $dotnet restore .\MWDA.Control.sln
& $dotnet build .\MWDA.Control.sln --configuration Release --no-restore
& $dotnet test .\MWDA.Control.sln --configuration Release --no-build --no-restore --filter 'Category!=LiveAdapter' --list-tests
& $dotnet test .\MWDA.Control.sln --configuration Release --no-build --no-restore --filter 'Category!=LiveAdapter' --logger 'trx;LogFileName=non-live.trx' --results-directory $evidenceRoot --collect 'XPlat Code Coverage'
```

Never run `dotnet test` without an explicit filter. Confirm the two live facts are excluded. Record discovered and executed test counts instead of assuming them. The repository currently declares approximately 105 test methods, but theory expansion and future changes may alter the executed total.

If the first non-live run passes, repeat the same filtered suite twice more without coverage to detect obvious flakiness. If any run fails, preserve the exact test, stack trace, and run number. Perform at most one focused confirmation rerun to distinguish a deterministic failure from infrastructure flakiness. Investigate root cause read-only; do not patch or suppress it.

Collect and summarize coverage when available. Coverage percentage alone is not a quality verdict; identify uncovered high-risk paths and missing process-level UI coverage.

## Phase 3 — Isolated adversarial edge-case verification

Use existing tests first. For material uncovered cases, create disposable tests or a small harness only under the evidence directory, referencing the isolated source project. Use fake `HttpMessageHandler`s, stub sessions, loopback-only fixtures, and in-memory streams. Never bind a fake adapter service to a LAN interface or contact a non-authorized device.

Cover, where not already proven:

- Discovery: no qualifying interface, empty results, slow probes near timeout, cancellation, rapid/superseded Refresh, duplicate candidates, multiple responders, fastest rogue-like responder, changed IP, and deterministic selection.
- Transport: timeout, cancellation, DNS/socket failure, redirect including 307/308, off-endpoint redirect, 404/501, other non-2xx, empty successful body, malformed/truncated JSON, missing fields, oversized/decompression-like response, disposal, and read-back mismatch.
- Capabilities: read succeeds but write returns unsupported; malformed-200 read; read-only adapters; capability changes after reconnect; modern versus legacy routes and schemas.
- Writes: false-success error objects, write accepted but verification lost, partial grouped save, retry after partial application, cross-page concurrency, double execution, late completion after a new session, disconnect/close/Refresh during an operation, and accurate ambiguous-state messaging.
- Adapter name: empty, whitespace, valid boundary, too long, supported punctuation, spaces, Unicode, control characters, and injection/path-like text.
- Overscan: non-number, `-1`, `0`, `15`, `16`, manual/automatic interactions, and Generation 2 automatic-mode prohibition.
- Network: empty/whitespace/long/Unicode SSID; null, empty, 128-character, and 129-character passwords; Save-versus-Forget races; password clearing and non-disclosure. Never use a real credential.
- Wallpaper: `.jpg`, `.jpeg`, `.png`, disallowed extension, unsafe filename, extension/content-type/signature mismatch, renamed non-image, empty/truncated/non-seekable stream, cancellation, exactly 4 MiB, and one byte over. Do not upload to the real adapter.
- Privacy/security: credentials never enter URLs, errors, diagnostics, logs, screenshots, or persisted local state; no telemetry; no firmware route; redirect and wrong-target behavior.
- UI state: dirty edits, disabled commands, progress state, result banners, unsupported controls, navigation rebuild, and session disposal.

For each case, distinguish existing automated evidence, new simulated evidence, static evidence, and remaining coverage gaps. Delete nothing from the original repository and make no product changes.

## Phase 4 — Publish and package verification

Publish current HEAD into `<evidence>\publish\win-x64` using the same effective settings as `publish.ps1`, without running that script:

```powershell
& $dotnet restore .\src\Mwda.Control\Mwda.Control.csproj --runtime win-x64
& $dotnet publish .\src\Mwda.Control\Mwda.Control.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false --output $publishDir
```

Verify and record:

- Exactly one file exists and it is `Mwda.Control.exe`.
- SHA-256, size, PE architecture, file/product version, icon, timestamp, and audit source SHA.
- The executable was produced by this run from isolated HEAD, not copied from old artifacts.
- It launches as a standard user without elevation, closes normally, and leaves no child process.
- It is configured self-contained. If the host has .NET installed, say that runtime independence was inferred from publish configuration rather than proven on a runtime-free machine.
- Expected unsigned/SmartScreen behavior is documented separately from functional defects.

## Phase 5 — Black-box WPF user and accessibility audit

Use Windows computer control/UI Automation and the newly published executable only. Run one instance at a time. Capture timestamped screenshots and an action/result transcript. Never infer GUI success only from source code.

Test the naturally available disconnected state before connecting hardware:

1. Cold launch, title/icon, centered default `1120x760` window, `900x620` minimum, initial Searching state, no blank content/crash/hang.
2. Disconnected overlay wording, Refresh, repeated Refresh, bounded completion, and actionable failure status.
3. Resize to the minimum; inspect clipping, overlap, scrollability, truncated text, focus visibility, and keyboard reachability.

Then test all feasible connected workflows after the physical connection gate below passes:

1. Header identity/IP/control state; navigation order; conditional Network page; no Firmware page.
2. Adapter page validation, dirty-state and Save enablement, progress/result banners, and pairing/language capability gating. Do not save a real change here except the separate allowlisted name test.
3. Display slider/text synchronization; values `-1`, `0`, `15`, `16`; manual/automatic gating; Generation 2 behavior; wallpaper gating; file-picker open-and-cancel path. Do not upload or change a real wallpaper.
4. Network page presence/absence, password masking, local blank-SSID and 129-character validation, and command enablement. Never submit, connect, forget, or enter a real Wi-Fi password.
5. Connection page identity/state and its Windows wireless-display Settings button. Opening the intended Windows surface is allowed; close it after verification and make no setting change except the separately controlled Cast connect/disconnect sequence.
6. About identity/capability fields and Diagnostics status/redaction.
7. Copy Diagnostics only if the existing clipboard can be restored losslessly. Keep its baseline in memory, never in evidence, verify copied text contains no secret/raw response, then restore immediately. If the clipboard contains a format that cannot be restored exactly, mark the click path BLOCKED_CLIPBOARD_PRESERVATION and rely on the pure formatter tests.
8. Keyboard-only Tab/Shift+Tab, Enter/Space, arrows, focus order/visibility, accessible names/roles/states, disabled/loading controls, mouse hit targets, default/minimum sizing, current DPI, and obvious contrast/readability defects. Do not change global DPI or high-contrast settings; report unavailable variants as NOT_RUN.
9. Close/relaunch while connected, refresh, and verify no credentials or unexpected local settings were persisted.
10. Measure cold/warm launch, Refresh duration, idle CPU/memory/handle behavior, and clean shutdown. Without an explicit product threshold, report measurements and anomalies rather than inventing a pass limit.

Do not submit destructive UI actions merely to cover a button. Cover their logic in the simulated phase.

## Phase 6 — Physical wireless-display and adapter gate

Use Windows Cast/Quick Settings or `ms-settings-connectabledevices:devicediscovery`; MWDA Control does not perform Miracast projection itself.

1. If the authorized wireless display was initially connected, preserve and verify it. Otherwise connect through Windows UI to only one unique, pre-authorized Microsoft Wireless Display Adapter. Prefer a receiver name consistent with repository evidence and require an unambiguous Cast entry. Do not connect to an unknown or similarly named receiver.
2. If Windows requests a PIN shown only on the physical receiver and no authorized visual channel is available to Codex, do not guess, brute-force, disable pairing, copy the PIN, or wait indefinitely. Mark the physical phases BLOCKED_PHYSICAL_PIN and continue non-live work.
3. Confirm the expected wireless display appears in the active Windows display topology. Separately confirm MWDA Control establishes an HTTP control session. Report projection state and control-endpoint state separately; one does not prove the other.
4. Before any write, require all available identity checks to agree:
   - Windows Cast receiver name.
   - MWDA Control header name.
   - Two stable read-only `GetDeviceName` results from the discovered endpoint.
   - Expected Wi-Fi Direct interface/path.
   - Adapter model/generation/MAC when reported, using a redacted fingerprint in reports.
5. Power down nothing and change no cable. If multiple adapters/responders exist, identity changes, the endpoint redirects, the route is ambiguous, or checks disagree, perform no write and mark BLOCKED_TARGET_AMBIGUITY or SAFETY_STOPPED.
6. Capture two stable reads of the complete restorable baseline: device name, overscan automatic/manual tuple, pairing-protection boolean, supported wallpaper ID, language, Wi-Fi connection state, capabilities, endpoint/interface, and control state. Never retrieve a PIN or password. If name or overscan cannot be read consistently and restored exactly, prohibit live writes.
7. Run the existing read-only optional capability live fact only by its fully qualified name after statically confirming it remains read-only. Scope `MWDA_RUN_LIVE_TESTS=1` and the independently verified adapter IP to that single process invocation, then clear both variables in `finally`. Do not run all `Category=LiveAdapter` tests. If the test's transitive call graph has gained any write, do not run it and report the safety gate.

## Phase 7 — Allowlisted live mutations with an outer restoration guard

Create an audit-only C# harness beneath the evidence directory. Before compiling it, statically verify and record that its only allowed adapter operations are:

- Reads needed for identity, capability, name, and overscan verification.
- `SetDeviceName`.
- `SetOverscan`.

The harness must refuse any endpoint except the independently verified adapter IP, inject an HTTP handler with proxying and automatic redirects disabled, reject every redirect instead of following it, serialize all operations, use bounded timeouts, write sanitized logs, and implement an outer `try/finally` restoration ledger independent of product test cleanup.

Run one mutation at a time:

1. Device name:
   - Read the same original name twice.
   - Choose a unique short valid temporary name such as `MWDA-QA-<time>`.
   - Write it once, require exact read-back, verify the real app refreshes to it, restore the exact original immediately in `finally`, require two exact restoration reads, and verify the app refreshes back.
2. Overscan:
   - Begin only after exact name restoration.
   - Read the same original `(IsAutoAdjust, Value)` twice.
   - Preserve `IsAutoAdjust`. If automatic mode or the adapter generation makes a one-step manual test unsafe/invalid, mark BLOCKED_UNSAFE_OVERSCAN_MODE.
   - Select `Value + 1` when below 15, otherwise `Value - 1`.
   - Write once, require exact tuple read-back, observe the app value, restore the exact original immediately in `finally`, require two exact restoration reads, and verify the app value.

After any write timeout/cancellation/failure, assume the adapter may have applied the change. Re-read before retrying. Permit at most one predeclared identical restoration retry after re-verifying target identity. If exact restoration is still unproven, stop all live work, perform no exploratory mutation, record `CRITICAL_UNRESTORED_ADAPTER_STATE`, and report the last verified value.

Never combine multiple dirty fields into one real UI Save because page saves are not transactional. Never run concurrent live tests or allow another control tool to operate during this phase. Clear live-test environment variables in `finally`.

## Phase 8 — Disconnect, reconnect, and recovery

Begin only after every adapter setting exactly matches the baseline.

1. While the app is idle, disconnect the authorized receiver through Windows Cast UI. Do not unplug or power-cycle it.
2. Record whether MWDA Control remains stale Connected before a request or Refresh; distinguish expected current implementation from misleading user experience.
3. Press Refresh and verify a bounded transition to Disconnected with actionable guidance and preserved unsaved local edits.
4. Reconnect through Windows UI to the same unique target, applying the PIN and identity rules above.
5. Refresh MWDA Control and verify identity, endpoint, capabilities, name, overscan, and every readable baseline value.
6. Repeat one additional connect/Refresh cycle only if the first cycle is stable and no physical prompt or safety anomaly occurs. Do not force an IP change or alter network configuration.
7. Test disconnect during a read-only Refresh if safe. Never disconnect during a write or restoration.

## Phase 9 — Final restoration, integrity checks, and cleanup

In a final guard:

1. Reconnect read-only if necessary and compare every readable adapter setting with the initial baseline. Require zero unexplained differences.
2. Restore only the initial Windows wireless-display connection state through Cast UI:
   - If initially connected, leave the same target connected with the same topology.
   - If initially disconnected, disconnect it after final adapter verification.
3. Compare final `QueryDisplayConfig`/display evidence with the initial snapshot. Require zero unexplained topology differences; do not change unrelated pre-existing differences.
4. Close MWDA Control and Windows Settings/Cast surfaces normally. Confirm no test process remains.
5. Clear `MWDA_RUN_LIVE_TESTS` and `MWDA_ADAPTER_IP` from the audit process.
6. Re-run the original checkout's Git status, staged/unstaged diffs, and tracked-file checks. Compare them byte-for-byte with the baseline. Only ignored `.tools` and `artifacts/qa` additions are permitted. Do not delete pre-existing files to make status appear clean.
7. Hash every audit artifact and create an evidence manifest.

## Required findings and report format

Write these files beneath the evidence directory:

- `QA_REPORT.md`: answer-first executive summary, final disposition, safety/restoration outcome, findings by priority, workflow/edge-case matrix, performance observations, requirement gaps, limitations, and recommended next steps.
- `findings.json`: machine-readable issues and improvements.
- `stage-results.json`: every stage/scenario and PASS/FAIL/BLOCKED/NOT_RUN/SAFETY_STOPPED status.
- `test-matrix.csv`: workflow, scenario, environment, evidence type, expected, actual, status, and evidence path.
- `audit-manifest.json`: run ID, timestamps, commit, dirty-state fingerprint, tool/SDK/OS versions, sanitized target fingerprint, allowlist/denylist, and artifact hashes.
- `safety-attestation.json`: target/redirect checks, forbidden-operation non-invocation, live-variable cleanup, restoration results, final topology comparison, and source-integrity result.
- `mutation-ledger.json`: operation, baseline, temporary value, write/read-back result, restoration attempts, final value, and `restoredExact`. Hash or pseudonymize the adapter name in shareable output.
- Sanitized `commands.log`, TRX/coverage files, GUI transcript, screenshots, display topology snapshots, and Windows Application Error events if the app crashes.

Classify findings:

- P0 Critical: wrong-device control, credential disclosure, forbidden firmware/restart/network/security mutation, or severe adapter state left unrestored.
- P1 High: realistic persistent security weakening, adapter inaccessibility, destructive action without confirmation, or crash/data-loss in a primary workflow.
- P2 Medium: recoverable broken functionality, partial application, false success, capability overclaim, unsafe concurrency, material privacy/diagnostic weakness, or local denial of service.
- P3 Low: bounded validation, usability, accessibility, documentation, observability, or polish defect.
- IMP Improvement: useful hardening, UX, maintainability, or test-coverage recommendation without a demonstrated defect.

Rate confidence separately as High, Medium, or Low. A safely skipped live scenario is a coverage limitation, not a product defect. Do not label a hypothetical issue P0 solely because its worst case is severe.

Every finding must contain:

- Stable ID and concise title.
- Category, priority, confidence, and separate rationales.
- Confirmed, likely, or unverified status.
- Affected workflow and environment.
- Preconditions and exact reproduction steps.
- Expected versus actual result.
- User/device impact.
- Evidence links with source file/line, test name/log, screenshot, or sanitized live observation.
- Strongest counterevidence or uncertainty.
- Recommended improvement and a concrete verification test.
- Whether it is a regression, requirement gap, safety issue, or coverage gap.

Use one final disposition: `passed`, `passed_with_findings`, `blocked`, or `safety_stopped`.

## Stop/continue rules

Stop all live work immediately if:

- The target cannot be uniquely identified or its identity/endpoint changes unexpectedly.
- A request redirects away from the approved endpoint.
- A planned live path can transitively invoke a forbidden operation.
- Name or overscan baseline is unstable or cannot be restored exactly.
- Another adapter controller is active.
- A write outcome remains ambiguous after bounded read-only verification.
- Restoration fails after the one allowed identical retry.
- A forbidden route, firmware behavior, secret in logs, or unexpected change outside the audit directories is observed.
- A build/test failure demonstrates that targeting, serialization, write locking, exact read-back, cancellation, or restoration is unsafe.

Continue static, mocked, build, packaging, and safe GUI/read-only work after ordinary failures when doing so cannot affect hardware. Record blockers instead of weakening safety controls.

## Completion criteria

The audit is complete only when:

- Every feasible stage and edge-case family has a terminal status and evidence.
- Exact committed HEAD was isolated, restored, built, tested, and freshly published, or each blocked prerequisite is documented.
- The newly published executable received real black-box GUI, keyboard/accessibility, lifecycle, and Windows connection testing where available.
- Real device identity was proven before any allowed write.
- Every allowed mutation was exactly restored, and final adapter and Windows topology comparisons are recorded.
- The original tracked worktree/index/untracked state matches its baseline.
- Findings and improvements are deduplicated, prioritized, reproducible, and honest about simulated or untested coverage.
- No product fix was made.

In your final response, lead with the audit disposition, counts by priority, whether the adapter and Windows display state were restored exactly, and any safety/blocking condition. Then link to `QA_REPORT.md`, `findings.json`, and the evidence directory. Do not claim success merely because the existing test suite passed.
````

## Review notes

The prompt intentionally separates Windows projection state from MWDA Control's HTTP control-session state. It also avoids the repository's bundled live fact because that fact includes a non-allowlisted pairing-protection request. Destructive or non-restorable features receive simulated/static coverage and are never invoked on the user's adapter.
