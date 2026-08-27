# Codex 0.150.0 -> 0.150.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.150.1`.
- Verified `external/codex` is pinned to `rust-v0.150.1` and matches the `rust-v0.150.1` tag commit.
- Audited the local upstream delta from `rust-v0.150.0` to `rust-v0.150.1`, focusing on app-server schema/protocol changes, feature-list projection, remote compaction behavior, and existing SDK surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.150.1`.
- No handwritten SDK code changes were required.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.150.1` after this parity pass.

## Confirmed Upstream Changes

### 1. Retained-image compaction budgeting is now stable and enabled by default

Upstream changed the `compaction_image_budget` feature from under-development and default-disabled to stable and default-enabled. The matching upstream test now expects retained images to be trimmed by default during remote compaction, while explicit disablement still preserves images.

SDK impact:

- No handwritten feature enum or feature allowlist exists in the SDK for this upstream flag.
- `experimentalFeature/list` already projects `name`, `stage`, `enabled`, `defaultEnabled`, optional display fields, and the raw JSON entry without hard-coding known feature IDs or stage values.
- Existing JSONL and app-server response item parsers preserve compaction and image payloads generically, so the changed default behavior is observable through upstream Codex without SDK parser changes.

### 2. Remaining upstream changes were release, CI, and test maintenance

The delta also includes the upstream workspace version bump, CI workflow changes for SDK binary staging, a Cargo manifest verification exception cleanup, a spelling allowlist update, and a test import cleanup.

SDK impact:

- These changes do not affect the app-server protocol schema, generated DTO contract, exec process arguments, JSONL event parsing, or public SDK wrappers.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AuthAccountConfigWrappersTests.ListExperimentalFeaturesAsync"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.150.0 -> 0.150.1` window.

## Upstream Sources

- GitHub release `rust-v0.150.1`
- `external/codex` local tags `rust-v0.150.0` and `rust-v0.150.1`
- `external/codex/codex-rs/features/src/lib.rs`
- `external/codex/codex-rs/core/tests/suite/compact_remote.rs`
