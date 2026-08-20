# Codex 0.148.0 -> 0.149.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.149.0`.
- Verified `external/codex` is pinned to `rust-v0.149.0` and matches the `rust-v0.149.0` tag commit.
- Audited the local upstream delta from `rust-v0.148.0` to `rust-v0.149.0`, focusing on app-server protocol changes, generated DTO drift, and existing handwritten SDK wrappers.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.149.0`.
- Handwritten SDK parity changes were required for existing app-server thread, MCP resource, config-requirements, and notification contracts.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.149.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Thread payloads and filters gained project assignment fields

Upstream added experimental project support. The new project RPC family is experimental and remains unexposed as a first-class SDK feature in this pass, but existing thread responses now include `projectId`, and existing `thread/start`, `thread/list`, and `thread/metadata/update` methods gained project assignment/filter fields.

SDK impact:

- Added `CodexThreadSummary.ProjectId` parsing.
- Added experimental `ThreadStartOptions.ProjectId`.
- Added experimental project filters to `ThreadListOptions`, including the explicit null filter for unassigned threads.
- Added experimental project assignment/clear patching to `ThreadMetadataUpdateOptions`.
- Kept project-specific options behind existing experimental API guards.

### 2. MCP resource reads gained app resource origin/scoping fields

Upstream changed `mcpResource/read` so `threadId` is optional, added `originCallId` and `connectorId` request fields, and returns `originCallId` when app-specific resource scoping is applied.

SDK impact:

- Relaxed `McpResourceReadOptions.ThreadId` to optional, matching upstream's threadless read path.
- Added `OriginCallId` and `ConnectorId` request fields.
- Preserved upstream validation that `OriginCallId` requires `ThreadId`.
- Added `McpResourceReadResult.OriginCallId`.

### 3. Config requirements gained managed auth endpoint fields

Upstream added `cliAuthCredentialsStore` and `chatgptBaseUrl` to `configRequirements/read`.

SDK impact:

- Added open value-object support for `CliAuthCredentialsStoreMode`.
- Added typed `ConfigRequirements.CliAuthCredentialsStore` and `ConfigRequirements.ChatGptBaseUrl`.
- Preserved raw payloads for forward compatibility.

### 4. New notification methods became stable wire contracts

Upstream added `project/changed`, `thread/project/updated`, and `autoApprovalReview/strictReviewRequired`.

SDK impact:

- Added typed notification records for all three methods.
- Added mapper coverage with required-field validation and `UnknownNotification` fallback for malformed payloads.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ThreadSummaryParsingTests|FullyQualifiedName~ThreadStartParamsSerializationTests|FullyQualifiedName~ThreadListParamsSerializationTests|FullyQualifiedName~AppServerCommandAndFilesystemTests|FullyQualifiedName~McpServerWrappersTests|FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ExperimentalApiGuardsTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

The new upstream experimental project RPC family (`project/list`, `project/read`, `project/create`, `project/import`, `project/update`, `project/move`, and `project/delete`) is not exposed as a public SDK abstraction in this pass. No remaining actionable drift was identified for existing SDK app-server wrappers in the `0.148.0 -> 0.149.0` window.

## Upstream Sources

- `external/codex` local tags `rust-v0.148.0` and `rust-v0.149.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/project.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/notification.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/projects.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/config_rpc.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/guardian_v2.rs`
