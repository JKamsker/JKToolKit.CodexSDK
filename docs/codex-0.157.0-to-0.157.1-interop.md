# Codex 0.157.0 -> 0.157.1 Interop Research

## Scope

- Verified the `0.157.1` API marker matches the vendored `rust-v0.157.1` commit.
- Regenerated the upstream schema/DTO bundle and confirmed it is unchanged.
- Audited the release notes and local source delta from `rust-v0.157.0` to `rust-v0.157.1`.
- Focused on the changed Windows app-server daemon, local MCP server, code-mode host, and PTY process-launch behavior.

## Confirmed Upstream Changes

- Windows daemon launches no longer inherit the launcher's standard handles, so callers observing captured launcher output are not kept open by the detached daemon.
- Windows daemon breakaway validation now permits residual outer Job Object membership when the actual breakaway launch succeeds.
- Windows local MCP servers and the code-mode host now use `CREATE_NO_WINDOW`; suspended MCP launches preserve that flag.
- An optional MCP startup-grace test received timing-only adjustments for slower runners.

## No Actionable SDK Drift

- The affected daemon detachment and Job Object lifecycle are internal upstream implementation details and are not reimplemented by the SDK.
- SDK-owned exec, review, app-server, and generic stdio launches already set `CreateNoWindow = true` and intentionally redirect the child streams they consume.
- No app-server protocol, schema, exec JSON contract, or public SDK behavior changed in this release.
- Generated schema and DTO output remains current without changes.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.157.1`
- Local commit and source diff from `rust-v0.157.0` to `rust-v0.157.1`
- Upstream Windows daemon tests for inherited standard handles and residual Job Object membership
- Upstream local MCP stdio tests for console-free direct and `cmd.exe` launches

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.157.0 -> 0.157.1` window.
