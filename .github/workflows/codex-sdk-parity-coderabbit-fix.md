---
description: |
  Fixes actionable CodeRabbit review comments on parity pull requests created by
  the gh-aw parity automation.

on:
  workflow_dispatch:
    inputs:
      pr_number:
        description: "Parity pull request number to replay existing CodeRabbit comments for."
        required: false
        default: ""
      review_id:
        description: "Optional CodeRabbit pull request review id to focus on."
        required: false
        default: ""
  pull_request_review:
    types: [submitted, edited]
  bots:
    - coderabbitai
    - "coderabbitai[bot]"

if: >
  (
    github.event_name == 'workflow_dispatch' &&
    (
      github.event.inputs.pr_number != '' ||
      (
        fromJSON(github.event.inputs.aw_context || '{}').item_type == 'pull_request' &&
        fromJSON(github.event.inputs.aw_context || '{}').item_number != ''
      )
    )
  ) ||
  (
    github.event_name == 'pull_request_review' &&
    (github.event.review.user.login == 'coderabbitai' || github.event.review.user.login == 'coderabbitai[bot]') &&
    (github.event.review.state == 'commented' || github.event.review.state == 'COMMENTED') &&
    github.event.pull_request.state == 'open' &&
    startsWith(github.event.pull_request.title, '[parity] ') &&
    github.event.pull_request.user.login == 'github-actions[bot]' &&
    startsWith(github.event.pull_request.head.ref, 'parity/codex-') &&
    contains(toJSON(github.event.pull_request.labels.*.name), '"automation"') &&
    contains(toJSON(github.event.pull_request.labels.*.name), '"parity"')
  )

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
  push-to-pull-request-branch:
    target: triggering
    required-title-prefix: "[parity] "
    max: 1
    if-no-changes: "ignore"
    protected-files: fallback-to-issue

# Use .github/scripts/compile_gh_aw.py after editing this workflow. See
# docs/Runbooks/GhAwCustomEndpoint.md for the secret-backed endpoint contract.
# The same compile script also injects model_reasoning_effort = "high" into
# the generated Codex config so the model remains controlled by GH_AW_* vars.
engine: codex

post-steps:
  - name: Check whether parity CodeRabbit validation is required
    id: coderabbit-validation-guard
    shell: bash
    run: |
      set -euo pipefail
      outputs="/tmp/gh-aw/safeoutputs.jsonl"
      should_validate=false
      if [ -s "$outputs" ] && grep -Eq '"type":"push_to_pull_request_branch"' "$outputs"; then
        should_validate=true
      fi
      echo "should_validate=${should_validate}" >> "$GITHUB_OUTPUT"

  - name: Setup .NET for CodeRabbit fix validation
    if: steps.coderabbit-validation-guard.outputs.should_validate == 'true'
    uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 10.0.x

  - name: Materialize safe-output patch for CodeRabbit validation
    if: steps.coderabbit-validation-guard.outputs.should_validate == 'true'
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

  - name: Restore CodeRabbit fix validation dependencies
    if: steps.coderabbit-validation-guard.outputs.should_validate == 'true'
    run: dotnet restore JKToolKit.CodexSDK.sln

  - name: Verify generated DTOs before CodeRabbit safe output
    if: steps.coderabbit-validation-guard.outputs.should_validate == 'true'
    run: dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release --no-restore -- check

  - name: Build before CodeRabbit safe output
    if: steps.coderabbit-validation-guard.outputs.should_validate == 'true'
    run: dotnet build JKToolKit.CodexSDK.sln --configuration Release --no-restore

  - name: Test before CodeRabbit safe output
    if: steps.coderabbit-validation-guard.outputs.should_validate == 'true'
    run: dotnet test JKToolKit.CodexSDK.sln --configuration Release --no-build

  - name: Redact Codex endpoint artifacts
    if: always()
    env:
      CODEX_LB_BASE_URL: ${{ secrets.CODEX_LB_BASE_URL }}
    run: python3 .github/scripts/redact_codex_endpoint_artifacts.py
---

# Codex SDK Parity CodeRabbit Fix

Fix actionable CodeRabbit findings on an existing gh-aw parity pull request.

This workflow should only do useful work when it was triggered by a CodeRabbit
pull request review on an open parity PR created by the gh-aw parity workflow,
or when it was manually dispatched to replay existing CodeRabbit comments for
such a PR. The compiled workflow also guards this before activation where it can,
but verify it yourself before changing code:

- the triggering review author is `coderabbitai` or `coderabbitai[bot]`,
- the pull request title starts with `[parity] `,
- the pull request author is `github-actions[bot]`,
- the head branch starts with `parity/codex-`,
- the pull request has both `automation` and `parity` labels.

If any guard does not hold, leave the workspace unchanged and emit a `noop`.

## Manual Replay Runs

On `workflow_dispatch`, determine the target PR from
`github.event.inputs.pr_number` or the pull request number in
`github.event.inputs.aw_context`. Use `github.event.inputs.review_id` when it is
set; otherwise use the newest CodeRabbit review on that PR.

Manual replay runs exist to process CodeRabbit comments that were created before
this workflow existed. Treat them the same as a fresh CodeRabbit review event:
inspect the target PR, verify the PR guardrails above, read the selected or
newest CodeRabbit review, and read all unresolved CodeRabbit inline review
comments before editing.

## Task

Use the available GitHub tools to inspect the triggering pull request and the
newest CodeRabbit review on that pull request. Read the review body and all
unresolved CodeRabbit inline review comments.

For each finding:

1. Verify it against the current code before editing.
2. Fix only still-valid issues.
3. Skip invalid or obsolete findings with a brief reason in your final output.
4. Keep the change minimal and scoped to the reviewed parity PR.

Do not apply CodeRabbit suggestions mechanically when the current source proves
the suggestion is wrong or stale.

## Validation Before Safe Output

Do not request `push_to_pull_request_branch` until validation passes and hosted
CI is expected to pass.

Run the focused tests that cover every touched surface. If the change is not
pure documentation, also run:

```bash
dotnet run --project src/JKToolKit.CodexSDK.UpstreamGen --configuration Release -- check
dotnet test JKToolKit.CodexSDK.sln --configuration Release
```

The workflow enforces this after the agent runs: if a safe-output write is
requested, post-steps materialize the proposed patch if needed and run restore,
generated DTO check, build, and full tests before safe outputs are processed.
If those checks fail, the workflow run must fail instead of pushing a red PR.

## Output

When changes are needed:

1. Commit the changes locally with a concise message such as
   `fix: address CodeRabbit parity review`.
2. Use `push_to_pull_request_branch` to update the triggering pull request.

Do not use raw `git push`. Do not create a new pull request. Do not print API
keys, virtual token values, endpoint hosts, or other secrets.
