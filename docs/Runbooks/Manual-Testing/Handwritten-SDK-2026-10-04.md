# Handwritten SDK validation — 2026-10-04

This pass covers the complete handwritten SDK, Agent Framework adapter, and
Semantic Kernel adapter, including handwritten protocol models and converters.
Generated protocol DTOs and build-generated regular expressions are reported
separately. Demo applications and test fixtures are outside the coverage target.

## Coverage and integration

The cross-platform Release run for PR head `9728116` passed on Linux, Windows, and
macOS. CI tested the synthetic merge `dd7cb35270b8334819975122e35cf82db0383841`:
[CI run](https://github.com/JKamsker/JKToolKit.CodexSDK/actions/runs/37173549601).
The same-revision reports union covered lines and branch arms; they do not add
duplicate observations together.

| Handwritten scope | Lines | Branches |
|---|---:|---:|
| SDK | 19,984 / 20,294 (98.47%) | 9,161 / 9,470 (96.74%) |
| Agent Framework | 1,105 / 1,109 (99.64%) | 627 / 634 (98.90%) |
| Semantic Kernel | 131 / 131 (100.00%) | 91 / 92 (98.91%) |
| Combined | 21,220 / 21,534 (98.54%) | 9,879 / 10,196 (96.89%) |

Generated protocol DTOs measured 135 / 2,979 lines (4.53%) and 80 / 1,434
branches (5.58%). Build-generated coverage has its own report category and does
not inflate either handwritten or protocol coverage. CI gates handwritten
coverage at 98% lines and 95% branches, and requires all three assemblies.

The local Release suite passed 3,759 tests with 17 explicitly gated live skips;
78 Python automation tests passed. A separate pinned 0.160.0 integration run
passed all 61 tests with no skips, including Docker stdio/WebSocket/reattachment
and explicitly selected legacy MCP compatibility. Docker used a disposable home
without account credentials, a read-only repository mount, and an ephemeral
WebSocket capability token. Current-runtime tests assert the precise removal
diagnostics for obsolete MCP and restricted-read APIs.

The high-level sample passed with an explicit 0.160.0 binary: runtime/server
preflight, collection and usage, resumed conversation, two independent observers,
tool-authority external input, interruption request and terminal outcome, and
tracing. The interruption request raced with successful completion in this run;
it is not evidence that this particular turn was interrupted. Requiring a matching
version also correctly rejected the installed 0.154.0 CLI.

Extended fixed-seed fuzzing passed 26 cases with 3,000 histories per seed:
576,000 modeled lifecycle operations, 120,000 structured payload cases, 36,000
concurrent start attempts, and terminal/cancellation/RPC response permutations.
A separate Linux-only runtime probe sampled 256 process schedules: 83 successes,
173 expected cancellations, no unexpected exceptions, and no observed orphaned
children. Neither campaign proves that every possible interleaving is safe.

## Full mutation campaigns

The full campaigns use the immutable source and test snapshot
`ad759158dd863e92f5e89187c1afc87971264a7e` and Stryker.NET 5.0.0. They include every
handwritten SDK file and both entire adapter projects, Complete mutation level,
and all discovered offline unit/local integration tests. Generated upstream
protocol files are outside the mutation scope. Live model/Docker gates are off;
an isolated executable lookup prevents accidentally launching an installed Codex.

Reports preserve their exact source, test, tool, and effective configuration
hashes. The SDK used 16 workers and the adapters used four in a separate worktree;
only disjoint reports with matching source/test/tool identities are combined.
Later tests and documentation are not retroactively included in these scores.

| Scope | Generated | Killed | Timeout | Survived | Uncovered | Compile-invalid | Ignored | Evaluated | Raw detected |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| SDK | 17,224 | 9,094 | 98 | 2,270 | 334 | 2,966 | 2,462 | 77.92% | 53.37% |
| Agent Framework | 903 | 573 | 0 | 74 | 4 | 76 | 176 | 88.02% | 63.46% |
| Semantic Kernel | 133 | 96 | 0 | 0 | 1 | 7 | 29 | 98.97% | 72.18% |
| Combined | 18,260 | 9,763 | 98 | 2,344 | 339 | 3,049 | 2,667 | 78.61% | 54.00% |

All three profiles finished with zero RuntimeError or Pending mutants.
[Machine-readable counts, fingerprints, configurations, and report hashes](Handwritten-SDK-2026-10-04.mutation.json) accompany this report.

Evaluated score is `(Killed + Timeout) / (Killed + Timeout + Survived + NoCoverage)`.
Raw detected score divides by all generated mutants, retaining compile failures
and ignored mutations in that denominator. Compiler-invalid mutations are not
kills; Stryker can roll back other mutations in a method after a compile failure.
Automatically ignored block mutations are also separate. Timeouts do not identify
the assertion that would have detected a mutation. Surviving scheduling changes
are not declared universally equivalent merely because these tests passed.

The Agent Framework survivor review added four behavioral tests for empty
standalone/factory message lists and preservation of an empty tool result before
following content. Replaying exact mutants 227, 272, and 879 separately against
those tests fails the intended assertions; the restored adapter suite passes.
These later tests do not change the frozen full score. Additional lifecycle
replays did not reproduce a production defect.

Later SDK follow-ups also detected 45 distinct selected facade/runtime/telemetry
mutations, 32 catalog/thread parsing mutations, eight filesystem validation
mutations, and eight lifecycle mutations. The latter cover terminal-reference
release, approval cancellation, backoff schedules/caps, and discovery deadlines.
Each replay changes one exact historical mutation and restores original source;
these results are separate evidence and do not recompute the full score. No new
production defect was reproduced during this final bounded survivor pass.

The historical focused campaigns retain their own snapshots and scores. They
overlap one another and the full campaign, so their counts are not added to the
full results. Subsequent selective survivor replays establish only the behavior
of the specific mutations replayed.

## Review and reproducibility

Independent agents reviewed contracts, lifecycle handling, tests, and measurement
tooling without inherited conversation context. Substantive findings were fixed
and re-reviewed. Reproduced fixes include sandbox variant serialization, disposed
and canceled RPC requests, process/restart/exit races, cleanup of processes and
temporary files, nullable schemas, malformed input, and adapter contracts.

The mutation runner now rebuilds all references before each profile and archives
stale Stryker backups. This prevents aborted older runs from restoring binaries
from another revision. The recorded full campaign predates that runner repair;
separate binary/source checks establish its actual SDK/adapter inputs. The adapter
profiles restored their original binaries. A canceled duplicate adapter build
removed SDK test output after the completed SDK report; an uninstrumented rebuild
then verified matching SDK/adapter source and test binaries. Completed report
provenance was unaffected. Production source and tests were not modified during
the full campaigns.

See [coverage commands and scope](../TestCoverage.md),
[full mutation commands and score accounting](../FullMutationTesting.md), and
[the extended fuzz runbook](State-Fuzzing-2026-10-03.md).
