# Codex 0.152.1 -> 0.153.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.153.0`.
- Verified `external/codex` is pinned to `rust-v0.153.0` and matches the `rust-v0.153.0` tag commit.
- Audited the local upstream delta from `rust-v0.152.1` to `rust-v0.153.0`, focusing on app-server protocol/schema changes, plugin reconciliation, thread metadata projections, approval/request routing, generated DTO drift, and existing SDK wrapper surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.153.0`.
- Handwritten SDK parity changes were required for newly exposed thread metadata and the stable `plugin/reconcile` RPC.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.153.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Thread metadata now includes model settings

Upstream added nullable `model` and `reasoningEffort` fields to the app-server `Thread` contract. These fields describe the current configured model settings when the thread is loaded, or the latest persisted settings when available; they are not per-turn execution telemetry.

SDK impact:

- `CodexThreadSummary.Model` already existed, but the parser only read the lifecycle response envelope. That dropped `thread.model` on `thread/list`, `thread/read`, and nested thread payloads.
- Updated thread parsing to read `model` from the thread object first, while preserving the lifecycle envelope fallback.
- Added `CodexThreadSummary.ReasoningEffort` and parse coverage for `thread.reasoningEffort`.

### 2. App-server exposes stable `plugin/reconcile`

Upstream added `plugin/reconcile`, which performs blocking remote installed-plugin reconciliation and reports plugins affected by bundle, enablement, or removal changes. The response includes `changedPlugins`, `failedRemotePluginIds`, and `failedMaterializationRemotePluginIds`.

SDK impact:

- The generated DTOs already reflected the schema, but the public plugin client and resilient wrapper did not expose the stable RPC.
- Added `PluginReconcileOptions`, `PluginReconcileResult`, and `PluginReconcileChangedPlugin`.
- Added `ReconcilePluginsAsync` on `CodexAppServerClient` and `ResilientCodexAppServerClient`, with adapter forwarding and strict response parsing for the current required arrays.

### 3. Approval and app-link changes are upstream-owned or already covered

Upstream added per-account app approval settings, app-link approval metadata on MCP approval elicitations, explicit app account selectors for app tool calls, and an `approvalsReviewer` field on experimental `turn/settings/update`.

SDK impact:

- App/config generated DTOs preserve the new app-link configuration shapes.
- The SDK already supports approval reviewer routing on thread start, resume, fork, turn start, and thread settings update.
- `turn/settings/update` remains an experimental upstream RPC that this SDK does not currently expose as a handwritten public method, so no speculative public wrapper was added in this pass.
- MCP approval/request payloads keep raw JSON, so new app-link metadata is not lost by existing handlers.

### 4. Additional upstream changes did not require SDK code changes

Upstream also changed Guardian review behavior, remote plugin CLI internals, context-management feature activation, header injections in network requirements, structured asynchronous user-input tools, result-source analytics, rollout compression, symlinked thread forks, and TUI reconnect/history behavior.

SDK impact:

- Guardian, context management, network requirement enforcement, result-source analytics, and TUI reconnect behavior are upstream runtime behavior delegated to the vendored CLI/app-server.
- The structured async user-input tool appears in upstream tool schemas and history items; current SDK thread-item parsing preserves unmodeled fields through raw payloads.
- Generated DTO and schema checks are current, so no additional generated-artifact changes were needed.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ThreadApiParsingTests|FullyQualifiedName~PluginClientTests"`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.152.1 -> 0.153.0` window after the changes above.

## Upstream Sources

- GitHub release `openai/codex` `rust-v0.153.0`
- Local upstream tags `rust-v0.152.1` and `rust-v0.153.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread_data.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/turn.rs`
- `external/codex/codex-rs/app-server/src/request_processors/plugins/reconcile.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/plugin_reconcile.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/turn_settings_update.rs`
