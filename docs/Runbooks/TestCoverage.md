# Handwritten SDK coverage

Coverage includes the SDK, Agent Framework adapter, and Semantic Kernel adapter.
Handwritten protocol DTOs and converters count toward the same denominator.
Generated protocol DTOs remain instrumented and are reported separately; they are
not silently discarded. Demo applications and test fixtures are outside this scope.

Run the offline unit and local integration suite from the repository root:

```sh
dotnet test JKToolKit.CodexSDK.sln -c Release \
  --settings tests/coverage.runsettings --collect:"XPlat Code Coverage" \
  --results-directory artifacts/coverage/offline
python3 .github/scripts/report_test_coverage.py \
  artifacts/coverage/offline/*/coverage.json \
  --output artifacts/coverage/summary.json
```

The reporter defaults to a 98% line and 95% branch gate for handwritten code.
It requires measured handwritten lines from all three assemblies, retains
uncovered source locations, and reports each assembly separately. Generated-only,
missing, or empty assembly reports cannot satisfy the gate. For exploratory runs,
explicitly pass `--minimum-line 0 --minimum-branch 0`; this reports measurements
without enforcing the target.

CI collects coverage on Linux, Windows, and macOS and merges results from the same
checkout and Release configuration. Lines and branch arms are unioned, not added,
so repeated reports do not inflate coverage. Publishing requires the combined
coverage gate as well as all platform tests. Each platform's raw reports, test
results, and summary are uploaded even when a test fails.

Only merge reports from the same source and build configuration. Coverlet JSON does
not contain enough provenance to detect arbitrary local reports from different
revisions; CI establishes that constraint through its shared checkout SHA.

## Live integration

Credentialed Codex tests require `CODEX_E2E=1`. Docker transport tests require
`CODEX_DOCKER_E2E=1` and Docker; they use a disposable home without account secrets,
a read-only repository mount, and an ephemeral WebSocket capability token. These
tests exercise stdio, managed-container WebSocket, reattachment, and Docker exec.

The pinned 0.160.0 CLI no longer exposes the legacy MCP server. The current-runtime
test checks that removal; legacy MCP interoperability requires an explicitly
selected compatible executable in `CODEX_E2E_MCP_EXECUTABLE` and an empty temporary
home. A model override is optional through `CODEX_E2E_MODEL`; otherwise live tests
use the configured model. Live turn tests assert successful terminal status and
absence of a turn error, rather than treating any completion notification as success.

```sh
CODEX_E2E=1 CODEX_DOCKER_E2E=1 \
CODEX_E2E_MCP_EXECUTABLE=/path/to/compatible-legacy-codex \
dotnet test tests/JKToolKit.CodexSDK.Tests -c Release \
  --filter 'FullyQualifiedName~.Integration.' \
  --settings tests/coverage.runsettings --collect:"XPlat Code Coverage" \
  --results-directory artifacts/coverage/live
```

Keep the same build/source snapshot when combining live and offline results.
Routine CI does not require model credentials. Mutation testing is deliberately
offline and includes local process, filesystem, and WebSocket integration tests;
see [FullMutationTesting.md](FullMutationTesting.md). The earlier extended state
fuzz campaign is documented in
[State-Fuzzing-2026-10-03.md](Manual-Testing/State-Fuzzing-2026-10-03.md).
