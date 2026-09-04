# Codex 0.153.1 -> 0.153.2 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.153.2`.
- Verified `external/codex` is pinned to `rust-v0.153.2` and matches the `rust-v0.153.2` tag commit.
- Audited the local upstream delta from `rust-v0.153.1` to `rust-v0.153.2`, focusing on model catalog text, app-server protocol/schema drift, generated DTO drift, and the SDK `model/list` wrapper.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.153.2`.
- No handwritten SDK code changes were required for this upstream patch.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.153.2` after this parity pass.

## Confirmed Upstream Changes

### 1. GPT-6-Astra Fast tier copy was corrected

Upstream corrected the hidden `gpt-6-astra` priority speed tier description from `1.5x speed, increased usage` to `2x speed, increased usage`. The release notes describe this as displayed text only, with no runtime request behavior change.

SDK impact:

- The app-server `model/list` protocol shape did not change in this window.
- The SDK projects server-provided model catalog descriptions dynamically through `ModelListEntry.Description` and supported reasoning effort descriptions through `ModelReasoningEffortOption.Description`.
- The SDK also preserves raw `model/list` entries through `ModelListEntry.Raw`, so the corrected upstream text flows through without a static SDK constant or DTO change.
- No generated DTO or handwritten model-list wrapper update was required.

### 2. Additional upstream changes did not affect SDK contracts

Upstream also advanced the Rust workspace package version from `0.153.1` to `0.153.2`.

SDK impact:

- No files under `codex-rs/app-server-protocol`, `codex-rs/app-server`, `codex-rs/core`, or `codex-rs/exec` changed between `rust-v0.153.1` and `rust-v0.153.2`.
- The generated schema metadata was already updated by the upstream sync PR to record `codexCliVersion` `0.153.2`.
- Generated schema checks confirm the repository's generated app-server DTO artifacts remain current.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AuthAccountConfigWrappersTests"`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.153.1 -> 0.153.2` window.

## Upstream Sources

- GitHub release `openai/codex` `rust-v0.153.2`
- Local upstream tags `rust-v0.153.1` and `rust-v0.153.2`
- `external/codex/codex-rs/models-manager/models.json`
- `external/codex/codex-rs/Cargo.toml`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerConfigClient.CatalogAndFeedback.cs`
- `tests/JKToolKit.CodexSDK.Tests/Unit/AuthAccountConfigWrappersTests.cs`
