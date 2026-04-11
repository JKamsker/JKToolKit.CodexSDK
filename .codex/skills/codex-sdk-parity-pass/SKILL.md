---
name: codex-sdk-parity-pass
description: Research and implement repo-specific parity updates between JKToolKit.CodexSDK and the vendored upstream Codex CLI in `external/codex`. Use when `UPSTREAM_CODEX_VERSION.txt` or `external/codex` changes, when a user asks what changed in codex-cli since the last parity pass, or when you need to audit and fix drift in exec-mode, app-server DTOs/parsers, docs, tests, or generated schema interop.
---

# Codex SDK Parity Pass

Use this skill to upgrade this repository after upstream `codex-cli` changes. Rebuild the last known-good baseline, research the upstream delta from primary sources, map it to the SDK surfaces that historically drift, land only confirmed fixes, and finish with focused plus full validation.

## Quick Start

1. Run `python .codex/skills/codex-sdk-parity-pass/scripts/parity_context.py`.
2. Read `.codex/skills/codex-sdk-parity-pass/references/last-parity-pass.md`.
3. Read `.codex/skills/codex-sdk-parity-pass/references/repo-map.md`.
4. Read `docs/upstreamgen.md` if the pin, vendored submodule, or generated DTOs will change.
5. Use `gh` CLI when possible for upstream release research and PR/CI follow-up.

## Workflow

### 1. Rebuild the baseline

- Confirm the repo root, current branch, pinned upstream version, and vendored `external/codex` tag.
- Treat `.codex/skills/codex-sdk-parity-pass/references/last-parity-pass.md` as the compact summary of what was already fixed in the `0.106.0 -> 0.118.0` pass.
- Read the current repo artifact `docs/codex-0.106-to-0.118-interop.md` before touching code. Prefer repo artifacts over old chat logs whenever both exist.
- If the user supplies an earlier upgrade log, mine it only for missing context that was not preserved in repo docs or commit history.

### 2. Determine the upstream delta

- Identify the last parity baseline version and the new target version before changing code.
- Prefer primary sources:
  - `gh release list -R openai/codex --limit 20`
  - `gh release view rust-v<version> -R openai/codex`
  - `git -C external/codex describe --tags --always`
  - `git -C external/codex log --oneline rust-v<from>..rust-v<to>`
  - `git -C external/codex diff --stat rust-v<from>..rust-v<to>`
- Release notes are only a starting point. Verify behavior by reading the touched upstream source in `external/codex/codex-cli` and `external/codex/codex-rs`.
- If the target version is newer than the vendored submodule, keep `UPSTREAM_CODEX_VERSION.txt` and `external/codex` on the same version line.

### 3. Audit the highest-risk surfaces first

- Start with the areas that historically drift in this repo:
  - Exec parity: resume target selection, session discovery, cwd normalization, recency ordering, provider scoping, session index behavior, structured-output semantics.
  - App-server parity: typed `turn/start` payloads, thread/turn/source/status unions, MCP tool and status parsing, plugin/account/config/app projections, approval/request envelopes.
  - Generated DTO drift: incorrect enums, placeholder unions, or misleading generated contracts under `Generated/Upstream/AppServer/V2`.
  - Docs and tests: outdated review examples, cross-platform path assumptions, stale fixtures, or missing regression coverage.
- Use `.codex/skills/codex-sdk-parity-pass/references/repo-map.md` to jump straight to the relevant SDK files and tests.
- When upstream behavior is unclear, inspect upstream code and its tests instead of inferring from names alone.

### 4. Implement only confirmed drift

- Reproduce the mismatch first: schema contract change, behavior change, or failing test.
- Prefer small targeted fixes plus regression tests over broad speculative cleanup.
- Keep non-generated C# maintainable and easy to understand. Favor composition and helper types over clever code or large files.
- If a touched file grows too far past the local size guard, split it by responsibility instead of hiding the size in partials without a strong reason.
- Do not relax SDK validation or typing unless upstream evidence shows the looser contract is correct.
- Generated DTOs are not authoritative. When the generator is wrong, fix generation or project into handwritten typed models instead of forcing public callers through broken raw shapes.

### 5. Validate before calling parity complete

- If the upstream pin or schema bundle changed:
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate`
  - `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`
- Run focused tests for each touched area before broad validation.
- Finish with `dotnet test JKToolKit.CodexSDK.sln --configuration Release`.
- Use the manual runbooks for risky behavior changes:
  - `docs/Runbooks/Manual-Testing/Exec.md`
  - `docs/Runbooks/Manual-Testing/AppServer.md`
- If work is on a PR branch, use `gh pr checks <number>` after pushing to confirm CI instead of assuming local green means hosted CI is green.

### 6. Close out the pass

- Update or create a research note named `docs/codex-<from>-to-<to>-interop.md`.
- Summarize:
  - baseline version
  - target version
  - confirmed upstream changes that mattered to the SDK
  - SDK changes made
  - tests and manual checks run
  - any remaining drift or explicit statement that no actionable drift remains
- If nothing actionable changed, say so clearly and still record the sources checked and the validation completed.

## Research Heuristics

- Release notes often miss behavior changes; source diff and tests matter more.
- In this repo, the highest-value parity work is usually behavior and typed projection correctness, not just missing top-level methods.
- Validate one end-to-end path at a time: upstream behavior, SDK implementation, regression tests, then docs/examples if needed.
- Default to local work. Only delegate or parallelize review if the user explicitly asks for that mode.

## References

- `.codex/skills/codex-sdk-parity-pass/references/last-parity-pass.md`
- `.codex/skills/codex-sdk-parity-pass/references/repo-map.md`
