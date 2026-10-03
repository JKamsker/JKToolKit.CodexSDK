# High-level SDK validation — 2026-10-03

Environment: Linux x64, .NET 10.0.400, SDK protocol pin 0.160.0. Existing PATH
installation: codex-cli 0.154.0. Live tests used the opt-in Linux runtime package
built from the committed npm integrity lock, the existing authenticated account,
a read-only sandbox, and no tool execution.

## Reproduction

```sh
python3 scripts/pack-codex-runtime.py --rid linux-x64
dotnet restore samples/HighLevelSmoke -p:UsePinnedRuntime=true \
  --configfile runtime/Smoke.NuGet.Config
dotnet run --project samples/HighLevelSmoke -c Release \
  -p:UsePinnedRuntime=true --no-restore -- --require-match --live
dotnet publish samples/HighLevelSmoke -c Release \
  -p:UsePinnedRuntime=true --no-restore -o artifacts/smoke-publish
dotnet artifacts/smoke-publish/HighLevelSmoke.dll --require-match
```

The `--live` flag performs authenticated model calls. Omit it for a binary-only
smoke test. Set `CODEX_SMOKE_BINARY` to test an explicit executable override.

## Observed results

- PATH-only preflight reported actual 0.154.0, expected 0.160.0, match false,
  with `SDK generated for 0.160.0, found CLI 0.154.0.`
- Restored consumer selected `codex-runtime/0.160.0/linux-x64/bin/codex`, reported
  match true, and initialized app-server 0.160.0 successfully.
- Collected run returned `READY`, three typed items, and non-null token usage.
- Resume remembered `ORCHID` from the first turn.
- Two independently registered observers each received eight notifications
  while the same turn was collected separately.
- External-message run completed and included a `functionCallOutput` item.
  Its response retained the preceding user instruction (`ORCHID`), consistent
  with the external content's lower authority.
- Explicit interruption returned terminal status `interrupted`.
  The live sample also accepts a validated `completed` outcome if the server
  finishes before interruption takes effect; an observed `no active turn`
  response does not by itself prove interruption or fail a completed turn.
- Activity listener observed completed and interrupted turn spans.
- Published consumer selected its own bundled 0.160.0 executable successfully.
- Explicit 0.154.0 executable override took precedence over the published bundle
  and produced the expected mismatch diagnostic.
- Temporary unrecorded API addition failed the build with RS0016. A temporary
  shipped declaration absent from code failed with RS0017. Both probes were
  removed, preserving the actual API baseline.

Automated companion checks cover middleware ordering, parent activity propagation,
content exclusion from trace tags, overlapping runs across resumed sessions,
early completion, queue overflow and partial-result reporting, duplicate items,
64-bit usage counters, cancellation, disposal, transport failure, independent
observers, randomized parser input, and fragmented Unicode frames. The full local
suite passed; opt-in legacy integration tests remain separately gated. Upstream
schema generation check and all 41 automation tests passed.

After the parallel review fixes, the live sample passed again: `READY`, resumed
`ORCHID`, independent observers (10 events each in this run), preserved external
tool output, and terminal interruption. The full .NET suite passed 893 tests
(15 separately gated integration tests skipped). Added regressions cover canceled
startup, middleware failure cleanup, shutdown during startup, turn payload
collection, custom DI executable resolution, version-scoped runtime caches, and
build/publish executable permissions for other users. Delayed turn and detached
review startup tests also verify that early items and terminal notifications
survive the normal orphan-buffer TTL while startup is pending.

Windows/macOS execution and all six RID packages are validated by PR CI. This
local record does not claim local execution of non-Linux binaries.
