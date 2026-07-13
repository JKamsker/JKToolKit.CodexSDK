# Codex 0.144.1 -> 0.144.3 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.144.3`.
- Verified `external/codex` is pinned to `rust-v0.144.3` and matches the `rust-v0.144.3` tag commit.
- Reviewed the local upstream delta from `rust-v0.144.1` to `rust-v0.144.3`, focusing on SDK protocol/schema, exec, app-server, and generated DTO impact.

## Update Status

- Added named SDK constants for upstream's first-class `max` and `ultra` reasoning effort values.
- Generated upstream schema/DTO output is up to date.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.144.3` after this parity pass.

## Confirmed Upstream Changes

### 1. Thread resume restores persisted model settings more carefully

Upstream changed app-server resume behavior so persisted thread metadata can restore model, provider, and reasoning effort without being accidentally overwritten by current config defaults. It also records `ThreadSettingsApplied` events into thread metadata, including the ability to clear persisted reasoning effort.

SDK impact:

- `ThreadResumeOptions` already omits model, provider, and config overrides unless the caller sets them, which matches the upstream restore-from-thread path.
- `ThreadSettingsUpdateOptions.CollaborationMode` remains a raw JSON escape hatch and can carry `settings.reasoning_effort: null` for the clear path introduced upstream.
- `CodexThread.ReasoningEffort` and related parsers already preserve nullable lifecycle reasoning effort values.

### 2. Max and Ultra reasoning are now visible advanced choices

Upstream's protocol enum already includes `max` and `ultra`, and the `0.144.3` TUI delta makes those values explicit advanced reasoning choices. Ultra reasoning is also referenced by app-server docs as the source of proactive multi-agent behavior.

SDK impact:

- Added `CodexReasoningEffort.Max` and `CodexReasoningEffort.Ultra` named constants while preserving custom-value parsing for future upstream efforts.
- Updated focused value-object tests and the app-server turn-start protocol comment to list the current upstream-known values.

### 3. Guardian review prompt wording changed

Upstream adjusted guardian policy/review prompt layout and removed an unused spec-plan tool reference.

SDK impact:

- This repository does not model guardian prompt text or the removed internal tool spec.
- No exec, app-server, or generated DTO changes were required.

## Audited Changes That Required No SDK Code

- `codex-rs/app-server-protocol` had no diff between `rust-v0.144.1` and `rust-v0.144.3`.
- Generated schema/DTO checks remained clean.
- TUI-only advanced reasoning picker behavior has no corresponding SDK UI surface beyond the reusable reasoning-effort value object.

## Validation

Validation was run after implementation:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter FullyQualifiedName~CodexReasoningEffortTests`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the `0.144.1 -> 0.144.3` window during this pass.

## Upstream Sources

- `external/codex` local tags `rust-v0.144.1` and `rust-v0.144.3`
- `external/codex/codex-rs/app-server/src/request_processors/thread_processor.rs`
- `external/codex/codex-rs/thread-store/src/thread_metadata_sync.rs`
- `external/codex/codex-rs/state/src/extract.rs`
- `external/codex/codex-rs/protocol/src/openai_models.rs`
- `external/codex/codex-rs/tui/src/chatwidget/model_popups.rs`
- `external/codex/codex-rs/tui/src/chatwidget/reasoning_shortcuts.rs`
