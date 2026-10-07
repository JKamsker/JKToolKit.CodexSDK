# Codex 0.160.1 -> 0.161.0 Interop Research

## Scope

- Verified that the `0.161.0` API marker and vendored `rust-v0.161.0` commit match exactly.
- Regenerated the upstream app-server schema and DTO bundle and confirmed the bootstrap commit is current.
- Audited the `rust-v0.160.1..rust-v0.161.0` release notes and the SDK-relevant app-server, exec, protocol, and session-index source delta.

## Confirmed Upstream Changes

- `codex exec` and the TypeScript SDK gained per-turn `--cyber-access-program` selection for `standard`, `daybreak_blue`, and `daybreak_red`.
- App-server thread-goal mutations gained an optional `origin`; explicit user mutations record durable user-goal history only when sent with `origin: "user"`.
- MCP OAuth login responses and completion notifications gained an optional `loginId` that correlates an explicit login attempt.
- Batch session-index name lookup now scans newest-first and trims the latest usable name.
- App-server added experimental thread prediction types and made unknown Codex error variants forward-compatible.
- Thread resume now returns authoritative committed replay history, and Responses retries honor upstream retry guidance.

## SDK Changes

- Added `CodexCyberAccessProgram` and wired it through exec session options, CLI launch arguments, and experimental app-server `turn/start` options.
- Added duplicate-argument validation for the typed exec Cyber option.
- Marked SDK-initiated thread-goal set and clear requests with `origin: "user"`.
- Preserved MCP OAuth `loginId` on both start results and completion notifications.
- Trimmed session-index thread names while preserving the latest usable entry.

## No Additional Actionable Drift

- Thread prediction remains experimental and is preserved as an unknown notification by the existing forward-compatible notification path; no stable SDK surface depends on it.
- Unknown `codexErrorInfo` values are already retained as raw JSON by handwritten SDK models.
- Authoritative resume replay, Responses retry guidance, world-state persistence, permission binding, and SQLite recovery are implemented inside Codex and do not change SDK request or parsing behavior.

## Validation

- `python3 scripts/sync-package-version.py --check`
- Focused tests for exec arguments, app-server turn options, goal mutations, MCP OAuth correlation, notification mapping, experimental guards, and session-index names
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Upstream Sources

- GitHub release notes for `rust-v0.161.0`
- Local source and commit diff from `rust-v0.160.1` to `rust-v0.161.0`
- Upstream app-server protocol and request processors for Cyber selection, goal provenance, MCP OAuth correlation, error compatibility, and thread prediction
- Upstream exec CLI and TypeScript SDK option wiring
- Upstream rollout session-index implementation and regression tests

## Remaining Drift

No remaining actionable drift was identified for existing SDK surfaces in the `0.160.1 -> 0.161.0` window.
