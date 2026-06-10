# Codex 0.137.0 -> 0.139.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.139.0` is the latest stable release as of 2026-06-10. `rust-v0.140.0-alpha.*` releases are prereleases and were not used as the parity target.
- Reviewed release notes and source/schema deltas in `external/codex` from `rust-v0.137.0` to `rust-v0.139.0`.
- Focused on handwritten SDK app-server wrappers/parsers, exec-mode argv behavior, generated schema interop, and tests.

## Update Status

- `external/codex` is pinned to `rust-v0.139.0`.
- `UPSTREAM_CODEX_VERSION.txt` is pinned to `0.139.0`.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check` passed.

## Implementation Tasklist

- [x] Add stable `account/usage/read` app-server wrapper and resilient-client parity.
- [x] Add experimental `remoteControl/pairing/status` wrapper and validation.
- [x] Add `turn/moderationMetadata` typed notification.
- [x] Surface optional `threadId` on `mcpServer/startupStatus/updated`.
- [x] Parse `ConfigRequirements.allowedPermissionProfiles` and `defaultPermissions`.
- [x] Parse `PluginDetail.appTemplates`.
- [x] Treat `runtimeWorkspaceRoots` as absolute paths on thread start/resume/fork and turn start.
- [x] Add `personalAccessToken` auth mode parsing.
- [x] Audit model-defined reasoning efforts, MCP tool schema preservation, and CLI resume prompt behavior.

## Confirmed Upstream Changes That Mattered

### 1. Account token usage is now a stable RPC

Upstream added `account/usage/read`, returning a token-usage summary and optional daily buckets.

SDK fix:

- Added `ReadAccountTokenUsageAsync` on direct and resilient app-server clients.
- Added typed `AccountTokenUsageReadResult`, `AccountTokenUsageSummary`, and `AccountTokenUsageDailyBucket` projections while preserving the raw payload.

### 2. Remote-control pairing status is now an experimental RPC

Upstream added `remoteControl/pairing/status`, which accepts exactly one of `pairingCode` or `manualPairingCode` and returns `claimed`.

SDK fix:

- Added `ReadRemoteControlPairingStatusAsync` on direct and resilient clients.
- Added options/result models.
- Validated the one-of selector client-side before sending JSON-RPC.

### 3. Notification payloads expanded

Upstream added `turn/moderationMetadata` and scoped MCP startup status updates with an optional `threadId`.

SDK fix:

- Added `TurnModerationMetadataNotification`.
- Added `McpServerStartupStatusUpdatedNotification.ThreadId`.
- Extended mapper tests for both payloads.

### 4. Config requirements switched permission-profile shape

Upstream replaced the legacy `allowedPermissions` array with `allowedPermissionProfiles` and added `defaultPermissions`.

SDK fix:

- Parsed `AllowedPermissionProfiles` as a string-to-bool map.
- Kept `AllowedPermissionProfileIds` as a convenience projection.
- Preserved legacy `allowedPermissions` fallback for older app-server builds.

### 5. Plugin details expose app templates

Upstream added required `appTemplates` on `PluginDetail`.

SDK fix:

- Added typed plugin app-template descriptors and unavailable-reason value object.
- Split plugin detail parsing into focused partial files to keep touched parser files under the local C# size guideline.
- Defaulted missing `appTemplates` to an empty list for compatibility with older app-server builds.

### 6. Runtime workspace roots are absolute paths

Upstream changed `runtimeWorkspaceRoots` from generic paths to absolute paths on thread start, thread resume, thread fork, and turn start.

SDK fix:

- Validated all four option surfaces with existing absolute-path guardrails before JSON-RPC is sent.
- Updated public option docs to state the absolute-path requirement.

### 7. Auth mode gained personal access tokens

Upstream added `personalAccessToken` to account auth-mode payloads.

SDK fix:

- Added `CodexAuthMode.PersonalAccessToken`.
- Covered account-updated notification parsing for the new value.

## Audited Changes That Required No SDK Code

- Model-defined reasoning effort values are already handled by SDK extensible value objects.
- MCP tool schema `oneOf` and `allOf` preservation is safe in the SDK path because MCP schemas are preserved as cloned JSON payloads.
- The upstream `resume --last "prompt"` fix is CLI argv parsing. The SDK already builds `codex exec resume --last` with the prompt as a separate argument.

## Validation

- Focused coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~ConfigRequirementsParsingTests|FullyQualifiedName~RemoteControlEnvironmentClientTests|FullyQualifiedName~RemoteControlPairingStatusClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~AccountTokenUsageWrappersTests|FullyQualifiedName~PluginAppTemplateParsingTests|FullyQualifiedName~RuntimeWorkspaceRootValidationTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
  - Result: `60` passed, `0` warnings
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: generated output is up to date
- Full suite:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `784` passed, `0` warnings
- Manual runbooks were not run; the actionable changes were wrapper/parser-level and are covered by focused regression tests plus the full suite.

## Remaining Drift

No new generated schema drift remains for the 0.137.0 -> 0.139.0 window.

Pre-existing app-server wrapper backlog remains for stable RPCs that were already present at 0.137.0, including `modelProvider/capabilities/read` and `account/sendAddCreditsNudgeEmail`.

## Upstream Sources

- `0.138.0`: <https://github.com/openai/codex/releases/tag/rust-v0.138.0>
- `0.139.0`: <https://github.com/openai/codex/releases/tag/rust-v0.139.0>
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/plugin.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/remote_control.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/remote_control.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/plugin_read.rs`
- `external/codex/codex-rs/tui/src/app_server_session.rs`
