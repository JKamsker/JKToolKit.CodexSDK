# Codex 0.158.0 -> 0.159.0 Interop Research

## Scope

- Verified the `0.159.0` API marker matches the vendored `rust-v0.159.0` commit.
- Regenerated the upstream schema and DTO bundle.
- Audited the upstream release notes and the local source delta from `rust-v0.158.0` to `rust-v0.159.0`.
- Focused on changed app-server protocol, MCP discovery, thread history pagination, turn errors, and empty-thread archival behavior.

## Confirmed Upstream Changes

- `mcpServerStatus/list` accepts an optional `serverName` selector. With a thread ID, the app-server reuses the thread's current MCP connection and tool catalog.
- `thread/items/list` accepts either its existing opaque cursor or an item anchor scoped to a non-empty turn ID.
- Interrupted turns can carry structured error information, including the new `tooManyDenials` value when strict Guardian circuit-break behavior is enabled.
- Empty threads can be archived before their first turn and remain visible in archived thread listings.
- MCP app resource URIs are preserved without synthesizing a preferred display mode.

## SDK Changes

- Added `McpServerStatusListOptions.ServerName` and forward it as `serverName` through the handwritten v2 request model.
- Updated the public turn-error documentation to reflect that errors can accompany interrupted turns.
- Accepted the regenerated internal DTOs for item-anchor cursors and the updated turn-error contract.

## No Additional SDK Drift

- `thread/items/list` is not currently exposed by a handwritten public SDK method; its new cursor union is represented by the regenerated internal DTOs.
- `CodexTurnError.CodexErrorInfo` preserves arbitrary JSON, so `tooManyDenials` remains forward compatible without weakening the public contract.
- Thread summaries already accept empty previews, and archive/list methods do not assume a thread has turns.
- MCP app UI metadata is not projected into a handwritten public SDK model, so the upstream display-default change requires no SDK mapping update.
- TUI, sandbox, network proxy, Code Mode, and transport-internal changes do not have SDK-owned equivalents.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- Focused MCP server wrapper and serialization tests
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.159.0`
- Local source and schema diff from `rust-v0.158.0` to `rust-v0.159.0`
- Upstream MCP status, thread item pagination, interrupted-turn history, and empty-thread archive tests

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.158.0 -> 0.159.0` window.
