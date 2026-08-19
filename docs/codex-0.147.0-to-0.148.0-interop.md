# Codex 0.147.0 -> 0.148.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.148.0`.
- Verified `external/codex` is pinned to `rust-v0.148.0` and matches the `rust-v0.148.0` tag commit.
- Audited the local upstream delta from `rust-v0.147.0` to `rust-v0.148.0`, focusing on app-server protocol changes, generated DTO drift, and existing handwritten SDK wrappers.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.148.0`.
- Handwritten SDK parity changes were required for app reads, account usage, MCP status/OAuth login, config requirements, model list metadata, plugin install correlation, thread section appearance, and new thread notifications.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.148.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Existing app-server wrappers gained new request or response fields

Upstream added thread-scoped `app/read`, thread-specific `account/usage/read` params plus `threadUsage`, MCP `pluginId` status metadata, per-login MCP OAuth client registration, plugin install `installAttemptId`, model `multiAgentVersion`, model upgrade `retirementAt`, and automatic review config requirements.

SDK impact:

- Added public options/result fields and parsers for those existing wrappers.
- Kept raw JSON payloads preserved for forward compatibility.
- Added focused tests for each changed wire contract.

### 2. Thread sections gained synchronized appearance metadata

Upstream added optional `appearance` on thread sections and create/update params. Update distinguishes omitted appearance from explicit `null` to clear it.

SDK impact:

- Added typed section appearance descriptors and create/update options.
- Used dictionary request builders for correct omit-versus-clear behavior.

### 3. Thread queue and revert notifications were added

Upstream added `thread/queue/changed` and `thread/reverted` notifications alongside new queue and revert RPC surfaces.

SDK impact:

- Added typed notification records and mapper coverage for both events.
- The new queue RPC family was not exposed as a public SDK abstraction in this pass; no pre-existing SDK method drift was present.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AccountTokenUsageWrappersTests|FullyQualifiedName~CodexAppServerSkillsAppsClientTests|FullyQualifiedName~McpServerWrappersTests|FullyQualifiedName~AuthAccountConfigWrappersTests|FullyQualifiedName~AppServerCommandAndFilesystemTests|FullyQualifiedName~PluginClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for existing SDK app-server wrappers in the `0.147.0 -> 0.148.0` window. New upstream-only queue and revert RPCs remain candidates for a future feature pass if the SDK needs first-class queue management.

## Upstream Sources

- `external/codex` local tags `rust-v0.147.0` and `rust-v0.148.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/apps.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/model.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
