# State fuzzing and mutation testing — 2026-10-03

Environment: Linux x64, .NET 10.0.400, Stryker.NET 5.0.0. Tests exercise the real
SDK state machines and JSON-RPC connection with in-memory transports. They require
no account, model calls, or network service. Randomized tests use fixed seeds and
bounded payload sizes; they are repeatable within the same .NET runtime. Concurrent
task schedules still depend on the scheduler.

## Reproduce

The normal unit suite includes 150 histories per seed. The extended campaign uses
3,000; the environment variable accepts 1–100,000 and defaults to 150.

```sh
CODEX_STATE_FUZZ_CASES=3000 dotnet test tests/JKToolKit.CodexSDK.Tests -c Release \
  --filter 'FullyQualifiedName~FuzzTests' \
  --logger 'trx;LogFileName=state-fuzz-extended.trx' --results-directory artifacts/fuzz

dotnet test tests/JKToolKit.CodexSDK.Tests -c Release \
  --filter 'FullyQualifiedName~MutationRegressionTests'

dotnet tool restore
cd tests/JKToolKit.CodexSDK.Tests
dotnet stryker --config-file stryker-config.json --skip-version-check \
  --output ../../artifacts/mutation
```

For PowerShell, set `$env:CODEX_STATE_FUZZ_CASES = '3000'` before the first command
and remove it with `Remove-Item Env:CODEX_STATE_FUZZ_CASES` before Stryker. Keep the
default history count during mutation testing: hundreds of altered assemblies
each run their covering tests. Do not run another build/test concurrently with
Stryker in the same checkout. Rebuild normally after the mutation campaign.

Reports are local, ignored artifacts: `artifacts/fuzz/state-fuzz-extended.trx`,
`artifacts/mutation/reports/mutation-report.json`, and the adjacent HTML report.
No mutation dashboard upload is configured. The tool version is pinned in
`dotnet-tools.json`; its mutation scope is committed in `stryker-config.json`.

## Extended fuzz coverage

All 26 parameterized test cases passed with 3,000 histories per seed:

| Area | Work performed |
| --- | --- |
| First terminal outcome, repeated disposal/fault/completion, subscription overflow | 18,000 histories × 32 operations = 576,000 modeled operations |
| Concurrent completion/fault/disposal | 3,000 three-way races |
| Disposed managed-turn typed/raw queues | 6,000 randomized capacity/late-notification histories |
| Startup cancellation before/after response; successful/failed interruption | 9,000 schedules |
| Concurrent resumed sessions, previous-generation disposal | 3,000 generations × 12 contenders = 36,000 start attempts |
| Item/usage/diff payload shape and terminal merge | 120,000 structured JSON cases |
| Shuffled RPC responses, cancellation, duplicate replies, unknown IDs | 2,400 connections; 38,400 correlated requests |
| Truncated frames followed by valid replies and EOF | 6,000 connections with 1–11 pending requests each |
| Malformed RPC error-code/message shapes | 6,000 correlated error responses |
| Notification mapping already in flight when shutdown begins | One controlled gate-based race |

Seeds are embedded in each theory: `1`, `7`, `19`, `97`, `43121`, `8675309`, and
`2147483647`, with subsets selected per scenario. Failures in model-based tests
report the seed, case, and operation history. Async response waits and stream
drains have deadlines. Existing parser and fragmented Unicode-frame fuzz tests
remain in the full suite.

The collector payload campaign checks settlement and stable collected results
across malformed optional fields. Separate mutation regressions assert semantic
content: terminal-only items overwrite drafts without changing item order,
final-answer selection, all six token counters in both breakdowns, 64-bit context
windows, absent malformed breakdowns, and preservation of future raw fields.

## Bugs found and fixed

1. **Terminal outcome changed for late subscribers.** A second termination could
   overwrite the observer error even though the collected result had already
   settled. Seed `1` and the first concurrent terminal race reproduced this.
   Observation termination now preserves the first outcome.
2. **Late notifications erased disposed-turn queue contents.** The drop-oldest
   writer treated a closed channel as full and drained events already promised
   to readers. Seeds `7` and `43121`, case zero, reproduced both typed/raw paths.
   Publishing now checks observation state and writes atomically with termination.
3. **An in-flight notification erased global queues after client shutdown.**
   A transformer paused mapping while disposal sealed queues; resumed publication
   drained those queues and could recreate orphan buffers. A controlled race
   reproduced it. Global publication, closure, and routing now share a shutdown
   boundary, and late callbacks stop before writing or buffering.
4. **Malformed RPC error codes stranded requests indefinitely.** Non-number
   `error.code` values made `TryGetInt32` throw after removal from the pending
   request map. Both error-envelope seeds reproduced it at case zero. Parsing now
   checks the JSON kind, uses the existing fallback error code, and guarantees an
   already-correlated request is faulted if response parsing unexpectedly throws.

Each regression was observed failing before its production fix. Truncated JSON
frames are deliberately ignored by the existing protocol contract; that behavior
was verified, not changed into a disconnect policy.

## Mutation campaign

Stryker mutates 14 files covering the thread reservation/ownership layer, turn
handle/subscriptions/collector/usage, core notification routing and buffering,
disconnect/request handling, turn startup, and JSON-RPC dispatch/wire parsing.
It runs the unit suite with coverage-based test selection and six workers.
No mutator categories are excluded. Stryker itself suppresses mutations in blocks
already represented by another mutation and rolls back mutations that cannot
compile. These are reported separately from detected mutations.

The first campaign tested 598 mutants: 351 killed by assertions, 19 timed out,
and 228 survived; another 113 had no test coverage. Its score was 52.04%, including
timeouts as Stryker does. The JSON report records 174 mutations that could not
compile and 252 ignored by the tool (including six pre-excluded declarations
outside the configured scope). Generated/unselected code also produced
instrumentation warnings; the log's repository-wide creation/rollback counts
are not the number of tested mutants.

Survivor inspection led to 22 additional regression cases covering the semantic
content above, legacy completion and queue closure, raw-handle disposal,
early-buffer contents and exact drop counts, pruning multiple expired orphans,
middleware returning an unowned handle, custom RPC serialization, dispatch
callbacks, cancellation wire format, notification EOF, transport disposal, and
remote-error data lifetime. A second full campaign measured those additions.

The second full campaign tested 599 mutants: **388 killed, 19 timed out, 192
survived**, with **112 no-coverage**, **174 compile errors**, and **252 ignored**.
The score increased to **57.24%**; 36 survivors from the first campaign were now
detected. This is a measured improvement, not a claim that all remaining
survivors are equivalent or harmless.

Inspection of the remaining no-coverage paths led to 13 more regression cases:
non-object error payloads, missing results, invalid/unhandled server requests,
transport read failures, irrelevant malformed frames, and process exit reaching
the result, legacy completion, turn queues, subscriptions, and global queues.
The focused follow-up uses the same settings with this scope override:

```sh
# From tests/JKToolKit.CodexSDK.Tests
dotnet stryker --config-file stryker-boundaries-config.json --skip-version-check \
  --output ../../artifacts/mutation-boundaries
```

That focused campaign tested **205 mutants** across disconnect handling and the
three JSON-RPC partial files: **122 killed, 14 timed out, 69 survived**, plus
**29 no-coverage**, **16 compile errors**, and **93 ignored** (again including six
pre-excluded declarations). Its score was **58.12%**. This is a different scope
from the full campaign; scores should not be compared as if the denominator were
unchanged. The reports remain separate under their respective artifact folders.

Remaining survivors include diagnostics/logging and apparently equivalent
conditions, plus real test gaps around channel scheduling options, observer
retention, some custom-mapper/transformer fallbacks, and synchronization-context
behavior. These are recorded as limitations, not silently excluded to raise the
score. Stryker's compile rollback also prevents mutation validation of some
startup branches, even though separate ownership/cancellation fuzz tests execute
them. Timeouts indicate detection of a hang or slowdown, not an assertion failure.

The mutation score is exploratory (`thresholds.break = 0`), not a new coverage
gate. Do not interpret compile errors, ignored mutants, or no-coverage mutants as
proof that tests detect a fault. Fuzzing and mutation testing cannot prove all
state bugs absent, especially arbitrary scheduler interleavings, real process or
network failures, resource exhaustion, and code outside the configured scope.

## Independent review

Two reviewers examined the changes independently with no conversation fork.
The production lifecycle/locking review found no actionable issue. The test/RPC
review identified unbounded test waits; these were bounded, and re-review found
no issues above nit level. Neither reviewer ran builds concurrently with Stryker.

## Final validation

After Stryker restored the assemblies, a non-incremental Release build completed
with zero warnings/errors. The full .NET suite passed **975 tests**, with the
existing **15 opt-in integration tests skipped**; all **43 Python automation
tests** passed. The extended 3,000-history campaign was rerun against the rebuilt
assemblies: **26/26 cases passed** in 11 seconds.

The authenticated `HighLevelSmoke --require-match --live` sample passed against
the bundled 0.160.0 runtime: `READY` with items/usage, resumed `ORCHID`, both
observers receiving eight events, preserved external tool output, actual
`interrupted` terminal status, and corresponding completed/interrupted traces.
The raw TRX files and Stryker reports remain in the artifact paths listed above.
