# Codex 0.144.4 -> 0.144.6 Interop Research

## Scope

- Verified `UPSTREAM_CODEX_VERSION.json` `api` is `0.144.6`.
- Verified `external/codex` is pinned to `rust-v0.144.6` and matches the `rust-v0.144.6` tag commit.
- Reviewed the local upstream delta from `rust-v0.144.4` to `rust-v0.144.6`, focusing on SDK protocol/schema, exec, app-server, generated DTO, model catalog, and command execution impact.

## Update Status

- Generated upstream schema/DTO output is up to date.
- No handwritten SDK code changes were required.
- `UPSTREAM_CODEX_VERSION.json` `integration` is updated to `0.144.6` after this parity pass.

## Confirmed Upstream Changes

### 1. Dangerous command detection is stricter for forced `rm`

Upstream replaced the boolean dangerous-command heuristic with a typed `DangerousCommandMatch` result and expanded forced `rm` detection. The detector now catches more literal forms, including `/bin/rm`, combined force flags such as `-fr`, `--force`, `sudo rm`, `env ... rm`, `trap` actions, and literal commands nested in more complex shell syntax. When `AskForApproval::Never` is set, forced `rm` is now forbidden even when the sandbox or permission profile is explicitly disabled, and the rejection reason identifies the forced-`rm` policy.

SDK impact:

- The SDK does not implement or mirror upstream's dangerous-command evaluator.
- Exec mode launches the vendored Codex CLI and reads its JSONL/session output, so the stricter runtime decision stays upstream-owned.
- App-server `command/exec` sends the argv, sandbox policy, timeout, streaming, and environment parameters to upstream and parses the upstream response; it does not pre-approve, reject, or rewrite command safety decisions locally.
- Existing SDK approval-policy and sandbox models already preserve the upstream wire values needed to exercise the new behavior.

### 2. Complex shell parsing supports safety detection

Upstream added `parse_shell_lc_literal_commands` in `codex-rs/shell-command`, allowing safety checks to inspect literal command nodes inside valid shell syntax while ignoring dynamic words and redirections.

SDK impact:

- This is internal upstream shell parsing. No SDK parser, DTO, public API, or regression test fixture duplicates this logic.
- No SDK code change is needed unless the SDK later adds a local command-safety preview API.

### 3. Bundled model metadata was refreshed

The `0.144.6` hotfix refreshes `codex-rs/models-manager/models.json`, including context-window metadata and bundled prompt text for model catalog entries.

SDK impact:

- The app-server protocol schema and generated DTO bundle did not change in this window.
- The public SDK `model/list` wrapper projects stable app-server response fields and preserves raw entries via `ModelListEntry.Raw`, so extra or changed server-provided catalog metadata remains available without changing typed projections.
- No SDK-side prompt construction or model metadata bundle is maintained in this repository.

## Audited Changes That Required No SDK Code

- `codex-rs/app-server-protocol` had no diff between `rust-v0.144.4` and `rust-v0.144.6`.
- `codex-rs/core/src/exec_policy.rs` changed upstream runtime approval/forbidden decisions only.
- `codex-rs/shell-command/src/bash.rs` and `codex-rs/shell-command/src/command_safety/is_dangerous_command.rs` changed upstream runtime command parsing and safety classification only.
- `codex-rs/models-manager/models.json` changed bundled upstream catalog metadata only.

## Validation

Validation was run after the audit:

- `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- `dotnet test JKToolKit.CodexSDK.sln --configuration Release`

## Remaining Drift

No remaining actionable SDK drift was identified for the `0.144.4 -> 0.144.6` window during this pass.

## Upstream Sources

- `external/codex` local tags `rust-v0.144.4`, `rust-v0.144.5`, and `rust-v0.144.6`
- `external/codex/codex-rs/core/src/exec_policy.rs`
- `external/codex/codex-rs/core/src/exec_policy_tests.rs`
- `external/codex/codex-rs/core/tests/suite/exec_policy.rs`
- `external/codex/codex-rs/shell-command/src/bash.rs`
- `external/codex/codex-rs/shell-command/src/command_safety/is_dangerous_command.rs`
- `external/codex/codex-rs/models-manager/models.json`
