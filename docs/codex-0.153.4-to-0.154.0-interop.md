# Codex 0.153.4 -> 0.154.0 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.154.0`.
- Verified `external/codex` is pinned to `rust-v0.154.0` and matches the `rust-v0.154.0` tag commit.
- Audited the local upstream delta from `rust-v0.153.4` to `rust-v0.154.0`, focusing on app-server protocol/schema drift, generated DTO output, config requirements, account rate limits, MCP server status, and thread-list/read projections.

## Update Status

- Generated upstream schema/DTO output is up to date for `0.154.0`.
- Handwritten SDK projections were updated for stable app-server fields introduced by this version window.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.154.0` after this parity pass.

## Confirmed Upstream Changes

### 1. Account rate-limit reads gained client capability params and response fields

Upstream changed `account/rateLimits/read` params from omitted-only to optional `supportsLunaReserve` and `excludeResetCreditDetails` capability flags. The response now includes `ordinaryUsageAllowed`, and `RateLimitSnapshot` includes `normalModelSlug`.

SDK impact:

- Added `AccountRateLimitsReadOptions` and an overload for `ReadAccountRateLimitsAsync(...)` while preserving the existing no-arg call that sends omitted params.
- Projected top-level `ordinaryUsageAllowed` onto `AccountRateLimitsReadResult`.
- Preserved `normalModelSlug` through the existing raw rate-limit snapshot JSON and covered it in tests.

### 2. MCP server status reports tool-catalog discovery failures

Upstream added `toolsError` to `McpServerStatus` to distinguish a failed tool discovery from a returned empty or cached catalog.

SDK impact:

- Added `McpServerStatusInfo.ToolsError`.
- Updated MCP status parsing to read both camelCase and snake_case spellings for forward/backward tolerance.

### 3. Thread list and thread payloads expose originators

Upstream added `originator` to `Thread` and `originators` to `ThreadListParams`. Hosted backends can filter by exact originator; local app-server rejects nonempty lists.

SDK impact:

- Added `CodexThreadSummary.Originator` parsing.
- Added `ThreadListOptions.Originators` and v2 wire `ThreadListParams.Originators`.

### 4. Config requirements gained application network policy and WebMCP browser policy

Upstream added experimental `configRequirements/read.application` carrying managed application network requirements, and added `browserUse.allowWebmcp`.

SDK impact:

- Added typed `ApplicationRequirements` and `ApplicationNetworkRequirements` projections gated with the existing experimental config-requirements behavior.
- Added `BrowserUseRequirements.AllowWebMcp`.
- Raw config requirement payload preservation remains unchanged.

### 5. Other upstream changes did not require SDK changes

The version window includes substantial TUI, Guardian, managed worktree, voice, Windows sandbox, and app-server daemon work. These changes either do not affect existing stable SDK surfaces or are represented only through generated internal DTOs and raw JSON preservation today.

SDK impact:

- No exec resume, structured-output, JSONL, or process-start behavior drift requiring SDK changes was identified.
- Experimental user-verification app-server contracts were generated, but no stable public SDK wrapper was added in this pass.

## Validation

Validation run during this pass:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "AuthAccountConfigWrappersTests|AccountTokenUsageWrappersTests|McpServerWrappersTests|ThreadApiParsingTests|ThreadListParamsSerializationTests|ConfigRequirementsParsingTests|ConfigRequirementsReadWrapperTests|ResilientCodexAppServerClientTests"`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable drift was identified for existing stable SDK surfaces in the `0.153.4 -> 0.154.0` window.

## Upstream Sources

- GitHub release tag `openai/codex` `rust-v0.154.0`
- Local upstream tags `rust-v0.153.4` and `rust-v0.154.0`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/account.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/application.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/config.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/mcp.rs`
- `external/codex/codex-rs/app-server-protocol/src/protocol/v2/thread.rs`
- `external/codex/codex-rs/app-server/src/request_processors/config_processor.rs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerConfigClient.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientMcpParsers.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientThreadParsers.cs`
