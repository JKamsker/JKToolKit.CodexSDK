# Codex 0.118.0 -> 0.120.0 Interop Research

## Scope

- Verified upstream target with `gh`: `rust-v0.120.0` is the latest stable release as of `2026-04-11`.
- Reviewed the upstream delta from audited baseline `rust-v0.118.0` through `rust-v0.120.0`.
- Focused on stable or already-consumed SDK surfaces where the mechanical DTO regen was not sufficient on its own.

## Update Status

- `external/codex` is pinned to `rust-v0.120.0`.
- `UPSTREAM_CODEX_VERSION.txt` is pinned to `0.120.0`.
- App-server DTOs were already refreshed mechanically before this pass.
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check` passed.
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release` passed.

## Confirmed Upstream Changes That Mattered

### 1. `fs/watch` changed ownership of `watchId`

Upstream moved the connection-scoped `watchId` from the `fs/watch` response into the request payload. The SDK still expected the old response shape, which would break against `0.120.0`.

### 2. MCP management gained new stable request surface

Upstream added:

- `mcpServerStatus/list.detail`
- `mcpResource/read`

The generated DTOs reflected those additions, but the handwritten SDK wrappers and typed models did not.

### 3. Thread realtime became a richer request/notification surface

The `0.119.0` realtime work added:

- tri-state `prompt` semantics for `thread/realtime/start`
- optional realtime `transport`
- optional realtime `voice`
- `thread/realtime/sdp` notifications for WebRTC startup

The SDK still exposed only a prompt-string overload and dropped the new SDP notification entirely.

### 4. `thread/start` added `sessionStartSource`

Upstream now allows `sessionStartSource: "clear"` so session-start hooks can distinguish a replacement session after `/clear` from normal startup. The SDK had no public way to send it.

### 5. Guardian auto-approval review notifications changed shape

The unstable guardian notifications added or tightened:

- stable `reviewId`
- nullable `targetItemId`
- completed-notification `decisionSource`
- `userAuthorization`
- `critical` risk level

Without handwritten updates, valid upstream notifications could be flattened or rejected as unknown.

## SDK Changes Made

- Fixed `fs/watch` parity by sending a request-owned `watchId`, keeping the current public ergonomics by returning either the caller-supplied id or a generated one.
- Added `McpServerStatusDetail` and surfaced `detail` on `ListMcpServerStatusAsync`.
- Added typed `ReadMcpResourceAsync` wrappers and content models for `mcpResource/read`.
- Added `ThreadRealtimeStartOptions`, `ThreadRealtimeTransport`, prompt-mode support, optional voice support, and preserved the legacy realtime-start overload as a compatibility wrapper.
- Added typed `thread/realtime/sdp` notification mapping.
- Added `ThreadSessionStartSource` and surfaced `sessionStartSource` on `ThreadStartOptions`.
- Updated guardian review typing to preserve `reviewId`, nullable target items, decision source, user authorization, and `critical` risk.
- Kept the new realtime-start wiring in a dedicated helper so `CodexAppServerThreadsClient.cs` stayed below the repo's 500-line guard.

## Validation

- Focused unit coverage:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~McpServerWrappersTests|FullyQualifiedName~AppServerCommandAndFilesystemTests|FullyQualifiedName~AppServerClientGuardrailSeamTests|FullyQualifiedName~AppServerNotificationMapperTests|FullyQualifiedName~ThreadStartParamsSerializationTests|FullyQualifiedName~ResilientCodexAppServerClientTests"`
  - Result: `105` passed
- Size-guard regression check:
  - `dotnet test tests/JKToolKit.CodexSDK.Tests/JKToolKit.CodexSDK.Tests.csproj --configuration Release --filter "FullyQualifiedName~SourceFileSizeGuardTests"`
  - Result: `1` passed
- Generator drift check:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
  - Result: up-to-date
- Full validation:
  - `dotnet test JKToolKit.CodexSDK.sln --configuration Release`
  - Result: `680` passed, `14` skipped

## Remaining Drift

No additional actionable drift was identified in the audited `0.119.0/0.120.0` capability surfaces after these changes.

The broader long-tail parity wishlist from the `0.106.0 -> 0.118.0` note was not reopened unless the new upstream delta touched it directly.

## Upstream Sources

- `0.119.0`: <https://github.com/openai/codex/releases/tag/rust-v0.119.0>
- `0.120.0`: <https://github.com/openai/codex/releases/tag/rust-v0.120.0>
- `external/codex/codex-rs/app-server/README.md`
- `external/codex/codex-rs/app-server-protocol/src/protocol/common.rs`
- `external/codex/codex-rs/app-server-protocol/schema/json/v2/`
