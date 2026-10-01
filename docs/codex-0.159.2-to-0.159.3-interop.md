# Codex 0.159.2 -> 0.159.3 Interop Research

## Scope

- Verified the `0.159.3` API marker matches the vendored `rust-v0.159.3` commit.
- Regenerated the upstream schema and DTO bundle and confirmed it is unchanged.
- Audited the release notes and local source delta from `rust-v0.159.2` to `rust-v0.159.3`.
- Focused on the account-security setup reminder and every upstream file changed by the release.

## Confirmed Upstream Changes

- Eligible local TUI sessions authenticated with ChatGPT can asynchronously fetch and show an optional account-security setup reminder.
- The TUI validates reminder content, preserves dismissal state across widget replacement, and refreshes the reminder after account or connection changes.
- The implementation is confined to TUI state, presentation, and tests, apart from the release version update.

## No Actionable SDK Drift

- The release does not change CLI arguments, exec JSON, app-server protocol, schemas, or generated DTO contracts.
- The SDK does not host the upstream TUI or reproduce its private account-security reminder request and banner lifecycle.
- Existing SDK account and authentication projections are unaffected because the reminder uses existing auth status data only to gate a TUI-owned HTTP prefetch.
- Generated schema and DTO output remains current without changes.

## Validation

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.159.3`
- Local commit and source diff from `rust-v0.159.2` to `rust-v0.159.3`
- Upstream TUI security-setup implementation, account/reconnect integration, banner lifecycle, and tests

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.159.2 -> 0.159.3` window.
