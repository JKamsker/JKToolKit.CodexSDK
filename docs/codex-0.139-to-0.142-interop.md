# Codex 0.139.0 -> 0.142.3 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` had `api` at `0.142.3` and `integration` at `0.139.0`.
- Verified `external/codex` is checked out at `rust-v0.142.3` and matches the `api` marker commit.
- Reviewed the local upstream source delta from `rust-v0.139.0` to `rust-v0.142.3`, focusing on app-server protocol changes, generated DTO drift, and SDK wrapper/parser parity.
- `git -C external/codex fetch --tags --force` could not complete in this environment because outbound GitHub access was blocked by the proxy, but the needed local tags were already present.

## Update Status

- `external/codex` is pinned to `rust-v0.142.3`.
- `UPSTREAM_CODEX_VERSION.json` now has `integration` set to `0.142.3` after the handwritten parity pass.
- Generated upstream DTOs already reflected the `0.142.3` schema additions in this checkout.

## Confirmed Upstream Changes That Mattered

### 1. Account and workspace APIs expanded

Upstream added:

- `account/rateLimitResetCredit/consume`
- `account/workspaceMessages/read`
- nullable ChatGPT account emails
- Amazon Bedrock account payloads
- `bedrockApiKey` auth mode

SDK fix:

- Added `ConsumeAccountRateLimitResetCreditAsync`.
- Added `ReadWorkspaceMessagesAsync`.
- Allowed `CodexChatGptAccountInfo.Email` to be null.
- Added `CodexAmazonBedrockAccountInfo`.
- Added `CodexAuthMode.BedrockApiKey`.

### 2. External-agent import history and progress surfaced

Upstream added `externalAgentConfig/import/readHistories` and `externalAgentConfig/import/progress`.

SDK fix:

- Added `ReadExternalAgentConfigImportHistoriesAsync`.
- Added typed progress and completed import notifications while preserving raw item-type result payloads.

### 3. Thread management added deletion and parent/recency filters

Upstream added `thread/delete`, `thread/deleted`, `thread/list.parentThreadId`, and `recencyAt` sorting.

SDK fix:

- Added `DeleteThreadAsync`.
- Added `ThreadListOptions.ParentThreadId`.
- Preserved string-based `SortKey` support for new upstream values such as `recencyAt`.
- Added `ThreadDeletedNotification`.

### 4. Model safety buffering notifications are emitted

Upstream emits `model/safetyBuffering/updated`.

SDK fix:

- Added `ModelSafetyBufferingUpdatedNotification`.
- Mapped the notification to a typed SDK record while preserving raw array payloads.

## Audited Changes That Required No SDK Code

- Generated DTO additions for the same upstream window were already present in `src/JKToolKit.CodexSDK/Generated/Upstream/AppServer/V2`.
- Larger upstream changes around remote environments, path URI internals, dynamic tools, code mode, plugin catalog rendering, and TUI behavior do not map to currently exposed handwritten SDK surfaces in this repository.
- Existing SDK extensible string value objects continue to cover new model/reasoning enum values.

## Validation

- Focused coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~AuthAccountConfigWrappersTests|FullyQualifiedName~AppServerThreadManagementClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
  - Result: passed.
- Full suite:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release --no-restore`
  - Result: passed.
- Initial generator check without `--no-restore` could not run because NuGet restore was blocked by the environment proxy.
- Retried generator check with `--no-restore`, but `dotnet run` still attempted package restore and failed on the same blocked proxy.

## Remaining Drift

No actionable generated schema drift was identified for the `0.139.0 -> 0.142.3` window in this checkout.

Pre-existing app-server wrapper backlog remains for stable RPCs that were already present before this window, including `modelProvider/capabilities/read` and `account/sendAddCreditsNudgeEmail`.

## Upstream Sources

- `external/codex` tag: `rust-v0.139.0`
- `external/codex` tag: `rust-v0.142.3`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server/src/request_processors/account_processor.rs`
- `external/codex/codex-rs/app-server/src/request_processors/thread_delete.rs`
- `external/codex/codex-rs/app-server/src/bespoke_event_handling.rs`
