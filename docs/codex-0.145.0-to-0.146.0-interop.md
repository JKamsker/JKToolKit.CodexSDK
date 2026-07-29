# Codex 0.145.0 -> 0.146.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.146.0`.
- Verified `external/codex` is pinned to `rust-v0.146.0` and matches the `rust-v0.146.0` tag commit.
- Audited the local upstream delta from `rust-v0.145.0` to `rust-v0.146.0`, focusing on app-server protocol/schema, generated DTOs, handwritten app-server wrappers, notification and thread-read projections, and exec/session behavior.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.146.0`.
- Handwritten SDK parity changes were required for newly exposed app-server metadata and request fields.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.146.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Thread pinning is now part of app-server thread metadata

Upstream added `Thread.isPinned`, a `thread/list` `isPinned` filter, and `thread/metadata/update` `isPinned` patch support.

SDK impact:

- Added `ThreadListOptions.IsPinned` and `Protocol.V2.ThreadListParams.IsPinned`.
- Added `CodexThreadSummary.IsPinned` parsing.
- Extended `ThreadMetadataUpdateOptions` so callers can send pin-only metadata updates or combine pin and Git metadata patches.
- Added regression coverage for serialization, request construction, and response parsing.

### 2. Plugin and app metadata expanded

Upstream added `plugin/list.forceRefetch`, workspace-publish capability on plugin share payloads, remote icon URLs on skill interfaces, and app tool enablement/read-only fields.

SDK impact:

- Added `PluginListOptions.ForceRefetch` and request wiring while preserving the older `ForceRemoteSync` source-compatible no-op.
- Added `CanPublishToWorkspace` on plugin share context and save results.
- Added remote skill icon URL parsing.
- Added `AppToolSummaryDescriptor.IsEnabled`, `DisabledReason`, and `IsReadOnly`.

### 3. Config requirements added more policy fields

Upstream expanded `configRequirements/read` with browser-use, feedback, update, login-shell, Windows sandbox private desktop, and path URI requirements.

SDK impact:

- Added typed `ConfigRequirements` properties for the new scalar and path fields.
- Added `BrowserUseRequirements` and `FeedbackRequirements` typed projections.
- Existing raw preservation still retains unknown and experimental fields.

### 4. External agent detection gained limit/source selectors

Upstream added `maxSessionAgeDays`, `maxSessions`, and `migrationSource` to `externalAgentConfig/detect`.

SDK impact:

- Added the corresponding fields to `ExternalAgentConfigDetectOptions`.
- The upstream import-side `providerId` and history-record method are noted as future wrapper surface expansion; no current SDK caller contract broke because imports still preserve the existing required migration item path.

### 5. Thread-read command execution items carry plugin attribution

Upstream added optional `pluginId` and `scriptPath` on `ThreadItem.CommandExecution`.

SDK impact:

- Added optional typed properties to `CodexThreadItemCommandExecution`.
- Added parser and fixture coverage while preserving the existing constructor shape.

### 6. Exec/runtime changes remain upstream-owned

The upstream delta includes proxy routing, code-mode WebSockets, Windows sandboxing, MCP runtime refresh behavior, side conversations, paginated forks, and TUI changes.

SDK impact:

- Exec mode delegates these runtime behaviors to the vendored Codex CLI.
- The only direct exec-facing delta inspected was resume thread listing, where upstream now sends `isPinned: null`; the SDK's app-server thread listing wrapper now exposes the same filter field.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ThreadListParamsSerializationTests|FullyQualifiedName~ThreadApiParsingTests|FullyQualifiedName~AppServerCommandAndFilesystemTests|FullyQualifiedName~PluginClientTests|FullyQualifiedName~CodexAppServerSkillsAppsClientTests|FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~ExternalAgentConfigWrappersTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining CI-blocking SDK drift was identified for the `0.145.0 -> 0.146.0` window. Future wrapper work can add first-class methods for `externalAgentConfig/import/recordHistory` and import attribution fields if consumers need that workflow.

## Upstream Sources

- GitHub release `rust-v0.146.0`
- `external/codex` local tags `rust-v0.145.0` and `rust-v0.146.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/apps.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/item.rs`
- `external/codex/codex-rs/exec/src/lib.rs`
