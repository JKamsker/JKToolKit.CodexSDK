# Codex 0.139.0 -> 0.142.3 Interop Research

## Scope

- Audited the vendored upstream delta from `rust-v0.139.0` to `rust-v0.142.3`.
- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.142.3` and `external/codex` is pinned to `rust-v0.142.3`.
- Focused on handwritten SDK drift around app-server wrappers, typed notification projection, account parsing, initialize capabilities, and execution-environment path contracts.

## Confirmed Upstream Changes That Mattered

### 1. Account and workspace APIs expanded

Upstream added:

- `account/rateLimitResetCredit/consume`
- `account/workspaceMessages/read`
- `rateLimitResetCredits` on `account/rateLimits/read`

SDK fix:

- Added reset-credit consume options/result and workspace-message read models.
- Preserved raw JSON for all new projections.
- Parsed `rateLimitResetCredits.availableCount` from rate-limit snapshots.

### 2. Account shapes changed

Upstream now allows ChatGPT accounts without email and reports Bedrock credential source. Auth mode also gained `bedrockApiKey`.

SDK fix:

- Made `CodexChatGptAccountInfo.Email` nullable.
- Added `CodexAmazonBedrockAccountInfo`.
- Added `CodexAuthMode.BedrockApiKey`.

### 3. Thread management APIs expanded

Upstream added:

- `thread/delete`
- `thread/backgroundTerminals/list`
- `thread/backgroundTerminals/terminate`
- `thread/list.parentThreadId`

SDK fix:

- Added direct and resilient wrappers for deletion and background terminal list/terminate.
- Added `ThreadListOptions.ParentThreadId`.
- Kept background terminal list/terminate behind experimental API gating.

### 4. App-server notifications expanded

Upstream added or enriched:

- `thread/deleted`
- `model/safetyBuffering/updated`
- `externalAgentConfig/import/progress`
- `externalAgentConfig/import/completed`
- `account/rateLimits/updated.rateLimitResetCredits`

SDK fix:

- Added typed notifications for the new methods.
- Preserved raw item-type result payloads for external-agent import notifications.
- Preserved optional reset-credit payloads on account rate-limit updates.

### 5. MCP elicitation and initialize capabilities expanded

Upstream added OpenAI form MCP elicitations and an initialize capability flag.

SDK fix:

- Added `InitializeCapabilities.McpServerOpenAiFormElicitation`.
- Added `McpServerElicitationMode.OpenAiForm`.

### 6. Environment cwd path contract loosened

Upstream changed `TurnEnvironmentParams.cwd` to a legacy/app-native path string instead of an absolute path buffer.

SDK fix:

- Stopped requiring `TurnEnvironmentOptions.Cwd` to be absolute.
- Kept `runtimeWorkspaceRoots` absolute-only.

## Audited Changes That Required No SDK Code

- Deprecated multi-agent mode fields are now ignored upstream in favor of reasoning effort, so no new public SDK mode abstraction was added.
- Generated upstream schema/DTO artifacts were already up to date for `0.142.3`.
- Remote executor Noise transport, PathUri internals, token-budget internals, and TUI/plugin UI changes do not change the current handwritten SDK surface directly.
- `rust-v0.142.3` release notes identify the patch as maintenance-only after `0.142.2`.

## Validation

- Focused tests:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~AuthAccountConfigWrappersTests|FullyQualifiedName~AppServerThreadManagementClientTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~InitializeCapabilitiesTests|FullyQualifiedName~RuntimeWorkspaceRootValidationTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
  - Result: `90` passed.
- Generator check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: generated output is up to date.
- Full suite:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `790` passed, `15` skipped.

## Remaining Drift

No confirmed actionable handwritten SDK drift remains for the `0.139.0 -> 0.142.3` window after this pass.

Pre-existing wrapper backlog called out in the `0.137.0 -> 0.139.0` note remains outside this delta, including stable RPCs that predated `0.139.0`.

## Upstream Sources

- `0.140.0`: <https://github.com/openai/codex/releases/tag/rust-v0.140.0>
- `0.141.0`: <https://github.com/openai/codex/releases/tag/rust-v0.141.0>
- `0.142.0`: <https://github.com/openai/codex/releases/tag/rust-v0.142.0>
- `0.142.3`: <https://github.com/openai/codex/releases/tag/rust-v0.142.3>
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/turn.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/current_time.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs`
