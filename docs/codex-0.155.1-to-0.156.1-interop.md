# Codex 0.155.1 -> 0.156.1 Interop Research

## Scope

- Verified the `0.156.1` API marker matches the vendored `rust-v0.156.1` commit.
- Audited release notes and the local source delta from `rust-v0.155.1` through `rust-v0.156.1`.
- Focused on changed app-server protocol/schema, exec JSON output, thread lifecycle, account/config, MCP, model, plugin, and image-input surfaces.
- Regenerated the upstream schema/DTO bundle before the handwritten audit; generation was deterministic and produced no additional changes beyond the upstream-sync commit.

## Confirmed SDK Changes

### App-server projections

- Added typed MCP server capability payloads.
- Added model access-program projections and plugin onboarding-skill projection.
- Added managed model-provider definitions and effective login methods to config requirements.
- Added experimental account workspace-routing metadata.

### Thread and turn settings

- Added disabled-plugin identifiers to `turn/start`, `thread/settings/update`, lifecycle responses, and settings notifications.
- Added the experimental `thread/start.daybreakEnabled` option with capability guarding.
- Preserved the effective collaboration-mode payload returned by `thread/resume`.

### Image and exec interop

- Added uploaded file-ID image references for `turn/start` input.
- Preserved structured web-search results emitted in exec JSON events.

### Removed upstream rollback DTO

Upstream removed the deprecated `thread/rollback` API and its generated request DTO. The SDK's compatibility wrapper remains available for older servers, but now uses its handwritten legacy wire type and no longer depends on the removed generated artifact.

## No Actionable Drift

- The `0.156.1` hotfix only updates the model catalog with GPT-6 Sol and Luna; the existing model-list parser is forward-compatible with those catalog entries.
- TUI, voice, daemon, analytics, worktree, sandbox implementation, and message-board changes do not alter current SDK-owned stable behavior beyond the fields covered above.
- Existing raw JSON preservation continues to cover unknown and experimental extension fields.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- Focused app-server, exec, plugin, MCP, config, account, thread, and serialization tests
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.156.0` and `rust-v0.156.1`
- Local source and tests under `external/codex/codex-rs/app-server-protocol`
- Local app-server implementations under `external/codex/codex-rs/app-server`
- Local exec JSON projection under `external/codex/codex-rs/exec`
- Local protocol and thread-store changes under `external/codex/codex-rs/protocol` and `external/codex/codex-rs/thread-store`

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.155.1 -> 0.156.1` window.
