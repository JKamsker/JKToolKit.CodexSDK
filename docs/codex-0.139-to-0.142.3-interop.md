# Codex 0.139.0 -> 0.142.3 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.142.3` and `external/codex` is pinned to `rust-v0.142.3`.
- Reviewed local upstream source and schema deltas from `rust-v0.139.0` to `rust-v0.142.3`.
- Focused on stable app-server wrappers, typed notification mapping, generated DTO drift, and high-risk exec/path behavior.
- `git -C external/codex fetch --tags --force` was blocked by the workflow network boundary, but the required local tags were already present and resolved.

## Update Status

- `external/codex` is pinned to `rust-v0.142.3`.
- `UPSTREAM_CODEX_VERSION.json` `api` is `0.142.3`.
- Generated upstream schema/DTO output is up to date.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.142.3` after this parity pass.

## Implementation Tasklist

- [x] Add stable `thread/delete` wrapper on direct and resilient app-server clients.
- [x] Add stable `account/rateLimitResetCredit/consume` wrapper and typed outcome parsing.
- [x] Add stable `account/workspaceMessages/read` wrapper and typed workspace-message projections.
- [x] Surface `rateLimitResetCredits.availableCount` from `account/rateLimits/read`.
- [x] Map `thread/deleted` and `model/safetyBuffering/updated` notifications to typed records.
- [x] Audit generated DTO output and keep public SDK projections handwritten where generated unions remain lossy.

## Confirmed Upstream Changes That Mattered

### 1. Thread deletion is now a stable RPC

Upstream added `thread/delete` and emits `thread/deleted` with `{ threadId }`.

SDK fix:

- Added `DeleteThreadAsync` on direct and resilient clients.
- Added `ThreadDeleteResult`.
- Added `ThreadDeletedNotification` mapping.

### 2. Rate-limit reset credits are exposed and consumable

Upstream added `rateLimitResetCredits` to `account/rateLimits/read` and added `account/rateLimitResetCredit/consume`.

SDK fix:

- Added `RateLimitResetCreditsSummary` on `AccountRateLimitsReadResult`.
- Added `ConsumeAccountRateLimitResetCreditAsync`.
- Added typed consume outcomes while preserving the raw outcome string and payload.

### 3. Workspace messages are now readable

Upstream added `account/workspaceMessages/read`, returning `featureEnabled` and active workspace messages.

SDK fix:

- Added `ReadWorkspaceMessagesAsync`.
- Added typed `WorkspaceMessagesReadResult`, `WorkspaceMessageInfo`, and `WorkspaceMessageKind` projections.

### 4. Safety-buffering notifications are surfaced to clients

Upstream added `model/safetyBuffering/updated` with model, use-case, reason, UI, and faster-model metadata.

SDK fix:

- Added `ModelSafetyBufferingUpdatedNotification`.
- Preserved list fields and nullable `fasterModel`.

## Audited Changes That Required No SDK Code

- Generated upstream schema/DTO artifacts were already current for `0.142.3`.
- The generated reset-credit DTO still contains a placeholder enum for the consume outcome; the public SDK wrapper therefore parses the raw JSON directly instead of exposing that generated placeholder.
- Broad upstream exec-server, PathUri, plugin, and TUI changes did not show confirmed drift in the SDK wrapper surfaces touched by this pass.

## Validation

- Focused coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AccountTokenUsageWrappersTests|FullyQualifiedName~AppServerThreadManagementClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
  - Result: `48` passed
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: generated output is up to date
- Full suite:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `788` passed, `15` skipped

## Remaining Drift

No new generated schema drift remains for the `0.139.0 -> 0.142.3` window.

Pre-existing backlog remains for stable RPCs that were already noted before this pass, including `modelProvider/capabilities/read` and `account/sendAddCreditsNudgeEmail`.

## Upstream Sources

- `external/codex` local tags `rust-v0.139.0` and `rust-v0.142.3`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/model.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server/README.md`
