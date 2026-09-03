# Codex 0.153.0 -> 0.153.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.153.1`.
- Verified `external/codex` is pinned to `rust-v0.153.1` and matches the `rust-v0.153.1` tag commit.
- Audited the local upstream delta from `rust-v0.153.0` to `rust-v0.153.1`, focusing on model catalog changes, Guardian computer-use review behavior, app-server protocol/schema drift, generated DTO drift, and existing SDK model-list/review surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.153.1`.
- No handwritten SDK code changes were required for this upstream patch.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.153.1` after this parity pass.

## Confirmed Upstream Changes

### 1. GPT-6-Astra is available through the API model catalog

Upstream added a hidden `gpt-6-astra` bundled model entry. The release notes describe this as API support without changing the default model or showing it in the model picker. The catalog entry is hidden by default, marked `supported_in_api`, and includes existing model-list concepts such as input modalities, default and supported reasoning efforts, multi-agent metadata, and runtime policy metadata.

SDK impact:

- The app-server `model/list` protocol shape did not change in this window.
- The SDK already exposes `ModelListOptions.IncludeHidden`, `ModelListEntry.Hidden`, open string reasoning-effort values, open `ModelMultiAgentVersion` parsing, input modalities, and `ModelListEntry.Raw`.
- New reasoning labels such as `max` and `ultra`, hidden visibility, and unprojected model-catalog fields are preserved without adding a closed enum or new public SDK type.
- No generated DTO or handwritten model-list wrapper update was required.

### 2. Guardian computer-use scoring now follows live model review requirements

Upstream tightened Guardian v2 computer-use scoring so node REPL/browser-style review is only sampled when the live parent model requires REPL auto-review. Switching a thread between ordinary and reviewed models now invalidates older skipped scores so a stale in-flight classifier result cannot approve a later reviewed-model action.

SDK impact:

- This is upstream runtime behavior inside the vendored Codex app-server/Guardian implementation.
- The SDK does not compute Guardian risk scores or maintain the upstream per-thread `ModelInfo` store.
- Existing app-server notification wrappers already parse Guardian review lifecycle payloads and preserve raw payloads for unmodeled details.
- No request, response, notification, or approval-handler contract changed in this window.

### 3. Additional upstream changes did not affect SDK contracts

Upstream also adjusted Guardian tests, MCP tool test behavior for declined Guardian elicitations, TUI test helpers, model-priority ordering, and Cargo/package version metadata.

SDK impact:

- No files under `codex-rs/app-server-protocol` changed between `rust-v0.153.0` and `rust-v0.153.1`.
- No SDK source generator inputs changed beyond the already vendored upstream tree.
- Generated schema checks confirm the repository's generated app-server DTO artifacts remain current.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AuthAccountConfigWrappersTests|FullyQualifiedName~AppServerNotificationMapperTests"`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.153.0 -> 0.153.1` window.

## Upstream Sources

- GitHub release `openai/codex` `rust-v0.153.1`
- Local upstream tags `rust-v0.153.0` and `rust-v0.153.1`
- `external/codex/codex-rs/models-manager/models.json`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/model.rs`
- `external/codex/codex-rs/app-server/src/models.rs`
- `external/codex/codex-rs/ext/guardian-v2/src/async_scorer/extension.rs`
- `external/codex/codex-rs/ext/guardian-v2/src/async_scorer/extension_tests.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/guardian_v2.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/guardian_v2_model_tests.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/mcp_tool.rs`
