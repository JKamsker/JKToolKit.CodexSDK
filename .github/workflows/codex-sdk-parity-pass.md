---
description: |
  Runs the local codex-sdk-parity-pass skill after upstream Codex sync activity.
  The workflow repairs SDK drift on upstream-sync PRs and creates a parity PR if
  the default branch ever has an upstream API/submodule or integration mismatch.

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
      - "UPSTREAM_CODEX_VERSION.json"
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

network:
  allowed:
    - defaults
    - dotnet

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
# The same compile script also injects model_reasoning_effort = "high" into
# the generated Codex config so the model remains controlled by GH_AW_* vars.
engine: codex

post-steps:
  - name: Check whether parity validation is required
    id: parity-validation-guard
    shell: bash
    run: |
      set -euo pipefail
      outputs="/tmp/gh-aw/safeoutputs.jsonl"
      should_validate=false
      if [ -s "$outputs" ] && grep -Eq '"type":"(create_pull_request|push_to_pull_request_branch)"' "$outputs"; then
        should_validate=true
      fi
      echo "should_validate=${should_validate}" >> "$GITHUB_OUTPUT"

  - name: Setup .NET for parity validation
    if: steps.parity-validation-guard.outputs.should_validate == 'true'
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 10.0.x

  - name: Materialize safe-output patch for validation
    if: steps.parity-validation-guard.outputs.should_validate == 'true'
    shell: bash
    run: |
      set -euo pipefail
      base_sha="${GITHUB_SHA:-}"
      current_sha="$(git rev-parse HEAD)"
      has_workspace_changes=false
      git diff --quiet || has_workspace_changes=true
      git diff --cached --quiet || has_workspace_changes=true

      if [ "$has_workspace_changes" = false ] && [ -n "$base_sha" ] && [ "$current_sha" = "$base_sha" ]; then
        shopt -s nullglob
        patches=(/tmp/gh-aw/aw-*.patch)
        if [ "${#patches[@]}" -eq 0 ]; then
          echo "::error::Safe output requested code changes, but the agent left no workspace changes and produced no patch artifact."
          exit 1
        fi
        if [ "${#patches[@]}" -gt 1 ]; then
          printf '::error::Expected one safe-output patch, found %s: %s\n' "${#patches[@]}" "${patches[*]}"
          exit 1
        fi
        git apply --check "${patches[0]}"
        git apply "${patches[0]}"
      fi

      git status --short

  - name: Restore parity validation dependencies
    if: steps.parity-validation-guard.outputs.should_validate == 'true'
    run: dotnet restore JKToolKit.CodexSDK.sln

  - name: Verify generated DTOs before safe output
    if: steps.parity-validation-guard.outputs.should_validate == 'true'
    run: dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release --no-restore -- check

  - name: Build before safe output
    if: steps.parity-validation-guard.outputs.should_validate == 'true'
    run: dotnet build JKToolKit.CodexSDK.sln --configuration Release --no-restore

  - name: Test before safe output
    if: steps.parity-validation-guard.outputs.should_validate == 'true'
    run: dotnet test JKToolKit.CodexSDK.sln --configuration Release --no-build

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
3. `UPSTREAM_CODEX_VERSION.json` has an `api` version whose tag commit does not match the checked-out `external/codex` submodule commit.
4. `UPSTREAM_CODEX_VERSION.json` has different `api` and `integration` versions, which means generated API artifacts are ahead of the deeper SDK parity baseline.

## First: Decide Whether A Pass Is Needed

Rebuild the local context before making changes:

```bash
python .codex/skills/codex-sdk-parity-pass/scripts/parity_context.py
cat UPSTREAM_CODEX_VERSION.json
git -C external/codex fetch --tags --force
git -C external/codex describe --tags --always
```

Then compare the API marker to the submodule and integration marker:

```bash
api_version="$(python3 -c 'import json; print(json.load(open("UPSTREAM_CODEX_VERSION.json", encoding="utf-8"))["api"])')"
integration_version="$(python3 -c 'import json; print(json.load(open("UPSTREAM_CODEX_VERSION.json", encoding="utf-8"))["integration"])')"
expected="$(git -C external/codex rev-parse "rust-v${api_version}^{commit}")"
actual="$(git -C external/codex rev-parse HEAD)"
test "$expected" = "$actual"
test "$api_version" = "$integration_version"
```

No-op if all of these are true:

- this is not a relevant pull request run or upstream-sync dispatch,
- the API marker and submodule commit already match,
- the API marker and integration marker already match,
- generated upstream DTO/schema checks are clean,
- and there is no confirmed SDK drift from the upstream delta.

When no-oping, leave the workspace unchanged and emit a concise explanation in the final output. Do not create a pull request and do not push to a pull request branch.

## Pull Request And Upstream-Sync Dispatch Runs

On `pull_request` events, only make changes when the triggering PR is an upstream Codex sync PR or clearly changes upstream Codex inputs:

- `UPSTREAM_CODEX_VERSION.json`
- the `external/codex` submodule
- generated upstream schema/DTO files
- upstream generator code

On `workflow_dispatch` events with `github.event.inputs.upstream_sync_pr == 'true'`, treat the run as the same upstream-sync PR path. Use `github.event.inputs.upstream_version` and `github.event.inputs.upstream_pr` as hints, but verify the actual checked-out `UPSTREAM_CODEX_VERSION.json`, `external/codex` submodule, and PR state from git/GitHub before making changes.

If the PR is from a fork, no-op; this workflow is only intended to repair same-repository upstream-sync branches.

When changes are needed:

1. Follow `.codex/skills/codex-sdk-parity-pass/SKILL.md`.
2. Audit the upstream delta from the `integration` version to the `api` version in `UPSTREAM_CODEX_VERSION.json`.
3. Implement only confirmed SDK drift fixes.
4. Update or create the relevant `docs/codex-<from>-to-<to>-interop.md` note.
5. Run focused tests for every touched surface.
6. Run `dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check`.
7. Run `dotnet test JKToolKit.CodexSDK.sln --configuration Release` if the change is not purely documentation.
8. Update `UPSTREAM_CODEX_VERSION.json` so `integration` matches `api` after the deeper SDK parity pass is complete. Do not change `api` from this workflow except when explicitly aligning an API marker/submodule mismatch.
9. Commit the changes locally.
10. Use the `push_to_pull_request_branch` safe-output tool to update the triggering PR branch.

Do not use raw `git push`.

## Validation Before Safe Output

Do not request `create_pull_request` or `push_to_pull_request_branch` until the local workspace passes the required validation commands for the changes you made.

The workflow also enforces this after the agent runs: if a safe-output write is requested, post-steps materialize the proposed patch if needed and run restore, generated DTO check, build, and full tests before safe outputs are processed. If those checks fail, the workflow run must fail instead of creating or updating a red pull request.

## Scheduled And Other Manual Runs

On `schedule` or manual `workflow_dispatch` runs where `github.event.inputs.upstream_sync_pr` is not `true`, use the API/submodule mismatch and API/integration mismatch checks above as the primary triggers.

If the default branch has an API/submodule mismatch:

1. Align `UPSTREAM_CODEX_VERSION.json` `api` and `external/codex` to the same `rust-v<version>` tag. Preserve the `integration` value until a deeper parity pass is complete.
2. Regenerate generated upstream artifacts if needed:

   ```bash
   dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- generate
   dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check
   ```

If the default branch has an API/integration mismatch:

1. Run the parity skill workflow against the delta from `integration` to `api`.
2. Validate as described in the skill.
3. Update `UPSTREAM_CODEX_VERSION.json` so `integration` matches `api`.
4. Commit the changes locally.
5. Use the `create_pull_request` safe-output tool with a title beginning `[parity] Codex SDK parity for <api-version>`.

If there is no mismatch and no actionable drift, no-op.

## Scope Guardrails

- Do not touch unrelated product code.
- Do not update the gh-aw workflow lockfiles unless the parity fix requires workflow changes.
- Do not relax SDK typing or validation unless upstream source proves the looser contract is correct.
- Do not print API keys, virtual token values, or endpoint hosts.
- Prefer primary sources: upstream release notes, `external/codex` tags, upstream source, and upstream tests.
