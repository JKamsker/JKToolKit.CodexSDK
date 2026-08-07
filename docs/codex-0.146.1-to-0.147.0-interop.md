# Codex 0.146.1 -> 0.147.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.147.0`.
- Verified `external/codex` is pinned to `rust-v0.147.0` and matches the `rust-v0.147.0` tag commit.
- Audited the local upstream delta from `rust-v0.146.1` to `rust-v0.147.0`, focusing on app-server protocol changes, generated DTO drift, plugin search, thread sections, and removed pinned-thread request fields.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.147.0`.
- Handwritten SDK parity changes were required for app-server thread sections, `plugin/search`, plugin metadata, and removed pinned-thread write/filter fields.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.147.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Thread pinning moved to persisted thread sections

Upstream removed `isPinned` from `thread/list` filters and `thread/metadata/update` params. Threads now carry optional `section` and `sectionEnteredAt` metadata, `thread/list` accepts a tri-state `sectionId` filter, and section CRUD is exposed through `threadSection/*`. Moving membership uses `thread/section/move`, including explicit `sectionId: null` to remove a thread from its section.

SDK impact:

- Added typed thread section descriptors, list/create/update/delete wrappers, and move wrapper.
- Added `ThreadListOptions.SectionId` and `ThreadListOptions.UnsectionedOnly`.
- Stopped sending removed `isPinned` fields while keeping obsolete source-compatible properties.
- Projected `CodexThreadSummary.Section` and `SectionEnteredAt`.

### 2. Plugin search became an app-server method

Upstream added experimental `plugin/search` with `searchTerm`, optional `scope`, optional `cwds`, cursor, and limit. Results contain a plugin summary plus marketplace discovery metadata.

SDK impact:

- Added `SearchPluginsAsync`, `PluginSearchOptions`, `PluginSearchScope`, `PluginSearchPage`, and typed result projection.
- Wired plugin search through the resilient app-server client.

### 3. Plugin summaries expose more remote catalog metadata

Upstream added optional `installedAt`, `disabledReason`, and `eligiblePlanTypes` fields to plugin summaries.

SDK impact:

- Added typed projection for install timestamps, disabled reasons, and eligible plan identifiers across plugin list/read/share/search parsing.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~PluginClientTests|FullyQualifiedName~AppServerCommandAndFilesystemTests|FullyQualifiedName~ThreadApiParsingTests|FullyQualifiedName~ThreadListParamsSerializationTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the audited app-server thread section, plugin search, and plugin metadata changes in the `0.146.1 -> 0.147.0` window.

## Upstream Sources

- `external/codex` local tags `rust-v0.146.1` and `rust-v0.147.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread_data.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin_search.rs`
- `external/codex/codex-rs/app-server/src/request_processors/thread_sections.rs`
- `external/codex/codex-rs/app-server/src/request_processors/plugins/search.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/thread_sections.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/plugin_search.rs`
