# Codex 0.160.0 -> 0.160.1 Interop Research

## Scope

- Verified the `0.160.1` API marker, vendored `rust-v0.160.1` tag, and submodule commit match exactly.
- Regenerated the upstream app-server schema and DTO bundle and confirmed it is unchanged.
- Audited the release notes and the complete two-file source delta from `rust-v0.160.0` to `rust-v0.160.1`.

## Confirmed Upstream Change

- Codex's remote-executor stdio MCP launcher now preserves `SYSTEMROOT`, `TEMP`, and `TMP` from a Windows executor when explicit remote environment variables activate its allowlist. This prevents Windows process startup failures when the orchestrator itself runs on Unix.
- The only other source change updates the Rust workspace package version from `0.160.0` to `0.160.1`.

## No Actionable SDK Drift

- The corrected allowlist belongs to Codex's internal remote MCP executor launcher. The SDK configures and starts Codex but does not reproduce that remote environment filtering policy.
- No app-server protocol or schema contract changed, and regeneration produced no schema or DTO changes.
- Existing SDK MCP configuration and status projections require no update for the launcher fix.

## Validation

- `python3 scripts/sync-package-version.py --check`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.160.1`
- Local commit, name-only, stat, and source diffs from `rust-v0.160.0` to `rust-v0.160.1`
- `codex-rs/rmcp-client/src/stdio_server_launcher.rs` and its regression test

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.160.0 -> 0.160.1` window.
