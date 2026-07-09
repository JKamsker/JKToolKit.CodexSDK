# Codex 0.142.4 -> 0.144.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.144.0`.
- Verified `external/codex` is pinned to `rust-v0.144.0` and matches the `rust-v0.144.0` tag commit.
- Reviewed the local upstream delta from `rust-v0.142.4` to `rust-v0.144.0`, focusing on app-server protocol/schema drift and typed SDK projections.

## Update Status

- Generated upstream schema/DTO output is up to date.
- SDK handwritten app-server surfaces were updated for confirmed stable protocol drift.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.144.0` after this parity pass.

## Confirmed Upstream Changes

### 1. `thread/fork` accepts `lastTurnId`

Upstream added `lastTurnId` to `thread/fork` so clients can fork through a specific completed turn and omit later turns.

SDK changes:

- Added `ThreadForkOptions.LastTurnId`.
- Added `Protocol.V2.ThreadForkParams.LastTurnId`.
- Wired the option through `CodexAppServerThreadsClient.ForkThreadAsync`.
- Added focused serialization and request mapping tests.

### 2. Plugin summaries expose remote version and install policy source

Upstream `PluginSummary` now includes remote marketplace `version` and nullable `installPolicySource` with values such as `WORKSPACE_SETTING` and `IMPLICIT_CANONICAL_APP`.

SDK changes:

- Added `PluginSummaryDescriptor.Version`.
- Added `PluginSummaryDescriptor.InstallPolicySource` and typed `InstallPolicySourceValue`.
- Added `PluginInstallPolicySource` value object.
- Extended plugin parser tests to cover both fields.

### 3. MCP status and OAuth flows gained thread/failure context

Upstream added:

- `failureReason` on MCP startup status payloads.
- `threadId` on `mcpServer/oauth/login` params and OAuth completion notifications.

SDK changes:

- Added `McpServerStatusInfo.StartupStatus`, `Error`, and typed `FailureReason`.
- Added `McpServerStartupFailureReason`.
- Added `McpServerOauthLoginOptions.ThreadId` and `McpServerOauthLoginParams.ThreadId`.
- Preserved `ThreadId` on `McpServerOauthLoginCompletedNotification`.
- Added focused wrapper, serialization, and notification mapping tests.

## Audited Changes That Required No SDK Code

- Generated DTO/schema drift is already covered by `UpstreamGen check`.
- The upstream removal of `on-failure` from app-server `AskForApproval` schema affects generated DTO contracts. Existing SDK value-object parsing remains intentionally open for exec/MCP compatibility and historical payloads.
- Rollout, thread-store, and TUI-only changes did not expose additional SDK contract drift in this pass.

## Validation

Validation was run after implementation:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- Focused unit tests for touched app-server surfaces
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No additional actionable SDK drift was identified for the `0.142.4 -> 0.144.0` window during this pass.

## Upstream Sources

- `external/codex` local tags `rust-v0.142.4` and `rust-v0.144.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/ThreadForkParams.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/PluginSummary.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/McpServerStatusUpdatedNotification.ts`
