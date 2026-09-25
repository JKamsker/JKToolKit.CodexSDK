# Codex 0.156.1 -> 0.157.0 Interop Research

## Scope

- Verified the `0.157.0` API marker matches the vendored `rust-v0.157.0` commit.
- Audited the release notes and local source delta from `rust-v0.156.1` to `rust-v0.157.0`.
- Focused on changed app-server protocol/schema, MCP resources and status, plugin summaries, gateway OAuth, thread item history, and exec behavior.
- Regenerated the upstream schema/DTO bundle before the handwritten audit.

## Confirmed SDK Changes

### MCP resource targeting and status

- Added the explicit hosted app/account target to `mcpResource/read`, including the required connector id and nullable account link id.
- Added the credential-free HTTP origin to typed MCP server status results.
- Fixed upstream DTO generation so the nullable resource target references `McpResourceReadTarget` instead of an empty anonymous `Target` placeholder. Without the fix, target fields were silently omitted from serialized requests.

### Initialize capability

- Added the `explicitGatewayOAuth` initialize capability and a matching client option. This preserves negotiation support for clients that handle the new explicit gateway OAuth protocol.

## No Actionable Drift

- Gateway OAuth requests and notifications are new opt-in protocol surfaces; generated DTOs preserve their wire contracts, while the current high-level account API remains unchanged.
- Hosted plugin extension declarations are preserved in generated DTOs and raw plugin summary payloads; no existing typed plugin field changed semantics.
- Thread item lifecycle timestamps affect `thread/items/list`, which is not currently exposed as a handwritten SDK operation.
- Exec changes are limited to authentication environment handling and worktree internals and do not change current SDK-owned exec JSON contracts.
- TUI, daemon, realtime, network policy, sandbox, voice, and multi-agent implementation changes do not alter existing SDK-owned stable behavior.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- Focused MCP and initialize-capability tests
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.157.0`
- Local source and tests under `external/codex/codex-rs/app-server-protocol`
- Local app-server MCP, plugin, gateway OAuth, and thread-history implementations
- Local exec and protocol changes under `external/codex/codex-rs/exec` and `external/codex/codex-rs/protocol`

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.156.1 -> 0.157.0` window.
