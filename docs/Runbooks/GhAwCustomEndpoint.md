# gh-aw Custom OpenAI Endpoint Runbook

This repository uses normal gh-aw Codex workflows, but committed lockfiles must be generated through the repository wrapper:

```bash
python .github/scripts/compile_gh_aw.py .github/workflows/daily-repo-status.md
```

The wrapper runs `gh aw compile` first, then applies the repository-specific lockfile patch that routes OpenAI-compatible Codex traffic through the load balancer.

## Why The Wrapper Exists

Raw `gh aw compile` regenerates `.lock.yml` files without the private endpoint patch. The wrapper preserves the normal gh-aw generated structure while adding the pieces gh-aw cannot express without committing the endpoint host:

- Reads the endpoint from the repository secret `CODEX_LB_BASE_URL`.
- Adds the endpoint host to AWF network allow-listing at runtime.
- Sets AWF's OpenAI API proxy target to that endpoint.
- Passes `CODEX_LB_BASE_URL` only to the runner-side patch step.
- Adds `--exclude-env CODEX_LB_BASE_URL` so the sandboxed agent does not receive the endpoint value.
- Redacts endpoint values from gh-aw artifacts before upload, including detection artifacts.

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
