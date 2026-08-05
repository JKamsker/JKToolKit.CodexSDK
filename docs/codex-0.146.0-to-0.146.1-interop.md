# Codex 0.146.0 -> 0.146.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.146.1`.
- Verified `external/codex` is pinned to `rust-v0.146.1` and matches the `rust-v0.146.1` tag commit.
- Audited the local upstream delta from `rust-v0.146.0` to `rust-v0.146.1`, focusing on app-server model catalog schema changes and TUI permission behavior that consumes model catalog metadata.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.146.1`.
- Handwritten SDK parity changes were required for newly exposed model specialty metadata.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.146.1` after this parity pass.

## Confirmed Upstream Changes

### 1. Model catalog entries expose model specialty metadata

Upstream added optional `model_specialty` metadata to Rust model catalog types and `modelSpecialty` to the app-server `model/list` response schema. The TUI now uses this metadata to identify cyber-specialized models and adjust permission prompts/defaults.

SDK impact:

- Added `ModelListEntry.ModelSpecialty`.
- Updated `model/list` parsing to project the optional `modelSpecialty` field while continuing to preserve the raw model entry JSON.
- Added regression coverage for the public `ListModelsAsync` wrapper.

### 2. Cyber model auto-review defaults are TUI-owned behavior

Upstream changed TUI model selection and permission profile flows so cyber-specialized models can default to workspace permissions plus auto-review when policy requirements allow it, and can show stronger full-access warnings.

SDK impact:

- No exec-mode change is required because the SDK delegates interactive TUI behavior to the vendored Codex CLI.
- Existing app-server thread and turn override models already expose `approvalPolicy`, `approvalsReviewer`, and `permissions`, so callers can send the same wire settings explicitly.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter FullyQualifiedName~AuthAccountConfigWrappersTests`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the `0.146.0 -> 0.146.1` window.

## Upstream Sources

- `external/codex` local tags `rust-v0.146.0` and `rust-v0.146.1`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/model.rs`
- `external/codex/codex-rs/protocol/src/openai_models.rs`
- `external/codex/codex-rs/app-server/src/models.rs`
- `external/codex/codex-rs/tui/src/app/thread_settings.rs`
- `external/codex/codex-rs/tui/src/chatwidget/permissions_menu.rs`
