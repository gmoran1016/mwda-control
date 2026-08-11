# Password-Protection Route Fallback Design

## Context

The Microsoft Wireless Display Adapter with the Microsoft Four Square logo uses the legacy password-protection endpoint:

```text
GET /cgi-bin/msupload.sh?Action=GetPasswordProtectState
```

It returns HTTP 200 with a case-insensitive legacy response such as:

```json
{"passwordProtect":false}
```

The newer adapter variant uses `GetPBCMode` and returns `PBCModeStatus`. MWDA Control v1.0.8 can parse the legacy response, but it always requests the newer route. On the legacy adapter that route returns HTTP 404, so password protection is incorrectly reported as unavailable.

## Goal

Make password-protection detection and configuration work on both the modern PBC-mode protocol and the legacy password-protection protocol without changing the public settings model or transmitting a password value.

## Design

`AdapterClient` will negotiate the password-protection protocol per session:

1. Request password protection through the existing modern `GetPBCMode` route.
2. If that route is explicitly unsupported with HTTP 404 or 501, retry the read through `GetPasswordProtectState`.
3. Record the successful protocol variant for the remainder of the session.
4. Use the recorded variant for password-protection writes and their exact read-back validation.

The modern variant will retain `SetPBCMode` with `PBCModeStatus=Disabled|Enabled`. The legacy variant will use `SetPasswordProtect` with the boolean `PasswordProtect` field and read back through `GetPasswordProtectState`. Existing request-encoding characterization remains in force for each variant.

The JSON parser will continue to use case-insensitive property matching and will accept both `PBCModeStatus` and `PasswordProtect`. It will not treat malformed JSON, unknown status values, or unrelated HTTP failures as evidence to switch protocols.

## Error handling

- HTTP 404 and 501 from the modern password-protection read select the legacy variant.
- Other non-success responses remain protocol failures and are shown through the existing redacted diagnostics path.
- A successful response with an unrecognized schema remains a malformed-response failure; it does not silently issue a second request through another protocol.
- If the legacy route is also unsupported, capability detection reports password protection as unsupported using the existing capability-probing behavior.

## Testing

Protocol tests will cover:

- modern `GetPBCMode` read and existing modern write/read-back behavior;
- fallback from modern HTTP 404 to legacy `GetPasswordProtectState`;
- parsing of the observed lowercase legacy property name;
- legacy `SetPasswordProtect` write and legacy read-back matching;
- no fallback for an unrelated HTTP failure;
- existing parser rejection for missing or invalid response properties.

The full non-live test suite, Release build, single-file publish verification, and the available live adapter tests will be run before release. The remote Four Square-logo adapter will be validated through the user-provided HTTP response and a fresh v1.0.9 executable.

## Scope boundaries

This change does not add firmware behavior, change PIN values, alter wallpaper support, change discovery, or modify the public view-model contract. It is a patch-level compatibility release: v1.0.9.
