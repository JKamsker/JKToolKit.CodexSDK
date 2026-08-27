# Codex 0.149.1 -> 0.150.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.150.0`.
- Verified `external/codex` is pinned to `rust-v0.150.0` and matches the `rust-v0.150.0` tag commit.
- Audited the local upstream delta from `rust-v0.149.1` to `rust-v0.150.0`, focusing on app-server schema/protocol changes, MCP status/event streaming, approval requests, config requirements, and existing SDK surfaces.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.150.0`.
- Handwritten SDK parity changes were required for stable app-server config requirement projection, MCP runtime status projection, and command-approval request kind handling.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.150.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Config requirements gained browser/computer-use policy detail

Upstream expanded `configRequirements/read` with `additionalDeveloperInstructions`, `allowBrowserAndComputerUse`, `inAppBrowser`, richer `browserUse` origin policies, and richer `computerUse` platform/app access requirements.

SDK impact:

- Added typed `ConfigRequirements` properties for the new top-level requirement fields.
- Added typed browser/computer-use requirement projections for allow/deny policy values, browser origin policy, access approval lifetime, macOS bundle ids, Windows AUMIDs, and Windows executable requirements.
- Preserved raw JSON payloads so future requirement fields remain available before a later handwritten projection exists.

### 2. MCP status now includes runtime connection state

Upstream added `runtimeStatus` to MCP server status entries, with values such as `notStarted`, `starting`, `connected`, `authenticationRequired`, `failed`, `cancelled`, and `disabled`.

SDK impact:

- Added `McpServerStatusInfo.RuntimeStatus`.
- Added `McpServerRuntimeStatus` and parser coverage for the upstream wire values.

### 3. Command approval requests distinguish stdin writes

Upstream added a `kind` field to `item/commandExecution/requestApproval`, defaulting to `command` for older servers and using `writeStdin` for terminal input approvals. Upstream also added a matching Guardian review action variant.

SDK impact:

- Added `CommandExecutionRequestApprovalParams.Kind` with a default of `command`.
- Preserved `writeStdin` and any future kind values as wire strings.
- Updated the console approval prompt to display the kind.

### 4. New MCP event stream and realtime timeline schemas are generated-only

Upstream added `mcpServer/event/stream/start` and `mcpServer/event/stream/notification`, plus experimental realtime timeline item schemas. Existing SDK stable surfaces do not expose dedicated wrappers for those APIs yet, and raw notification/request escape hatches still preserve unrecognized shapes.

SDK impact:

- No handwritten wrapper was added in this pass.
- Generated DTOs are current for internal use.

### 5. Exec/runtime changes did not require SDK changes

The upstream delta includes TUI task mentions, permission-mode shortcuts, interrupt hooks, shell snapshot/runtime hardening, remote MCP bearer token lookup, and shutdown fixes. Existing SDK exec process-launch, JSONL parsing, and app-server raw forwarding surfaces did not need changes for those upstream internals.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~McpServerWrappersTests|FullyQualifiedName~ApprovalHandlersTests|FullyQualifiedName~SourceFileSizeGuardTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.149.1 -> 0.150.0` window. Dedicated wrappers for the new MCP event stream and experimental realtime timeline items can be considered separately if those APIs become part of the SDK's supported handwritten surface.

## Upstream Sources

- GitHub release `rust-v0.150.0`
- `external/codex` local tags `rust-v0.149.1` and `rust-v0.150.0`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/ConfigRequirements.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/BrowserUseRequirements.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/ComputerUseRequirements.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/McpServerStatus.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/CommandExecutionRequestApprovalParams.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/McpServerEventStreamNotification.ts`
- `external/codex/codex-rs/app-server-protocol/schema/typescript/v2/ThreadRealtimeItem.ts`
