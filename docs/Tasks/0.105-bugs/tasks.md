---
description: "Fix 0.105.0 upstream drift/bugs found by multi-agent audit"
---

# Tasks: 0.105.0 Bug Fixes (Upstream `external/codex`)

**Upstream pin**: `UPSTREAM_CODEX_VERSION.txt` = `0.105.0`  
**Goal**: Fix correctness + robustness gaps identified by a read-only, multi-agent audit against `external/codex` (0.105.0), and add regression tests.

## Phase 0 — Repro + regression tests first

- [x] T001 Create/extend unit tests for `codex review` validation rules (targets + `--title` constraint).
- [x] T002 Create/extend unit tests covering session-id capture regex variants (`session id:` on stderr, JSON mode, non-hex ids).
- [x] T003 Create/extend unit tests for uncorrelated session discovery (baseline race, cancellation semantics, filename timestamp parsing).
- [x] T004 Create/extend unit tests for JSON-RPC concurrency framing (parallel writes must not corrupt JSONL messages).
- [x] T005 Create/extend unit tests for structured-output JSON extraction edge cases (fences + bracket noise + multiple JSON values).
- [x] T006 Create/extend unit tests for JSONL tailer rotation/partial-line behavior (partial line buffering, truncation + BOM, `FileShare.Delete`).

## Phase 1 — Exec review (`codex review`) argv + option validation (P0)

- [x] T010 Align `CodexReviewOptions.Validate()` with upstream `codex review` rules.
  - [x] T010a Require **exactly one** target: `--uncommitted` OR `--base <branch>` OR `--commit <sha>` OR stdin prompt (`-`).
  - [x] T010b Forbid mixing stdin prompt with `--uncommitted/--base/--commit`.
  - [x] T010c Forbid “no target” (all unset) and emit a helpful exception message.
  - [x] T010d Enforce upstream constraint: `Title != null` ⇒ `CommitSha != null` (and update XML docs to clarify intent).
- [x] T011 Update `ProcessStartInfoBuilder.CreateReview(...)` to only emit valid argv combinations.
  - [x] T011a Remove/adjust any logic that currently allows `--commit` + stdin prompt together.
  - [x] T011b Ensure prompt-only reviews use stdin with `-` and that `CodexProcessLauncherIo.WriteOptionalPromptAndCloseStdinAsync(...)` is still used.
- [x] T012 Update tests that currently assert invalid combos (e.g., commit + prompt) are permitted.
  - [x] T012a Update `tests/JKToolKit.CodexSDK.Tests/Unit/ProcessStartInfoBuilderTests.cs` to reflect upstream exclusivity.
  - [x] T012b Add new tests: prompt-only review, title-without-commit throws, no-target throws, prompt+commit throws.
- [x] T013 Update docs examples for `codex review` usage to match the new rules.
  - [x] T013a Update `docs/exec.md` and `src/JKToolKit.CodexSDK/README.md` review examples (if needed).
- [x] T014 Clarify `AdditionalOptions` tokenization for exec/review argv building.
  - [x] T014a Decide whether `AdditionalOptions` entries are strictly “one argv token per entry” (status quo) or whether the SDK should tokenize strings with spaces/quotes.
  - [x] T014b Document the chosen behavior in XML docs + `docs/exec.md` (add a short example).
  - [x] T014c If tokenization is added, add unit tests for quoted/space-delimited cases.

## Phase 2 — `CODEX_HOME` + encoding hardening for all subprocesses (P1)

- [x] T020 Ensure `CodexClientOptions.CodexHomeDirectory` is safe to use across all modes.
  - [x] T020a If `CodexHomeDirectory` is set, create it (or validate+throw) before launching `codex` subprocesses (exec/resume/review/app-server/mcp-server).
  - [x] T020b Add tests for “non-existent CODEX_HOME” to verify we don’t fail before Codex starts (and failures are actionable).
- [x] T021 Pin redirected stdio encodings to UTF-8 for Codex subprocesses.
  - [x] T021a Set `StandardInputEncoding`, `StandardOutputEncoding`, `StandardErrorEncoding` in exec/review `ProcessStartInfo` creation.
  - [x] T021b Set the same encodings in `StdioProcessStartInfoBuilder` (app-server + mcp-server).
  - [x] T021c Add tests that assert UTF-8 is configured (Windows default codepage must not leak into JSON-RPC/JSONL).
- [x] T022 Clarify/guard `SessionsRootDirectory` semantics.
  - [x] T022a Decide whether `SessionsRootDirectory` should implicitly derive/set `CODEX_HOME` (or remain “SDK-only attach/list override”).
  - [x] T022b Update XML docs + `docs/exec.md` accordingly (avoid implying it changes where Codex writes).

## Phase 3 — Session-id capture + session log discovery robustness (P0/P1)

- [x] T030 Broaden session-id capture to match upstream output and future formats.
  - [x] T030a Update `CodexClientRegexes.SessionIdRegex()` to treat the captured id as **opaque** (not `[0-9a-fA-F-]+`).
  - [x] T030b Add regression tests for stderr output format “session id: <id>” and other known variants.
- [x] T031 Decouple stdout/stderr draining from the caller’s cancellation token (deadlock prevention).
  - [x] T031a Ensure drains continue until process exit even if the *startup* `CancellationToken` is canceled after start returns.
  - [x] T031b Add a stress test that writes lots of stdout/stderr to verify no pipe-buffer hang.
- [x] T032 Fix filename→id fallback to handle hyphenated UUIDs correctly.
  - [x] T032a Update `CodexSessionLocatorHelpers.TryExtractSessionIdFromFilePath(...)` to parse `rollout-<timestamp>-<id>.jsonl` and extract the full `<id>`.
  - [x] T032b Add a unit test with a hyphenated UUID filename (assert full UUID is returned).
- [x] T033 Fix cancellation semantics in session-locator wait APIs.
  - [x] T033a `WaitForNewSessionFileAsync(...)` must throw `OperationCanceledException` when the caller cancels (not `TimeoutException`).
  - [x] T033b `WaitForSessionLogByIdAsync(...)` must throw `OperationCanceledException` when the caller cancels (not `TimeoutException`).
  - [x] T033c Add unit tests for cancellation behavior (cancel between polls).
- [x] T034 Rework uncorrelated new-file discovery to avoid baseline/creation-time pitfalls.
  - [x] T034a Avoid “baseline race” where files created after `startTime` are added to `baseline` and then ignored.
  - [x] T034b Stop relying on filesystem creation time for ordering/filtering; prefer parsing the rollout timestamp from the filename and/or verifying by reading `session_meta`.
  - [x] T034c Restrict scans to likely date directories (e.g., `sessions/YYYY/MM/DD`) to avoid O(N) `AllDirectories` walks.
  - [x] T034d Add unit tests with a mocked `IFileSystem` for: baseline race, coarse timestamps, multiple candidates, and “wrong cwd” rejection.
- [x] T035 Harden by-id lookup.
  - [x] T035a Escape/protect `SessionId.Value` when forming glob patterns (avoid wildcard injection).
  - [x] T035b Define deterministic selection when multiple files match (prefer exact filename structure, then most recent by parsed timestamp).
  - [x] T035c Add tests for wildcard chars (if allowed) and multiple matches.

## Phase 4 — JSONL tailer correctness (P0/P1)

- [x] T040 Prevent emitting partial (newline-less) lines.
  - [x] T040a Buffer the final unterminated line at EOF and only yield it once a newline arrives.
  - [x] T040b Add a unit test that appends a JSON object in two writes without a newline; assert the tailer doesn’t yield a fragment.
- [x] T041 Improve truncation/rotation handling.
  - [x] T041a Ensure UTF-8 BOM handling works after `Seek(0)` (recreate `StreamReader` or strip `\uFEFF` on the next line).
  - [x] T041b Add a unit test: truncate+rewrite BOM and verify first post-truncate line parses as JSON.
- [x] T042 Allow rotation/replace on Windows.
  - [x] T042a Open the file with `FileShare.Delete` (and document why).
  - [x] T042b Detect “file replaced” and reopen when necessary (identity/size heuristics).
  - [x] T042c Add a unit test: rename old file, create new file at same path, ensure tail continues on the new file.
- [x] T043 Make `FromByteOffset` safer.
  - [x] T043a Decide whether to resync to next newline after seeking (recommended) or document “offset must be at line boundary”.
  - [x] T043b Add a unit test that seeks mid-line and asserts no invalid JSON fragment is yielded.
- [x] T044 Fix docs/comments that claim timestamp filtering happens in the tailer/parser.
  - [x] T044a Update `src/JKToolKit.CodexSDK/Infrastructure/JsonlTailer.cs` comment about `AfterTimestamp`.

## Phase 5 — Exec JSONL event parsing/mapping drift fixes (P1/P2)

- [x] T050 Treat `exited_review_mode.review_output: null` as valid (upstream allows null).
  - [x] T050a Make `ExitedReviewModeEvent.ReviewOutput` nullable (or introduce a representation for “no structured output”).
  - [x] T050b Update parser to accept null and still emit the event.
  - [x] T050c Add a unit test for `review_output: null`.
- [x] T051 Align `plan_update` parsing with upstream `UpdatePlanArgs`.
  - [x] T051a Add `Explanation` to `PlanUpdateEvent` and parse it.
  - [x] T051b Decide what to do with `Name` (remove, repurpose, or keep as an SDK-only field).
  - [x] T051c Add a unit test parsing `plan_update` with `explanation`.
- [x] T052 Decide how to handle `response_item.payload` being an array (future-proofing).
  - [x] T052a Choose behavior: emit a batch event, emit multiple events, or map to `UnknownCodexEvent` with preserved raw JSON.
  - [x] T052b Add unit tests for the chosen behavior.
- [x] T053 Make parsers resilient to schema drift (avoid `GetString()` throws).
  - [x] T053a Introduce safe helpers (`TryGetString`, `TryGetInt32`, etc.) and update high-risk parsers.
  - [x] T053b Add unit tests where formerly-string fields become non-strings (ensure event is not dropped).
- [x] T054 Make smoke tests forward-compatible.
  - [x] T054a Decide whether smoke parsing should allow `UnknownCodexEvent` (recommended) or whether we must map a broader set of upstream EventMsg variants.
  - [x] T054b Update `tests/JKToolKit.CodexSDK.Tests/Smoke/SessionJsonlParsingSmokeTests.cs` accordingly.

## Phase 6 — Structured outputs robustness (P0/P1)

- [x] T060 Improve tolerant JSON extraction in `CodexStructuredJsonExtractor`.
  - [x] T060a Code fences: scan all fences and select the first/last fence body that parses as JSON (don’t commit to the first generic fence).
  - [x] T060b Bracket scanning: try multiple `{`/`[` candidates (don’t throw on the first invalid candidate when valid JSON appears later).
  - [x] T060c Decide policy for multiple JSON values (first parseable, last parseable, or first that deserializes to `T`).
  - [x] T060d Add unit tests for: “bad first fence, good second fence”, “markdown [link] before JSON”, “{not json} then real JSON”, “unbalanced brace before JSON”, “multiple JSON values”.
- [x] T061 Make exec final-text capture more robust.
  - [x] T061a Add fallbacks to capture the last assistant message from `ResponseItemEvent` (assistant role) and/or `TurnItemCompletedEvent` when `AgentMessageEvent`/`TaskCompleteEvent.LastAgentMessage` aren’t available.
  - [x] T061b Add tests for sessions that lack `agent_message` and/or lack `task_complete.last_agent_message`.
- [ ] T062 Fix resume boundary behavior for structured-output resume.
  - [ ] T062a Prefer byte-offset-based resume (record log size and use `EventStreamOptions.FromByteOffset`) over timestamp filtering.
  - [ ] T062b Add a unit test for events whose timestamp equals the resume boundary (ensure we don’t drop the first “new” event).

## Phase 7 — JSON-RPC core (`JsonRpcConnection`) correctness + concurrency (P0)

- [x] T070 Serialize all outbound JSON-RPC writes (prevent interleaved/corrupted JSONL).
- [x] T070 Serialize all outbound JSON-RPC writes (prevent interleaved/corrupted JSONL).
  - [x] T070a Implement a single outbound writer pump (Channel/queue) or a `SemaphoreSlim` lock for *all* writes (requests, notifications, server-request responses).
  - [x] T070b Ensure server-request responses go through the same serialization path.
  - [x] T070c Add a concurrency stress test (many parallel calls + simulated server requests) asserting every emitted line is valid JSON.
- [ ] T071 Emit spec-compliant JSON-RPC responses.
  - [ ] T071a When `error` is present, omit `result` (don’t emit both).
  - [ ] T071b Add serialization “golden” tests for success vs error responses.
- [ ] T072 Ensure cancellation cannot corrupt the wire.
  - [x] T072a Do not cancel mid-write; cancellation should stop waiting for the response, not interrupt message emission.
  - [ ] T072b Add a test that cancels a request during send/wait and asserts the wire remains well-formed.
- [ ] T073 Keep the read loop responsive under slow handlers.
  - [ ] T073a Dispatch `OnNotification` and `OnServerRequest` handling so the read loop doesn’t stall message processing.
  - [ ] T073b Add a test where a handler delays and unrelated request/response still completes.
- [ ] T074 Make null `params` behavior explicit.
  - [ ] T074a Decide whether to omit `params` when null vs emitting `{}`; implement/document accordingly.
  - [ ] T074b Add a test for `initialized` and other param-less notifications.

## Phase 8 — App-server client lifecycle + event routing fixes (P0/P1)

- [ ] T080 Fix the turn-handle registration race (avoid missing early turn notifications).
  - [ ] T080a Buffer notifications by `turnId` when no `CodexTurnHandle` exists yet (bounded + TTL).
  - [ ] T080b Flush buffered notifications when `RegisterTurnHandle(...)` is called.
  - [ ] T080c Add a unit/integration test with a transcript: `turn/start` response immediately followed by `turn/started` + `item/started` (per-turn stream must observe them).
- [ ] T081 Ensure processes are cleaned up on initialization failure.
  - [ ] T081a Wrap app-server `StartAsync` initialization in `try/catch` and dispose process+RPC on any failure.
  - [ ] T081b Apply the same behavior to DI factories.
  - [ ] T081c Add a test that forces `initialize` failure and asserts no orphan process remains.
- [ ] T082 Enforce a handshake timeout using `StartupTimeout`.
  - [ ] T082a Apply `CancelAfter(StartupTimeout)` around `initialize` + `initialized` (and dispose on timeout).
  - [ ] T082b Add a test where the server never responds to `initialize`.
- [ ] T083 Clarify notification stream semantics.
  - [ ] T083a Decide whether `Notifications()` / `Events()` are single-consumer queues or true pub-sub.
  - [ ] T083b If keeping queues, document “single consumer” prominently in docs + XML docs.
  - [ ] T083c If implementing pub-sub, add fanout and tests.
- [ ] T084 Improve visibility into dropped notifications.
  - [ ] T084a Add drop counters/telemetry (or switch per-turn streams to backpressure) and document tradeoffs.

## Phase 9 — MCP server client protocol fixes + drift tolerance (P0/P1)

- [ ] T090 Enforce handshake timeout and cleanup on failure (same behavior as app-server).
  - [ ] T090a Apply `CancelAfter(StartupTimeout)` around `initialize` + `notifications/initialized`.
  - [ ] T090b Dispose process+RPC on any handshake failure.
  - [ ] T090c Add a test for “server never responds to initialize” (timeout + cleanup).
- [ ] T091 Make default elicitation behavior non-hanging and upstream-compatible.
  - [ ] T091a Decide the default response shape for `elicitation/create` so upstream treats it as a correlated response (avoid JSON-RPC error-only responses if upstream doesn’t correlate them).
  - [ ] T091b Update docs (`docs/McpServer/README.md`) to reflect the actual default behavior.
  - [ ] T091c Add an integration test that triggers an elicitation and verifies the call completes (denied) without a handler.
- [ ] T092 Propagate cancellation to the server (best-effort).
  - [ ] T092a On request cancellation, send `notifications/cancelled` with the request id before dropping `_pending`.
  - [ ] T092b Add a test: start a long `tools/call`, cancel, assert the server is interrupted (or at least receives the cancel notification).
- [ ] T093 Improve MCP parsing robustness and observability.
  - [ ] T093a Implement (or document) pagination support for `tools/list` (`nextCursor`).
  - [ ] T093b Add a “strict mode” (or logging) for unexpected result shapes so drift isn’t silently swallowed.
  - [ ] T093c Concatenate all `content[*].text` blocks when extracting best-effort text (don’t take only the first).
  - [ ] T093d Add additional thread-id fallbacks (`thread_id`, `conversation_id`, top-level) for drift tolerance.
  - [ ] T093e Add unit tests for each parsing variant.
- [ ] T094 Gate outgoing tool arguments against server schema to avoid unknown fields.
  - [ ] T094a Decide whether to preflight `tools/list` and filter outgoing args (e.g., `include-plan-tool`).
  - [ ] T094b Add tests that validate we don’t send args the server doesn’t declare.

## Phase 10 — Validation

- [ ] T100 Run `dotnet test` and ensure all new + existing tests pass.
- [ ] T101 Run the manual testing runbook smoke sequence (`docs/Runbooks/Manual-Testing/README.md`) and ensure no hangs.
