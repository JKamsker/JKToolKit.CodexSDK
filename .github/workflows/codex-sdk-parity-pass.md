---
description: |
  Runs the local codex-sdk-parity-pass skill after upstream Codex sync activity.
  The workflow repairs SDK drift on upstream-sync PRs and creates a parity PR if
  the default branch ever has an UPSTREAM_CODEX_VERSION.txt/submodule mismatch.

on:
  workflow_dispatch:
    inputs:
      upstream_sync_pr:
        description: "Set to true when upstream-sync dispatches this for its update PR."
        required: false
        default: "false"
      upstream_version:
        description: "The @openai/codex version from the upstream-sync PR."
        required: false
        default: ""
      upstream_pr:
        description: "The upstream-sync pull request number, when available."
        required: false
        default: ""
  schedule:
    # GitHub Actions cron uses UTC. These runs are one hour after the
    # 09:00 UTC and 21:00 UTC Upstream Sync runs.
    - cron: "0 10,22 * * *"
  pull_request:
    types: [opened, synchronize, reopened, ready_for_review]
    paths:
      - "UPSTREAM_CODEX_VERSION.txt"
      - "external/codex"
      - "external/codex/**"
      - "src/JKToolKit.CodexSDK/Generated/Upstream/**"
      - "src/JKToolKit.CodexSDK.UpstreamGen/**"
      - "docs/upstreamgen.md"

permissions:
  contents: read
  pull-requests: read
  issues: read

checkout:
  fetch-depth: 0
  submodules: recursive

network: defaults

tools:
  github:
    lockdown: false
    min-integrity: none

safe-outputs:
  create-pull-request:
    title-prefix: "[parity] "
    labels: [automation, parity]
    draft: true
    max: 1
    if-no-changes: "ignore"
    protected-files: fallback-to-issue
  push-to-pull-request-branch:
    target: triggering
    required-title-prefix: "chore(upstream): bump @openai/codex"
    max: 1
    if-no-changes: "ignore"
    protected-files: fallback-to-issue
    fallback-as-pull-request: true

# Use .github/scripts/compile_gh_aw.py after editing this workflow. See
# docs/Runbooks/GhAwCustomEndpoint.md for the secret-backed endpoint contract.
engine: codex

post-steps:
  - name: Redact Codex endpoint artifacts
    if: always()
    env:
      CODEX_LB_BASE_URL: ${{ secrets.CODEX_LB_BASE_URL }}
    run: python3 .github/scripts/redact_codex_endpoint_artifacts.py
---

# Codex SDK Parity Pass

Use the local skill at `.codex/skills/codex-sdk-parity-pass` for this run.

This workflow exists to keep `JKToolKit.CodexSDK` aligned with the vendored upstream Codex CLI in `external/codex`. It should normally do useful work only when one of these conditions is true:

1. The run was triggered by an upstream Codex sync pull request, for example a PR titled `chore(upstream): bump @openai/codex to <version>`.
2. The run was dispatched by `Upstream Sync (@openai/codex)` with `github.event.inputs.upstream_sync_pr` set to `true`.
3. `UPSTREAM_CODEX_VERSION.txt` names a Codex version whose tag commit does not match the checked-out `external/codex` submodule commit.

## First: Decide Whether A Pass Is Needed

Rebuild the local context before making changes:

```bash
python .codex/skills/codex-sdk-parity-pass/scripts/parity_context.py
cat UPSTREAM_CODEX_VERSION.txt
git -C external/codex fetch --tags --force
git -C external/codex describe --tags --always
```

Then compare the version pin to the submodule:

```bash
version="$(tr -d '\r' < UPSTREAM_CODEX_VERSION.txt | head -n 1 | xargs)"
expected="$(git -C external/codex rev-parse "rust-v${version}^{commit}")"
actual="$(git -C external/codex rev-parse HEAD)"
test "$expected" = "$actual"
```

No-op if all of these are true:

- this is not a relevant pull request run or upstream-sync dispatch,
- the version pin and submodule commit already match,
- generated upstream DTO/schema checks are clean,
- and there is no confirmed SDK drift from the upstream delta.

When no-oping, leave the workspace unchanged and emit a concise explanation in the final output. Do not create a pull request and do not push to a pull request branch.

## Pull Request And Upstream-Sync Dispatch Runs

On `pull_request` events, only make changes when the triggering PR is an upstream Codex sync PR or clearly changes upstream Codex inputs:

- `UPSTREAM_CODEX_VERSION.txt`
- the `external/codex` submodule
- generated upstream schema/DTO files
- upstream generator code

On `workflow_dispatch` events with `github.event.inputs.upstream_sync_pr == 'true'`, treat the run as the same upstream-sync PR path. Use `github.event.inputs.upstream_version` and `github.event.inputs.upstream_pr` as hints, but verify the actual checked-out `UPSTREAM_CODEX_VERSION.txt`, `external/codex` submodule, and PR state from git/GitHub before making changes.

If the PR is from a fork, no-op; this workflow is only intended to repair same-repository upstream-sync branches.

When changes are needed:

1. Follow `.codex/skills/codex-sdk-parity-pass/SKILL.md`.
2. Audit the upstream delta from the previous SDK parity baseline to the new `UPSTREAM_CODEX_VERSION.txt` version.
3. Implement only confirmed SDK drift fixes.
4. Update or create the relevant `docs/codex-<from>-to-<to>-interop.md` note.
5. Run focused tests for every touched surface.
6. Run `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`.
7. Run `dotnet test JKToolKit.CodexSDK.sln --configuration Release` if the change is not purely documentation.
8. Commit the changes locally.
9. Use the `push_to_pull_request_branch` safe-output tool to update the triggering PR branch.

Do not use raw `git push`.

## Scheduled And Other Manual Runs

On `schedule` or manual `workflow_dispatch` runs where `github.event.inputs.upstream_sync_pr` is not `true`, use the version/submodule mismatch check above as the primary trigger.

If the default branch has a mismatch:

1. Align `UPSTREAM_CODEX_VERSION.txt` and `external/codex` to the same `rust-v<version>` tag.
2. Regenerate generated upstream artifacts if needed:

   ```bash
   dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate
   dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check
   ```

3. Run the parity skill workflow against the resulting upstream delta.
4. Validate as described in the skill.
5. Commit the changes locally.
6. Use the `create_pull_request` safe-output tool with a title beginning `[parity] Codex SDK parity for <version>`.

If there is no mismatch and no actionable drift, no-op.

## Scope Guardrails

- Do not touch unrelated product code.
- Do not update the gh-aw workflow lockfiles unless the parity fix requires workflow changes.
- Do not relax SDK typing or validation unless upstream source proves the looser contract is correct.
- Do not print API keys, virtual token values, or endpoint hosts.
- Prefer primary sources: upstream release notes, `external/codex` tags, upstream source, and upstream tests.
