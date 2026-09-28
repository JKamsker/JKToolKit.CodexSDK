# Codex 0.157.1 -> 0.158.0 Interop Research

## Scope

- Verified the `0.158.0` API marker matches the vendored `rust-v0.158.0` commit.
- Regenerated the upstream schema and DTO bundle.
- Audited the upstream release notes and the local source delta from `rust-v0.157.1` to `rust-v0.158.0`.
- Focused on changed app-server protocol, MCP configuration, account plan, command lifecycle, and image-tool behavior.

## Confirmed Upstream Changes

- Experimental `environment/add` accepts an optional `authBearerToken` for authenticated exec-server WebSocket connections.
- Streamable HTTP MCP configuration accepts a pre-registered OAuth client secret alongside its client ID.
- ChatGPT plan types include the new `promax` wire value.
- Plugin extension metadata was removed from plugin summaries and the corresponding generated DTOs.
- App-server error information adds `flexUnavailable`.
- Unified command completion preserves early output and reports process-launch failures.
- Image generation supports explicit transparent backgrounds, and image edits accept file-backed conversation images.

## SDK Changes

- Added `EnvironmentAddOptions.AuthBearerToken` and forward it as `authBearerToken`.
- Extended `SetMcpServerStreamableHttp` with OAuth client ID and secret overrides, including upstream-compatible validation that a secret requires a nonempty client ID.
- Added `CodexPlanType.ProMax` for the `promax` wire value.
- Accepted the generated removal of obsolete plugin extension DTOs and the updated generated plan enum.

## No Additional SDK Drift

- `CodexTurnError.CodexErrorInfo` preserves new error variants as raw JSON, so `flexUnavailable` remains forward compatible without weakening its contract.
- Command lifecycle improvements preserve existing app-server item fields consumed by the SDK; no wire projection changed.
- Image background and file-reference behavior is internal to upstream tool execution. The SDK consumes the resulting image-generation items without constructing those tool calls.
- Plugin extension metadata was not projected by the handwritten public plugin model, so only generated internal DTO removal was required.
- TUI, sandbox, Guardian, process-launch, and transport-internal changes do not have SDK-owned equivalents.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- Focused unit tests for environment, MCP configuration, and account plan handling
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.158.0`
- Local source and schema diff from `rust-v0.157.1` to `rust-v0.158.0`
- Upstream environment/add serialization tests
- Upstream MCP config, CLI, and OAuth client-credential tests

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.157.1 -> 0.158.0` window.
