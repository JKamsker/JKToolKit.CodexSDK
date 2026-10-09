# Codex 0.162.0 -> 0.162.1 Interop Research

## Scope

- Verified that the `0.162.1` API marker and vendored `rust-v0.162.1` commit match exactly.
- Confirmed the generated app-server schema and DTO bundle is current.
- Audited the `rust-v0.162.0..rust-v0.162.1` release notes and complete source delta.

## Confirmed Upstream Changes

- The TUI now preserves line breaks and complete hyperlink destinations when rendering multiline asynchronous questions.
- TUI-managed daemon compatibility checks now compare only explicit command-line feature overrides. Configuration-file defaults remain owned by the running daemon, and managed configuration and requirements continue to take precedence.
- The only other source change updates the upstream workspace package version from `0.162.0` to `0.162.1`.

## No Actionable SDK Drift

- The release does not change CLI arguments, exec JSON, app-server protocol schemas, or generated DTO contracts.
- Asynchronous-question layout and hyperlink annotation are private TUI presentation behavior; the SDK does not host or reproduce the upstream TUI.
- Daemon compatibility and recovery orchestration are also TUI-owned. The SDK starts exec or app-server processes directly and does not implement the TUI's shared-daemon discovery, restart, or feature-override comparison path.
- Existing typed configuration-requirements support is unaffected because the changed TUI code only consumes that established app-server method when deciding whether a daemon restart is necessary.

## Validation

- `python3 scripts/sync-package-version.py --check`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.162.1`
- Local commit and source diff from `rust-v0.162.0` to `rust-v0.162.1`
- Upstream TUI asynchronous-question rendering, daemon startup and recovery implementations, and their regression tests

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.162.0 -> 0.162.1` window.
