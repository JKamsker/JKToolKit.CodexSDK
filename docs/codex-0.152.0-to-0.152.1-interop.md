# Codex 0.152.0 -> 0.152.1 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.152.1`.
- Verified `external/codex` is pinned to `rust-v0.152.1` and matches the `rust-v0.152.1` tag commit.
- Audited the local upstream delta from `rust-v0.152.0` to `rust-v0.152.1`, focusing on Guardian approval review behavior, model metadata, app-server schema/DTO drift, and existing SDK review/model-list surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.152.1`.
- No handwritten SDK code changes were required for this upstream patch.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.152.1` after this parity pass.

## Confirmed Upstream Changes

### 1. Guardian node REPL policy can come from model metadata

Upstream added `AutoReviewMessages.node_repl_policy` to model metadata and now uses it when constructing Guardian node REPL review context. The built-in node REPL policy remains the fallback when model metadata does not provide one, and an explicitly empty model-owned policy suppresses node REPL policy injection.

SDK impact:

- The app-server `model/list` protocol does not expose `model_messages` or `node_repl_policy`, and the app-server schema did not change in this window.
- The SDK's public `ModelListEntry.Raw` still preserves any app-server fields returned in future without weakening the current typed projection.
- No generated DTO or handwritten model-list wrapper update was required.

### 2. Guardian review session reuse and model-safety checks include node REPL policy

Upstream now includes the resolved node REPL policy in Guardian review session reuse keys and rejects destination model changes that alter parent-fallback node REPL policy unless an explicit reviewer override prevents that fallback path.

SDK impact:

- This is upstream runtime behavior inside the vendored Codex CLI/core Guardian implementation.
- The SDK does not construct Guardian review sessions, model-safety fallback policy text, or node REPL policy prompts itself.
- Existing exec review and app-server review wrappers delegate this behavior to the CLI/app-server and do not need new request or response fields.

### 3. Additional upstream changes did not affect SDK contracts

The remaining upstream changes were patch stamping and tests around the Guardian policy behavior.

SDK impact:

- No app-server protocol files changed between `rust-v0.152.0` and `rust-v0.152.1`.
- The generated schema metadata version changed in the upstream sync PR, and `UpstreamGen check` confirms generated output is current.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.152.0 -> 0.152.1` window.

## Upstream Sources

- GitHub release `openai/codex` `rust-v0.152.1`
- Local upstream tags `rust-v0.152.0` and `rust-v0.152.1`
- `external/codex/codex-rs/protocol/src/openai_models.rs`
- `external/codex/codex-rs/core/src/context/guardian_node_repl_policy.rs`
- `external/codex/codex-rs/core/src/guardian/review.rs`
- `external/codex/codex-rs/core/src/guardian/review_session.rs`
- `external/codex/codex-rs/core/src/session/step_activation.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/model.rs`
