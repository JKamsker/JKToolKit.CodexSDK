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

## Runtime Packaging CI Repair

The initial CI gate passed SDK builds, tests, and coverage on all three operating
systems, but every runtime packaging job rejected the stale `0.160.0` lock.
Refreshed all six official npm archive URLs and SHA-512 hashes for `0.160.1`,
and added the same refresh to upstream-sync bootstrap for future releases.
Automation tests now require the committed lock to match the API pin and cover
every supported platform with a valid official source and SHA-512 digest.

The runtime smoke consumer also hardcoded `0.160.0`; it now uses
`$(CodexCliVersion)` so CLI updates and SDK-only patches resolve the correct
runtime independently of the SDK package version.

Repair validation: 93 automation tests passed, generated DTOs remained current,
and the full Release suite passed (3,764 passed, 17 opt-in integration tests
skipped). The Linux runtime consumer resolved and executed `0.160.1` both from
build output and published output.
