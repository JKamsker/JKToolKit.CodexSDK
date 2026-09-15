# Upstream Sync Runbook

`Upstream Sync (@openai/codex)` synchronizes the latest `@openai/codex` release and completes the entire integration without a manual merge.

## Authentication and secrets

- The workflows use the built-in `GITHUB_TOKEN` by default. Repository Actions settings must allow GitHub Actions to create pull requests.
- `GH_AW_GITHUB_TOKEN` is optional. Configure a repository-owner token with repository and workflow access when owner attribution is needed. The same selection is used for PR creation, merge, and repair dispatch.
- `OPENAI_API_KEY` and `CODEX_LB_BASE_URL` are used by the parity agent workflow.
- `BW_ACCESS_TOKEN` lets post-merge CI retrieve the NuGet API key without storing it in the repository.

Never print or copy secret values into workflow logs. Configure them with `gh secret set` or the GitHub repository settings.

## Success Path

1. The scheduled workflow resolves the stable npm version and checks for its matching Git tag. If the tag is not published yet, it defers to the next run. It creates a PR containing the API pin and submodule update, without requiring DTO generation to work first.
2. The parity agent regenerates DTOs, repairs generator/schema and handwritten SDK drift, validates the result, and updates the same branch.
3. The gate dispatches CI and records the PR's exact head SHA.
4. The gate merges only when that CI run succeeds and the PR head is still the tested SHA.
5. With an owner token, the merge triggers push CI on `master`. With the built-in token, the gate explicitly dispatches CI with `publish=true` and `release_sha=<merge SHA>`. GitHub suppresses push workflows for built-in-token merges; [explicit dispatches still run](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow). Release dispatches are restricted to the default branch and an already merged commit. CI builds that exact commit even if the branch advances. Ordinary manual CI runs do not publish.
6. The gate waits for the post-merge CI run and requires both overall success and a successful `NugetUpload` job. A skipped upload does not count as a release.

## Failure Path

If parity or gated CI fails, `Upstream Sync Repair` starts another parity agent session with the failed run as its first diagnostic input. A successful repair reruns the exact-SHA gate. Repairs are capped at three attempts per repair chain.

An existing PR resumes parity and the CI gate on its current head, preserving earlier repairs. An open exhausted-repairs issue pauses scheduled retries for that version; close it after resolving the blocker to resume.

After the third failed attempt, or if merge/release orchestration itself fails, the automation opens a deduplicated issue containing the PR, branch, and failed run. A NuGet failure after merge always creates an issue because the tested source is already on `master`.

## Local Validation

```bash
python3 -m unittest discover -s .github/scripts -p 'test_*.py' -v
python3 .github/scripts/compile_gh_aw.py .github/workflows/codex-sdk-parity-pass.md
gh aw validate .github/workflows/codex-sdk-parity-pass.md --no-check-update --stats
```
