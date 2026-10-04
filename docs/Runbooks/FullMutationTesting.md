# Full handwritten SDK mutation campaigns

Run from a clean checkout with Python 3 and the .NET SDK in `global.json`:

```sh
python3 .github/scripts/run_full_mutation.py \
  --dotnet "$HOME/.dotnet/dotnet" \
  --output /tmp/codex-mutation-campaign
```

The runner restores the pinned local Stryker tool and runs the SDK, Agent Framework,
and Semantic Kernel profiles sequentially. Select a subset with repeatable
`--profile sdk`, `--profile agentframework`, or `--profile semantickernel`.
Each profile uses four workers, Complete mutation level, per-test coverage, the
Release configuration, and stops if the unmutated initial test run fails. It does
not exclude any mutator categories. The output directory must be new.

All discovered tests are eligible except `SourceFileSizeGuardTests`, a repository
layout check that does not test runtime behavior. Unit tests and local process,
filesystem, and WebSocket integration tests remain enabled. The runner removes
`CODEX_E2E` and `CODEX_DOCKER_E2E` so credentialed live integration tests remain
skipped, and exports `DOTNET_ROOT` for local fixture apphosts.

The SDK profile includes every handwritten C# file, including handwritten protocol
converters and DTOs. Only `Generated/**/*.cs` is outside that profile; generated
upstream protocol code is **not measured** by this campaign and has no claimed
mutation score. Both adapter profiles include every C# file in their projects.

## Evidence and scores

Each run writes the original JSON report, a log, and a provenance sidecar containing
the commit, source and test file hashes, exact configuration, and pinned tool
manifest. The runner checks these inputs again after the run and rejects a changed
snapshot. Keep these artifacts together; do not commit the large raw reports.
The combined `summary.json` contains per-file status counts, survivor/uncovered
triage, and report hashes.

Two scores are intentionally shown:

- Evaluated score: `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`.
- Raw detected score: `(Killed + Timeout) / all generated mutants`, including
  compile errors and ignored mutants in the denominator.

Both use percentages. Neither treats ignored or compile-invalid mutants as killed.
Timeouts remain separately visible because a timeout does not establish which
assertion detected a mutation. Runtime errors, pending results, and unknown statuses
mark the summary incomplete. Empty denominators produce `null`, never 100%.

Stryker can ignore redundant block mutations internally even without configured
exclusions. It can also roll back multiple mutations in a method after a compiler
failure. These remain visible as `Ignored` and `CompileError` with their reported
reasons; a high evaluated score cannot establish mutation adequacy for those areas.
A zero exit code uses a reporting-only break threshold and is not a quality gate.

To summarize existing artifacts captured by this tooling:

```sh
python3 .github/scripts/summarize_mutation.py summarize \
  --input /tmp/codex-mutation-campaign/sdk/reports/mutation-report.json \
          /tmp/codex-mutation-campaign/sdk.provenance.json \
  --output /tmp/codex-mutation-summary.json
```

Repeat `--input REPORT PROVENANCE` for additional reports. Different commits,
source snapshots, test snapshots, or tool manifests remain separate groups.
Overlapping mutated files in the same group are rejected to prevent double
counting. The summarizer verifies mutated source text embedded in the report
against the captured source hashes; it does not infer provenance from the current
checkout or silently attach today's commit to an older report.

Validate the tooling with:

```sh
python3 -m unittest discover -s .github/scripts -p 'test_*mutation.py'
```
