# gh-aw Custom OpenAI Endpoint Runbook

This repository uses normal gh-aw Codex workflows, but committed lockfiles must be generated through the repository wrapper:

```bash
python .github/scripts/compile_gh_aw.py .github/workflows/daily-repo-status.md
```

The wrapper runs the installed `JKamsker/gh-aw` compiler, pins generated setup
actions to the corresponding upstream gh-aw-actions release declared in the
wrapper, then applies the repository-specific lockfile patch that routes
OpenAI-compatible Codex traffic through the load balancer.

## Why The Wrapper Exists

Raw `gh aw compile` regenerates `.lock.yml` files without the private endpoint patch. The wrapper preserves the normal gh-aw generated structure while adding the pieces gh-aw cannot express without committing the endpoint host:

- Reads the endpoint from the repository secret `CODEX_LB_BASE_URL`.
- Adds the endpoint host to AWF network allow-listing at runtime.
- Sets AWF's OpenAI API proxy target to that endpoint.
- Passes `CODEX_LB_BASE_URL` only to the runner-side patch step.
- Adds `--exclude-env CODEX_LB_BASE_URL` so the sandboxed agent does not receive the endpoint value.
- Redacts endpoint values from gh-aw artifacts before upload, including detection artifacts.
- Sets Codex reasoning effort to `medium` in each generated Codex configuration block.

The workflow source files select `gpt-5.6-sol` explicitly so repository or
organization model defaults cannot silently move these automations to a
different model. They also use a 2,000 AI-credit ceiling and GitHub `gh-proxy`
mode so full runs are not cut off by the former 1,000-credit default or direct
GitHub API firewall blocks.

The parity workflow also explicitly disables
`GH_AW_CODEX_CONTEXT_REBUILD_CIRCUIT_BREAKER`. In the pinned v0.88.7 harness,
this heuristic aborted useful parity sessions at a rebuild factor of 25,
including runs 35984664354 and 35988724323 on 2026-09-24. Those runs reported
`infrastructure_error` before completing the audit; restarting the same task
hit the same cutoff. The parity agent instead has a 40-minute execution limit
and the existing 2,000-credit ceiling. Sandbox, endpoint redaction, safe-output
validation, and the exact-commit CI/merge gate remain enabled. .NET 10 is
installed before the agent starts as well as before host-side validation.

Upstream sync checks persistent repair state before starting parity. After
three failed repair attempts, it opens an `Upstream sync <version> paused
[fingerprint]` issue keyed to the PR head and automation files. An open matching
issue prevents scheduled syncs from resetting the retry counter. A new PR head
or an automation change permits another chain; after fixing an external service
problem, close the matching pause issue to retry unchanged code. The next
scheduled or manual Upstream Sync run will resume. Active repair runs for the
same PR also defer new sync sessions. A deferred sync is successful, but does
not run the merge gate or publish a package; the pause issue and original failed
runs retain the failure evidence.

The endpoint host is considered secret. Do not write it in workflow YAML, lockfiles, docs, commit messages, logs, or comments. The secret name `CODEX_LB_BASE_URL` is safe to mention.

## Editing gh-aw Workflows

After editing a gh-aw workflow markdown file, compile with the wrapper instead of invoking `gh aw compile` directly:

```bash
python .github/scripts/compile_gh_aw.py .github/workflows/daily-repo-status.md
```

Then validate the workflow:

```bash
gh aw validate .github/workflows/daily-repo-status.md --no-check-update --stats
```

If you accidentally ran raw `gh aw compile`, rerun the wrapper before committing.

## Runtime Behavior

At runtime the workflow still uses the normal gh-aw Codex engine. The generated AWF config is patched on the runner so requests that gh-aw sends to the OpenAI-compatible target go through the load balancer.

The load balancer handles gh-aw model-list probes locally, so `/models` discovery should not create upstream fault records. Actual Codex response traffic is still proxied through the balancer and is attributed to the virtual token configured for the workflow.

## Production Verification

For production verification, filter by the gh-aw virtual token name rather than by all load balancer traffic:

```sql
SELECT
  count(*) AS total_requests,
  count(*) FILTER (WHERE l."Route" ILIKE '%/models%') AS model_requests,
  count(*) FILTER (WHERE l."Route" ILIKE '%/models%' AND (l."Status" >= 400 OR l."ErrorCode" IS NOT NULL)) AS model_faults,
  count(*) FILTER (WHERE l."Status" >= 400 OR l."ErrorCode" IS NOT NULL) AS all_faults,
  count(*) FILTER (WHERE l."Route" ILIKE '%/responses%' AND l."Status" = 200) AS response_successes
FROM request_logs l
JOIN virtual_tokens v ON v."Id" = l."VirtualTokenId"
WHERE v."Name" = 'Jonas-GH-AW'
  AND l."TsUtc" >= now() - interval '20 minutes';
```

Do not query or print virtual token secret values.
