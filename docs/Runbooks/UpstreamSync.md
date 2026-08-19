# Upstream Sync Runbook

`Upstream Sync (@openai/codex)` synchronizes the latest `@openai/codex` release and completes the entire integration without a manual merge.

## Required Secrets

- `GH_AW_GITHUB_TOKEN` must be a token for the repository owner with repository and workflow access. It makes the sync commit, parity commit, merge, and resulting workflow events belong to that user instead of `github-actions[bot]`.
- `OPENAI_API_KEY` and `CODEX_LB_BASE_URL` are used by the parity agent workflow.
- `BW_ACCESS_TOKEN` lets post-merge CI retrieve the NuGet API key without storing it in the repository.

Never print or copy secret values into workflow logs. Configure them with `gh secret set` or the GitHub repository settings.

## Success Path

1. The scheduled workflow creates an upstream PR with the repository owner as its author.
2. The parity agent updates the same branch with a signed commit made through the owner's token.
3. The gate dispatches CI and records the PR's exact head SHA.
4. The gate merges only when that CI run succeeds and the PR head is still the tested SHA.
5. The merge triggers push CI on `master`. Upstream marker changes count as package changes, so all three NuGet packages are packed and published.
6. The gate waits for the post-merge CI run and reports success only after NuGet publishing succeeds.

## Failure Path

If parity or gated CI fails, `Upstream Sync Repair` starts another parity agent session with the failed run as its first diagnostic input. A successful repair reruns the exact-SHA gate. Repairs are capped at three attempts.

After the third failed attempt, or if merge/release orchestration itself fails, the automation opens a deduplicated issue containing the PR, branch, and failed run. A NuGet failure after merge always creates an issue because the tested source is already on `master`.

## Local Validation

```powershell
python -m pytest .github/scripts/test_upstream_sync_gate.py -q
python .github/scripts/compile_gh_aw.py .github/workflows/codex-sdk-parity-pass.md
gh aw validate .github/workflows/codex-sdk-parity-pass.md --no-check-update --stats
```
