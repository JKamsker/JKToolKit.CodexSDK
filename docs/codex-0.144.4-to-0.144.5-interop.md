# Codex 0.144.4 -> 0.144.5 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.144.5`.
- Verified `external/codex` is pinned to `rust-v0.144.5` and matches the `rust-v0.144.5` tag commit.
- Reviewed the local upstream delta from `rust-v0.144.4` to `rust-v0.144.5`, focusing on SDK protocol/schema, exec, app-server, approval, and generated DTO impact.

## Update Status

- Generated upstream schema/DTO output is up to date.
- No handwritten SDK code changes were required.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.144.5` after this parity pass.

## Confirmed Upstream Changes

### 1. Dangerous-command detection now catches more forced `rm` forms

Upstream expanded command-safety heuristics for exec policy. The change detects forced `rm` variants beyond the previous direct `rm -f` / `rm -rf` cases, including split or reordered force flags, `/bin/rm`, `sudo`, `env`, `trap`, and literal commands nested in `bash -lc` scripts. It also changes denied forced-`rm` messaging to a more specific rejection reason.

SDK impact:

- The SDK does not implement or mirror upstream dangerous-command classification.
- Exec-mode calls delegate approval policy, sandbox policy, command parsing, and dangerous-command decisions to the vendored Codex CLI/runtime.
- App-server approval wrappers preserve the command-execution approval request and response shapes; no wire contract changed for `item/commandExecution/requestApproval`.
- No SDK-side parser, DTO, or public model change is needed for the new upstream heuristics.

### 2. `AskForApproval::Never` no longer allows dangerous commands solely because sandboxing is disabled

Upstream now forbids dangerous unmatched commands under `AskForApproval::Never`, including external or disabled sandbox profiles, instead of allowing them when sandbox enforcement was explicitly absent.

SDK impact:

- The SDK exposes `CodexApprovalPolicy.Never`, `CodexAskForApproval`, sandbox policy, and permission profile values as protocol inputs only.
- The changed decision is made inside upstream exec policy evaluation, not in SDK code.
- Existing SDK validation intentionally does not pre-classify command strings, so no behavior needs to be duplicated in C#.

## Audited Changes That Required No SDK Code

- `codex-rs/app-server-protocol` had no diff between `rust-v0.144.4` and `rust-v0.144.5`.
- `codex-rs/core/src/exec_policy.rs` changed internal exec-policy evaluation and rejection reasons only.
- `codex-rs/shell-command/src/bash.rs` and `codex-rs/shell-command/src/command_safety/is_dangerous_command.rs` changed internal shell parsing and dangerous-command detection only.
- Repository searches found no SDK implementation of `is_dangerous_command`, `exec_policy`, or dangerous-command approval heuristics to update.

## Validation

Validation was run after the audit:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the `0.144.4 -> 0.144.5` window during this pass.

## Upstream Sources

- GitHub release `rust-v0.144.5`
- `external/codex` local tags `rust-v0.144.4` and `rust-v0.144.5`
- `external/codex/codex-rs/core/src/exec_policy.rs`
- `external/codex/codex-rs/core/src/exec_policy_tests.rs`
- `external/codex/codex-rs/core/tests/suite/exec_policy.rs`
- `external/codex/codex-rs/shell-command/src/bash.rs`
- `external/codex/codex-rs/shell-command/src/command_safety/is_dangerous_command.rs`
