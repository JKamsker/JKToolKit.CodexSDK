# Codex 0.106.0 -> 0.118.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.118.0` is the latest stable release as of `2026-03-31`.
- Reviewed changes from `rust-v0.106.0` through `rust-v0.118.0`.
- Focused on changes that matter for `JKToolKit.CodexSDK` interop, not TUI-only behavior.

## Update Status

- `external/codex` updated to `rust-v0.118.0`.
- `UPSTREAM_CODEX_VERSION.txt` updated to `0.118.0`.
- Upstream DTOs regenerated from the vendored schema bundle.
- Validation passed with `dotnet test -c Release`.

## Implementation Tasklist

- [x] Add first-class `ServiceTier` support across thread start/resume/fork, turn start, and typed thread summaries.
- [x] Add exec prompt-plus-stdin support while keeping the legacy stdin-prompt API working.
- [x] Add typed app-server account login start/cancel wrappers for API key, browser ChatGPT login, and device-code login.
- [x] Add stable wrappers for `model/list`, `experimentalFeature/list`, `config/value/write`, `config/batchWrite`, `account/logout`, and `feedback/upload`.
- [x] Add public wrappers for `command/exec*`, `plugin/*`, `fs/watch`, and `fs/unwatch`.
- [x] Add typed external-agent config interop and stricter import/detect parity.
- [x] Extend app/network projections with `pluginDisplayNames`, canonical domain permissions, canonical unix-socket permissions, and newer network flags.
- [x] Re-run focused unit coverage and full validation after the interop changes land.

## Remaining Highest-Value SDK Gaps

### 1. Thread lifecycle and `thread/read` are still too lossy

The SDK now covers the stable app-server method surface much more completely, but thread snapshots still lag upstream. The biggest remaining gaps are typed `thread.turns`, `gitInfo`, richer `thread.source`, and more complete `ThreadItem` unions on `thread/read`.

Relevant code:

- `src/JKToolKit.CodexSDK/AppServer/CodexThread.cs`
- `src/JKToolKit.CodexSDK/AppServer/CodexThreadSummary.cs`
- `src/JKToolKit.CodexSDK/AppServer/ThreadRead/CodexThreadItem.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientThreadResponseParsers.cs`

Recommended work:

1. Preserve lifecycle-response `thread.turns` arrays instead of only `TurnCount`.
2. Project typed `gitInfo` and richer thread-source metadata.
3. Cover more stable `ThreadItem` variants and object unions.

### 2. Exec resume/structured-output parity still trails the CLI

The SDK now supports prompt-plus-stdin, but `exec resume` still differs from upstream around target resolution, `--all` argument placement, ephemeral sessions, and structured-output success/failure handling.

Relevant code:

- `src/JKToolKit.CodexSDK/Exec/Internal/CodexResumeTargetResolver.cs`
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexSessionRunner.cs`
- `src/JKToolKit.CodexSDK/StructuredOutputs/Internal/StructuredOutputExecCapture.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/ProcessStartInfoBuilder.cs`

Recommended work:

1. Align resume target resolution with upstream `thread/list` / `updated_at` behavior.
2. Fix `resume --all` argv ordering and bootstrap capture for JSON-mode `thread_id`.
3. Stop reporting structured-output success from failed/interrupted turns.
4. Revisit the SDK’s current hard rejection of ephemeral exec handles.

### 3. Notifications and server requests still need better parity

Core notification mapping is much better than before, but there is still drift in notification backpressure semantics, raw-notification passthrough, typed request-approval DTOs, and some realtime/collaboration shapes.

Relevant code:

- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientCore.cs`
- `src/JKToolKit.CodexSDK/AppServer/IAppServerApprovalHandler.cs`
- `src/JKToolKit.CodexSDK/AppServer/Notifications/AppServerNotificationMapper.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerThreadsClient.cs`

Recommended work:

1. Preserve upstream best-effort vs must-deliver notification semantics, especially for command exec.
2. Add typed DTOs for the remaining common approval/server-request variants.
3. Tighten realtime/collaboration models where the SDK still flattens structured fields.

### 4. Skills/apps/plugins/catalog typing still weakens upstream contracts

Stable wrappers now exist, but several public result models are still looser than the upstream contract: `skills/list`, `app/list`, plugin summaries/results, config/account projections, and some catalog notifications still rely on nullable or raw JSON where upstream is strongly typed.

Relevant code:

- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerSkillsAppsClient.cs`
- `src/JKToolKit.CodexSDK/AppServer/AppDescriptor.cs`
- `src/JKToolKit.CodexSDK/AppServer/PluginModels.cs`
- `src/JKToolKit.CodexSDK/AppServer/PluginResults.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerConfigClient.CatalogAndFeedback.cs`

Recommended work:

1. Remove client-side validation that is stricter than upstream where the server already returns per-entry errors.
2. Tighten required fields and typed unions on stable list/read/install result shapes.
3. Continue replacing raw JSON placeholders with ergonomic typed projections where the upstream contract is stable.

### 5. Generated DTO drift remains a recurring maintenance problem

The vendored generator output is still materially wrong for several important unions and enums. The SDK now works around many of those gaps with manual projections, but generator drift still creates misleading types under `Generated/Upstream/AppServer/V2`.

Relevant code:

- `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2`
- `src/JKToolKit.CodexSDK.UpstreamGen/AppServerV2DtoGenerator.Fixups.cs`

Recommended work:

1. Keep important public interop on handwritten typed projections rather than raw generated unions.
2. Add targeted fixups for the most damaging generated placeholders so upstream DTOs are less misleading for internal use.

## Source Notes

- `0.108.0` and `0.109.0` did not both have normal GitHub Release bodies. For those, tag/compare data was taken from `gh` and repository history.
- `0.109.0` appears to be mostly a release-note/version bump on top of the `0.108.0` code line.

## Upstream Sources

- `0.107.0`: <https://github.com/openai/codex/releases/tag/rust-v0.107.0>
- `0.110.0`: <https://github.com/openai/codex/releases/tag/rust-v0.110.0>
- `0.111.0`: <https://github.com/openai/codex/releases/tag/rust-v0.111.0>
- `0.112.0`: <https://github.com/openai/codex/releases/tag/rust-v0.112.0>
- `0.113.0`: <https://github.com/openai/codex/releases/tag/rust-v0.113.0>
- `0.114.0`: <https://github.com/openai/codex/releases/tag/rust-v0.114.0>
- `0.115.0`: <https://github.com/openai/codex/releases/tag/rust-v0.115.0>
- `0.116.0`: <https://github.com/openai/codex/releases/tag/rust-v0.116.0>
- `0.117.0`: <https://github.com/openai/codex/releases/tag/rust-v0.117.0>
- `0.118.0`: <https://github.com/openai/codex/releases/tag/rust-v0.118.0>
