# Codex 0.134.0 -> 0.137.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.137.0` is the latest stable release as of 2026-06-08. `rust-v0.138.0-alpha.*` releases are prereleases and were not used as the parity target.
- Reviewed release notes for `rust-v0.135.0`, `rust-v0.136.0`, and `rust-v0.137.0`.
- Reviewed source/schema deltas in `external/codex` from `rust-v0.134.0` to `rust-v0.137.0`.
- Focused on handwritten SDK app-server wrappers/parsers, exec-mode session behavior, generated schema interop, and tests.

## Update Status

- `external/codex` is pinned to `rust-v0.137.0`.
- `UPSTREAM_CODEX_VERSION.txt` is pinned to `0.137.0`.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check` passed.

## Confirmed Upstream Changes That Mattered

### 1. Deprecated `persistExtendedHistory` request fields were removed

Upstream removed the legacy `persist_extended_history` / `persistFullHistory` wire fields from `thread/start`, `thread/resume`, and `thread/fork`.

SDK fix:

- Removed the wire fields from handwritten v2 params.
- Kept public `PersistExtendedHistory` compatibility options as ignored legacy switches so existing consumers still compile.
- Removed experimental guard failures for those compatibility switches.

### 2. Thread resume can request an initial turns page

Upstream added experimental `initialTurnsPage` request options and returns an `initialTurnsPage` page on `thread/resume`.

SDK fix:

- Added `ThreadResumeInitialTurnsPageOptions`, wire params, and guard coverage.
- Added `CodexTurnsPage` and lifecycle response parsing for `initialTurnsPage`.

### 3. Turn requests gained user message IDs and context metadata

Upstream added stable `clientUserMessageId` and experimental `responsesapiClientMetadata` / `additionalContext` for `turn/start` and `turn/steer`. Thread user-message items now include `clientId`.

SDK fix:

- Added request option/wire fields for start and steer.
- Added experimental guards for metadata/context fields.
- Parsed `CodexThreadItemUserMessage.ClientId`.

### 4. Thread and MCP metadata expanded

Upstream exposed `Thread.parentThreadId`, `mcpServerStatus/list.threadId`, and MCP `serverInfo`.

SDK fix:

- Parsed top-level and nested sub-agent parent thread ids into `CodexThreadSummary.ParentThreadId`.
- Added `ThreadId` to `McpServerStatusListOptions`.
- Parsed `McpServerImplementationInfo` from status results.

### 5. Skills extra roots moved to a dedicated RPC

Upstream added stable `skills/extraRoots/set`.

SDK fix:

- Added `SetSkillsExtraRootsAsync` on direct and resilient clients.
- Validated extra roots as absolute paths before sending the generated upstream params.

### 6. Remote-control management RPCs were added

Upstream added experimental `remoteControl/pairing/start`, `remoteControl/client/list`, and `remoteControl/client/revoke`.

SDK fix:

- Added direct and resilient wrappers.
- Added typed options/results for pairing and client grant management.
- Preserved raw payloads for forward compatibility.

### 7. Config and permission payloads expanded

Upstream added `environmentId` to permissions approval requests, `allowedWindowsSandboxImplementations` to config requirements, `enterpriseManaged` layer id/name fields, and changed Unix socket denial from `none` to `deny`.

SDK fix:

- Parsed and surfaced `PermissionsRequestApprovalParams.EnvironmentId`.
- Parsed Windows sandbox implementation allow-lists.
- Parsed enterprise-managed config layer `id` and `name`.
- Added `NetworkUnixSocketPermission.Deny` while keeping `None` as a legacy compatibility value.

### 8. Exec/session changes were audited

Upstream changed TUI resume list source filters to include `exec` and `appServer` sessions when non-interactive sessions are requested, added CLI archive/unarchive commands, fixed idle cached thread resume cwd overrides, and fixed Windows verbatim path comparison for running-thread resume.

SDK fix/audit result:

- Local SDK session listing already scans rollout JSONL files directly and does not exclude `codex exec` sessions by source kind.
- App-server thread listing already exposes `SourceKinds`; upstream default behavior remains interactive-only when source kinds are omitted.
- App-server archive/unarchive wrappers already existed, and archived resume/fork rejection is upstream server behavior.
- Tightened local `CodexSessionLocatorHelpers.NormalizedPathEquals` so Windows verbatim paths such as `\\?\C:\repo` match normal `C:\repo` paths.

## Validation

- Focused coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ThreadResumeParamsSerializationTests|FullyQualifiedName~TurnStartParamsSerializationTests|FullyQualifiedName~SteerAndReviewParamsTests|FullyQualifiedName~AppServerClientGuardrailSeamTests|FullyQualifiedName~ExperimentalApiGuardsTests|FullyQualifiedName~ThreadApiParsingTests|FullyQualifiedName~McpServerWrappersTests|FullyQualifiedName~CodexAppServerSkillsAppsClientTests|FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~ConfigRequirementsReadWrapperTests|FullyQualifiedName~ConfigReadWrapperTests|FullyQualifiedName~RemoteControlEnvironmentClientTests|FullyQualifiedName~ApprovalHandlersTests|FullyQualifiedName~ResilientCodexAppServerClientTests|FullyQualifiedName~CodexSessionLocatorHelpersTests|FullyQualifiedName~CodexResumeTargetResolverTests"`
  - Result: `147` passed
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: generated output is up to date

## Remaining Drift

No generated schema drift remains. The main remaining difference is intentional: the SDK exposes generic app-server `ThreadListOptions.SourceKinds` instead of baking in TUI-specific resume picker defaults for non-interactive sessions.

## Upstream Sources

- `0.135.0`: <https://github.com/openai/codex/releases/tag/rust-v0.135.0>
- `0.136.0`: <https://github.com/openai/codex/releases/tag/rust-v0.136.0>
- `0.137.0`: <https://github.com/openai/codex/releases/tag/rust-v0.137.0>
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/remote_control.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/thread_resume.rs`
- `external/codex/codex-rs/tui/src/lib.rs`
- `external/codex/codex-rs/tui/src/resume_picker.rs`
