# Codex 0.144.3 -> 0.144.4 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.144.4`.
- Verified `external/codex` is pinned to `rust-v0.144.4` and matches the `rust-v0.144.4` tag commit.
- Reviewed the local upstream delta from `rust-v0.144.3` to `rust-v0.144.4`, focusing on SDK protocol/schema, exec, app-server, and generated DTO impact.

## Update Status

- Generated upstream schema/DTO output is up to date.
- No handwritten SDK code changes were required.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.144.4` after this parity pass.

## Confirmed Upstream Changes

### 1. Model catalog entries can carry Guardian auto-review policy text

Upstream added `model_messages.auto_review.policy` to the shared model catalog protocol and preserved that metadata when local config overrides clear instruction templates. Guardian auto-review session construction now prefers an explicitly managed `guardian_policy_config`, then falls back to the catalog-provided auto-review policy, then the built-in Guardian policy.

SDK impact:

- The app-server protocol schema and generated DTO bundle did not change between `0.144.3` and `0.144.4`; the generated check remained clean.
- The public SDK `model/list` wrapper projects the app-server model list response shape, which still does not expose `model_messages`.
- The SDK already preserves raw `model/list` entries through `ModelListEntry.Raw`, so any server-provided extra catalog metadata remains available without relaxing typed parsing.
- Guardian policy selection is internal upstream CLI behavior and has no corresponding SDK-side config builder or review-session implementation to update.

### 2. Guardian auto-review tests cover catalog policy injection

Upstream added tests that verify auto-review prewarm requests include the model catalog policy and that managed Guardian policy config takes precedence over catalog policy text.

SDK impact:

- The SDK parses Guardian auto-review notification payloads but does not construct Guardian review sessions itself.
- No notification, `review/start`, approval reviewer, or exec review wire contract changed.

## Audited Changes That Required No SDK Code

- `codex-rs/app-server-protocol` had no diff between `rust-v0.144.3` and `rust-v0.144.4`.
- `codex-rs/protocol/src/openai_models.rs` added `AutoReviewMessages` under model catalog metadata, but that shape is not part of the generated app-server schema consumed by the SDK.
- `codex-rs/models-manager` and `codex-rs/core/src/guardian` changes are upstream runtime behavior only.

## Validation

Validation was run after the audit:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the `0.144.3 -> 0.144.4` window during this pass.

## Upstream Sources

- `external/codex` local tags `rust-v0.144.3` and `rust-v0.144.4`
- `external/codex/codex-rs/protocol/src/openai_models.rs`
- `external/codex/codex-rs/models-manager/src/model_info.rs`
- `external/codex/codex-rs/models-manager/src/model_info_tests.rs`
- `external/codex/codex-rs/core/src/guardian/review.rs`
- `external/codex/codex-rs/core/src/guardian/review_session.rs`
- `external/codex/codex-rs/core/tests/suite/guardian_review.rs`
