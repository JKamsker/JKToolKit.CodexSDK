# Codex 0.161.0 -> 0.162.0 Interop Research

## Scope

- Verified that the `0.162.0` API marker, `rust-v0.162.0` tag, and vendored submodule commit match.
- Confirmed the generated app-server schema and DTO bundle is current.
- Audited the `rust-v0.161.0..rust-v0.162.0` release notes and the SDK-relevant app-server, protocol, exec, config, and session delta.

## Confirmed Upstream Changes

- App-server `turn/start` accepts `parentTurnId` and `rootTurnId`, and turn snapshots expose `rootTurnId`, so delegated work can preserve turn attribution.
- App-server added `thread/attachmentOwner/list` for paginated reverse lookup of attachment ownership across archived and non-archived threads.
- `environment/add` accepts required skill catalog names that are checked before inference.
- `configRequirements/read` reports whether Fast and Ultra Fast policy gates are independent and can return managed browser-extension request headers.
- Assistant message phases gained `partial_answer`; the SDK already preserves phase values as forward-compatible strings.
- Sub-agent activity gained optional model and reasoning-effort fields, and misalignment errors gained an opaque review target; the SDK's unknown-item and raw-error projections already preserve these additions.

## SDK Changes

- Added typed turn-lineage options and projected `rootTurnId` from thread history.
- Added typed attachment-owner reverse lookup, including the resilient client surface.
- Added required environment skills to `environment/add`.
- Added independent speed-mode capability projection and typed browser-extension request headers.
- Added focused serialization, parsing, transport, guard, and resilient-client coverage for the new surfaces.

## No Additional Actionable Drift

- Managed worktree tools, transcript selection, TUI URL handling, sandbox internals, response retry timing, and installer changes are implemented inside Codex and do not change existing SDK contracts.
- Exec-mode changes in this release only consume the new turn-lineage fields with null defaults; no SDK CLI argument or resume behavior changed.
- New partial-answer behavior is already retained without enum narrowing by the SDK's string-valued message phase projections.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for turn lineage, attachment-owner lookup, environment skill requirements, config requirements, thread history, and resilient transport
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.162.0`
- Local source and commit diff from `rust-v0.161.0` to `rust-v0.162.0`
- Upstream app-server protocol schemas and request processors for turn lineage, attachment ownership, environment skills, and config requirements
- Upstream exec source and tests for lineage compatibility

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.161.0 -> 0.162.0` window.
