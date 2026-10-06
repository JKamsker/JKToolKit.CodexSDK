# Codex 0.160.0 -> 0.160.1 Interop Research

## Scope

- Verified that the `0.160.1` API marker and vendored `rust-v0.160.1` commit match exactly.
- Regenerated the upstream app-server schema and DTO bundle and confirmed it is unchanged.
- Audited the `rust-v0.160.0..rust-v0.160.1` release notes and complete source delta.

## Confirmed Upstream Changes

- Codex's remote stdio MCP launcher now preserves `SYSTEMROOT`, `TEMP`, and `TMP` when explicit remote environment variables activate its allowlist. This allows a Unix orchestrator to retain the startup environment required by a Windows executor.
- The only other source change updates the upstream workspace package version from `0.160.0` to `0.160.1`.

## No Actionable SDK Drift

- The app-server protocol schema did not change, and regeneration produced no schema or DTO changes.
- Remote stdio MCP process launch and its environment allowlist are implemented inside Codex. The SDK forwards MCP configuration to Codex and neither launches those servers nor filters their remote environment.
- No SDK exec process-launch, configuration projection, parser, or public contract is affected by this patch.

## Validation

- `python3 scripts/sync-package-version.py --check`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.160.1`
- Local commit and source diff from `rust-v0.160.0` to `rust-v0.160.1`
- Upstream `codex-rs/rmcp-client/src/stdio_server_launcher.rs` implementation and regression test

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.160.0 -> 0.160.1` window.
