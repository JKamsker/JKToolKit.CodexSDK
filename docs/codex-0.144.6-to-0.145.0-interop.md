# Codex 0.144.6 -> 0.145.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.145.0`.
- Verified `external/codex` is pinned to `rust-v0.145.0` and matches the `rust-v0.145.0` tag commit.
- Audited the local upstream delta from `rust-v0.144.6` to `rust-v0.145.0`, focusing on app-server protocol/schema, generated DTOs, handwritten app-server wrappers, notification mapping, exec/session behavior, and changed tests.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.145.0`.
- Handwritten SDK parity changes were required for newly exposed app-server app APIs and simple notifications.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.145.0` after this parity pass.

## Confirmed Upstream Changes

### 1. App-server exposes `app/read` and `app/installed`

Upstream added typed v2 contracts for:

- `app/read`, which reads metadata for up to 100 app/connectors ids and optionally includes display-only public tool summaries.
- `app/installed`, which reads the committed installed connector runtime snapshot and can evaluate effective configuration for a loaded thread.

SDK impact:

- The SDK already exposed `app/list`, but did not expose either new request.
- Added `ReadAppsAsync` and `ReadInstalledAppsAsync` to the direct app-server client and resilient client.
- Added public raw-preserving result models for connector metadata, tool summaries, and installed runtime state.
- Added request validation for empty, blank, and over-limit `app/read` ids.
- Added unit coverage for request DTOs, response parsing, validation, and resilient parity.

### 2. App-server emits additional simple notifications

Upstream added or formalized:

- `rawResponse/completed`, carrying exact per-response usage including cache-write tokens.
- `thread/environment/connected` and `thread/environment/disconnected`, carrying thread and environment ids.

SDK impact:

- Added typed notification records and mapper coverage for these methods.
- Malformed payloads continue to map to `UnknownNotification`.

### 3. App/plugin metadata and token usage schemas expanded

Upstream generated schemas now include connector metadata, scheduled task summaries on plugin details, runtime connector state, cache-write token usage, richer app metadata, and related enum additions such as `sessionEnd` hooks.

SDK impact:

- Existing plugin and app list projections preserve raw JSON for metadata fields that do not yet need first-class SDK wrappers.
- Existing hook notification parsing accepts event names as strings, so the new `sessionEnd` enum value does not require SDK enum changes.
- Existing token usage notifications preserve raw usage JSON, so cache-write token fields remain available without changing a public typed token model.

### 4. Exec/runtime changes remain upstream-owned

The upstream delta includes Windows sandboxing, managed network proxy, command safety, code-mode yield, shell/path, realtime/audio, and TUI changes.

SDK impact:

- Exec mode delegates runtime command policy, sandbox behavior, and code-mode execution to the vendored Codex CLI.
- No local exec/session discovery behavior in this SDK was changed by the audited upstream files.

## Audited Changes That Required No SDK Code

- `app/list` remains compatible with the existing SDK wrapper and parser.
- Plugin scheduled task fields are preserved through raw plugin payloads.
- Environment status request/response support already exists in the SDK; only connection notifications needed typed mapping.
- `rawResponseItem/completed` support already existed; only the new response-level completion notification was missing.
- Realtime audio and transcript notification handling already had SDK coverage matching the changed protocol.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~CodexAppServerSkillsAppsClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~AppServerNotificationMapperFaultToleranceTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the `0.144.6 -> 0.145.0` window during this pass.

## Upstream Sources

- `external/codex` local tags `rust-v0.144.6` and `rust-v0.145.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/apps.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/environment.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/app_read.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/app_installed.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/environment_add.rs`
