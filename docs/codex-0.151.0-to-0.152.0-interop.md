# Codex 0.151.0 -> 0.152.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.152.0`.
- Verified `external/codex` is pinned to `rust-v0.152.0` and matches the `rust-v0.152.0` tag commit.
- Audited the local upstream delta from `rust-v0.151.0` to `rust-v0.152.0`, focusing on app-server schema/protocol changes, generated DTO drift, shell-command execution, account rate-limit reads, auth recovery notifications, MCP configuration, and project-list changes.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.152.0`.
- Handwritten SDK app-server parity was updated for confirmed stable drift.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.152.0` after this parity pass.

## Confirmed Upstream Changes

### 1. `thread/shellCommand` accepts an optional timeout

Upstream added `timeoutMs` to `thread/shellCommand`. Omitted or null values use the server default timeout; non-negative integers set the execution timeout, and zero requests an immediate timeout. Negative values are rejected before execution.

SDK impact:

- Added `ThreadShellCommandOptions.TimeoutMs`.
- Serialized `timeoutMs` when callers set it, preserving prior omission behavior otherwise.
- Added client-side validation for negative timeouts.

### 2. Model-provider auth recovery emits app-server notifications

Upstream now forwards authentication recovery progress as `modelProvider/authRecoveryStarted` and `modelProvider/authRecoveryCompleted` notifications with `threadId`, `turnId`, `provider`, and `message`.

SDK impact:

- Added `ModelProviderAuthRecoveryNotification`.
- Mapped both auth-recovery notification methods to the typed notification.
- Added malformed-payload coverage so incomplete notifications still fall back to `UnknownNotification`.

### 3. Account rate-limit reads include snapshot identity and upsell payload

Upstream added `accountId` and `rateLimitUpsell` to `account/rateLimits/read`. The banner remains backend-owned raw JSON and may be absent.

SDK impact:

- Added `AccountRateLimitsReadResult.AccountId`.
- Added `AccountRateLimitsReadResult.RateLimitUpsell` while preserving the full raw response.
- Updated wrapper tests to verify both new projections.

### 4. MCP per-tool config includes output token limits

Upstream extended per-tool MCP configuration with `output_token_limit`, a positive token budget for individual tool output.

SDK impact:

- Added `CodexConfigOverridesBuilder.SetMcpServerTool(...)` for per-tool `approval_mode` and `output_token_limit` dotted overrides.
- Added validation that explicit output token limits must be positive.

### 5. Project DTOs now carry recency metadata and sort options

Upstream added `Project.recencyAt` plus `project/list` `sortKey` and `sortDirection` params. These surfaces are currently represented by generated internal DTOs in this SDK rather than handwritten public project-list wrappers.

SDK impact:

- Generated DTOs already include `Project.RecencyAt`, `ProjectSortKey`, and the updated schema.
- No handwritten SDK change was required in this pass.

### 6. Additional upstream changes did not require handwritten SDK changes

Upstream also changed Guardian/review handling, TUI model/rate-limit banners, Vim composer search, default `update_plan` configuration, cloud-task credential routing, terminal query handling, cached MCP tool refreshes, thread resume cwd restoration, code-mode internals, and app-server notification media filtering.

SDK impact:

- Existing exec resume/session-discovery code was not touched by this upstream window.
- Generated DTOs cover the new app-server schema artifacts.
- Existing raw notification and raw response payloads preserve unwrapped experimental or UI-only fields.

## Validation

Validation run during this pass:

- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AppServerCommandAndFilesystemTests|FullyQualifiedName~AuthAccountConfigWrappersTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~McpServerWrappersTests"`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.151.0 -> 0.152.0` window.

## Upstream Sources

- Local upstream tags `rust-v0.151.0` and `rust-v0.152.0`
- GitHub release `openai/codex` `rust-v0.152.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/notification.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/project.rs`
- `external/codex/codex-rs/config/src/mcp_types.rs`
- `external/codex/codex-rs/app-server/src/request_processors/thread_processor.rs`
- `external/codex/codex-rs/app-server/src/bespoke_event_handling.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/thread_shell_command.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/rate_limits.rs`
- `external/codex/codex-rs/app-server/tests/suite/v2/projects.rs`
