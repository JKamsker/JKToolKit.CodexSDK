# Codex 0.129.0 -> 0.130.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.130.0` is the latest stable release as of 2026-05-11.
- Reviewed the upstream delta from `rust-v0.129.0` to `rust-v0.130.0`.
- Focused on generated app-server DTO drift and handwritten SDK surfaces that broke or could silently send stale runtime payloads.

## Update Status

- `external/codex` is pinned to `rust-v0.130.0`.
- `UPSTREAM_CODEX_VERSION.txt` is pinned to `0.130.0`.
- App-server DTOs were regenerated from the vendored upstream schema bundle.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check` passed.
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release` passed.

## Confirmed Upstream Changes That Mattered

### 1. `skills/list` removed extra user roots

Upstream removed the `perCwdExtraUserRoots` request field and the generated `SkillsListExtraRootsForCwd` DTO. The scheduled upstream sync failed because the handwritten skills client still referenced those generated types.

SDK fix:

- Stop serializing removed extra-root fields on `skills/list`.
- Keep `SkillsListOptions.ExtraRootsForCwd`, `SkillsListOptions.PerCwdExtraUserRoots`, and the public protocol property as compatibility no-ops so existing callers do not fail before reaching current Codex servers.
- Mark the lower-level public protocol property with `JsonIgnore` to prevent stale wire data when callers use that DTO directly.

### 2. Device-key and remote-control enrollment DTOs were removed

Upstream removed the device-key schema files and older remote-control enrollment/client audience DTOs. The SDK had no handwritten public wrappers depending on those generated types, so DTO regeneration was sufficient.

### 3. Plugin details now include bundled hooks

Upstream `plugin/read` includes a `hooks` array with hook key and event name. The SDK now exposes that as `PluginDetailDescriptor.Hooks`.

### 4. Remote-control status notification is part of the app-server surface

Upstream emits `remoteControl/status/changed` with `status` and nullable `environmentId`. The SDK now maps this to `RemoteControlStatusChangedNotification` instead of falling back to unknown.

### 5. Upstream sync workflow blocked PR creation on handwritten drift

The scheduled sync regenerated DTOs and ran full tests before creating a PR. When handwritten SDK code failed to compile after schema regeneration, the workflow failed repeatedly on `master` without creating an update branch that could be fixed.

Workflow fix:

- Keep version resolution, submodule update, DTO generation, and generator check in the sync workflow.
- Create the update PR before full test validation.
- Continue dispatching normal CI for the update branch, where handwritten parity failures are visible and fixable in the PR.

## SDK Changes Made

- Updated upstream pin and generated DTOs to `0.130.0`.
- Fixed `skills/list` against the removed extra-roots contract while preserving caller compatibility.
- Added plugin hook projection on plugin read results.
- Added typed remote-control status notification mapping.
- Changed upstream sync to avoid blocking future generated-update PR creation on handwritten compile drift.

## Validation

- Focused coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~CodexAppServerSkillsAppsClientTests|FullyQualifiedName~PluginClientTests|FullyQualifiedName~AppServerNotificationMapperTests"`
  - Result: `44` passed
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: up-to-date
- Full validation:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `735` passed, `15` skipped

## Remaining Drift

No additional compile-breaking drift was found after regeneration. The broader app-server long tail still exists where public ergonomic wrappers intentionally cover only stable SDK surfaces and preserve raw payloads for newer upstream fields.

## Upstream Sources

- `0.130.0`: <https://github.com/openai/codex/releases/tag/rust-v0.130.0>
- `external/codex/codex-rs/app-server/README.md`
- `external/codex/codex-rs/app-server-protocol/schema/json/v2/`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
