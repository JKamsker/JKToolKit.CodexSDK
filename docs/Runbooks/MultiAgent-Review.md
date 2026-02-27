# Multi-Agent Read-Only Review Runbook

This runbook describes how to run a **6-agent, read-only** audit of this repository to catch correctness gaps, protocol issues, and upstream drift (especially vs the pinned Codex CLI submodule under `external/codex`).

## Goals

- Find potential bugs, missing validations, race conditions, and protocol mismatches.
- Compare this SDK’s behavior against the pinned upstream (`UPSTREAM_CODEX_VERSION.txt` + `external/codex`).
- Produce **actionable** findings: file references + concrete fix ideas + tests to add.

## Hard rules (strict)

- Agents are **read-only**: they must not modify files (no patches, no formatting, no commits).
- Agents may run **read-only** commands (view files, search, `git show`, etc.). Avoid anything that writes to disk.
- Each agent must focus on its assigned subsystem and return a prioritized list of findings.

## Prerequisites

- Submodule is present: `external/codex/`
- Upstream pin is known: `UPSTREAM_CODEX_VERSION.txt` (example: `0.105.0`)
- You can run searches in the repo (`rg`) and view files.

## Per-run artifact (recommended)

Create a per-run checklist under `.tmp/multi_agent_review/`:

- Path: `.tmp/multi_agent_review/<run_number>.md` (e.g., `0001.md`)

Template:

```markdown
# Multi-Agent Review Run 0001

## Meta
- Date (UTC): <YYYY-MM-DD HH:mm>
- Runner: <name>
- Branch: <branch>
- HEAD: <sha>
- Upstream pin: <contents of UPSTREAM_CODEX_VERSION.txt>
- external/codex HEAD: <sha or tag>

## Agents
- [ ] Exec CLI args + process launch
- [ ] Session id + session discovery
- [ ] JSONL tailing + exec event parsing
- [ ] Structured outputs pipeline
- [ ] App-server JSON-RPC integration
- [ ] MCP server integration

## Findings summary
- P0:
- P1:
- P2:

## Links
- Tasks doc: <path under docs/Tasks/>
```

## Output format for each agent

Require every finding to include:

- **Severity**: `P0` (bug/hang/corruption), `P1` (high risk / cross-platform), `P2` (drift/ergonomics)
- **What breaks**: concrete failure mode
- **Evidence**: file path(s) + line(s) and/or upstream path(s)
- **Suggested fix**: 1–3 sentences
- **Suggested tests**: what test to add (unit/integration)

## Launch 6 agents (prompts)

Run 6 agents in parallel with **high reasoning**, using the prompts below.

Before you start, replace `<PIN>` with the actual value from `UPSTREAM_CODEX_VERSION.txt` (example: `0.105.0`).

### Agent 1 — Exec CLI args + process launch (exec/review)

```text
READ-ONLY CODE REVIEW ONLY (do not modify files; do not use apply_patch; do not run commands that write).

Goal: find potential bugs/gaps in exec-mode process launching vs upstream Codex CLI (external/codex), pinned version <PIN>.

Focus areas:
- CLI argument/flag correctness & ordering for `codex exec`, `codex exec resume`, `codex review`.
- Environment/working directory handling (ProcessStartInfo.WorkingDirectory vs `--cd`/`-C`; `CODEX_HOME` env var).
- Stdin prompt piping (`-`), stdout/stderr draining risks.

Start from:
- `src/JKToolKit.CodexSDK/Infrastructure/ProcessStartInfoBuilder.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/CodexProcessLauncher.cs`
- `src/JKToolKit.CodexSDK/Exec/CodexSessionOptions.cs`
- `src/JKToolKit.CodexSDK/Exec/CodexReviewOptions.cs`
- `src/JKToolKit.CodexSDK/Exec/CodexClientOptions.cs`

Compare with upstream CLI definitions/docs in `external/codex` and identify mismatches, missing required flags, wrong flag names, or incorrect subcommand placement.

Deliverable: prioritized list of concrete issues (with file:line refs) + suggested fix ideas + tests to add.
```

### Agent 2 — Session id capture + session log discovery

```text
READ-ONLY CODE REVIEW ONLY (no edits; no apply_patch; no write commands).

Goal: audit session-id capture + session-log discovery for correctness/robustness vs upstream (<PIN>).

Focus areas:
- Regex/session-id capture from stdout/stderr; failure modes; timeouts; pipe-buffer risks.
- Session log discovery by id and uncorrelated new-file discovery; race conditions; false positives.
- Cross-platform filesystem assumptions (creation time, case sensitivity, file patterns).

Start from:
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexSessionDiagnostics.cs`
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexClientRegexes.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/CodexSessionLocator.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/Internal/CodexSessionLocatorHelpers.cs`
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexSessionsRootResolver.cs`

Cross-check upstream rollout/session filename conventions in `external/codex`.

Deliverable: issues/gaps + risk assessment + recommended fixes/tests.
```

### Agent 3 — JSONL tailing + exec event parsing/mapping

```text
READ-ONLY CODE REVIEW ONLY (no modifications; no apply_patch).

Goal: audit JSONL tailing + exec-event parsing/mapping for correctness and forward compatibility vs upstream (<PIN>).

Focus areas:
- `JsonlTailer` correctness (EOF polling, truncation/replace behavior, encoding/BOM, cancellation).
- Parser robustness: unknown shapes, missing fields, partial lines, large payloads.
- Event filtering semantics (`EventStreamOptions.AfterTimestamp`, `FromByteOffset`, `Follow`, etc).

Start from:
- `src/JKToolKit.CodexSDK/Infrastructure/JsonlTailer.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/JsonlEventParser.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/Internal/JsonlEventParsing/*`
- `src/JKToolKit.CodexSDK/Exec/Notifications/*`
- `src/JKToolKit.CodexSDK/Exec/Protocol/*`

Deliverable: list of potential parsing bugs/compat gaps + suggested fixes/tests.
```

### Agent 4 — Structured outputs pipeline (schema + JSON extraction + retries)

```text
READ-ONLY CODE REVIEW ONLY (no modifications; no apply_patch).

Goal: audit structured outputs pipeline for correctness & corner cases vs upstream (<PIN>).

Focus areas:
- Schema handling: json schema materialization to temp file, encoding, cleanup.
- JSON extraction tolerance: code-fences, braces in strings, multiple JSON values, bracket noise.
- Retry logic correctness and what counts as the “final answer” in exec-mode logs.

Start from:
- `src/JKToolKit.CodexSDK/StructuredOutputs/CodexStructuredJsonExtractor.cs`
- `src/JKToolKit.CodexSDK/StructuredOutputs/CodexStructuredOutputExtensions.cs`
- `src/JKToolKit.CodexSDK/StructuredOutputs/Internal/*`
- `src/JKToolKit.CodexSDK/Exec/Internal/CodexSessionRunner.cs`

Deliverable: edge cases that break today + recommended fixes/tests.
```

### Agent 5 — App-server integration (JSON-RPC over stdio)

```text
READ-ONLY CODE REVIEW ONLY (no edits; no apply_patch).

Goal: audit `codex app-server` integration for protocol correctness, lifecycle safety, and drift vs upstream (<PIN>).

Focus areas:
- JSON-RPC framing (JSONL), request correlation, cancellation behavior, concurrency safety.
- Handshake (`initialize`/`initialized`) and capability negotiation.
- Notification routing (global + per-turn), buffering/drop behavior, thread safety.
- Process lifecycle: shutdown, disconnect detection, resiliency/restart wrappers.

Start from:
- `src/JKToolKit.CodexSDK/AppServer/CodexAppServerClient.cs`
- `src/JKToolKit.CodexSDK/AppServer/Internal/CodexAppServerClientCore*.cs`
- `src/JKToolKit.CodexSDK/Infrastructure/JsonRpc/*`
- `src/JKToolKit.CodexSDK/Infrastructure/Stdio/*`
- `docs/AppServer/README.md`

Compare with upstream app-server behavior in `external/codex`.

Deliverable: prioritized protocol/lifecycle bugs or drift risks + recommended mitigations/tests.
```

### Agent 6 — MCP server integration

```text
READ-ONLY CODE REVIEW ONLY (no edits; no apply_patch).

Goal: audit `codex mcp-server` integration for protocol correctness and robustness to upstream changes (<PIN>).

Focus areas:
- MCP handshake (`initialize` + `notifications/initialized`), JSON-RPC header usage, cancellation.
- `tools/list` and `tools/call` parsing; pagination; strictness/observability on drift.
- Codex tool result parsing (`threadId`, legacy `conversationId`, best-effort text extraction).
- Server-initiated requests / elicitation handling defaults (reject vs hang).

Start from:
- `src/JKToolKit.CodexSDK/McpServer/CodexMcpServerClient.cs`
- `src/JKToolKit.CodexSDK/McpServer/Internal/*`
- `src/JKToolKit.CodexSDK/Infrastructure/JsonRpc/*`
- `src/JKToolKit.CodexSDK/Infrastructure/Stdio/*`
- `docs/McpServer/README.md`

Compare with upstream MCP server implementation in `external/codex`.

Deliverable: concrete parsing/protocol issues + suggested fixes/tests.
```

## Triage + tasking

1. Consolidate findings into P0/P1/P2 buckets.
2. Convert findings into a checkbox task doc under `docs/Tasks/<pin>-bugs/` (example: `docs/Tasks/0.105-bugs/tasks.md`).
3. Prefer adding regression tests **before** fixes for P0/P1 issues (hangs, corruption, cross-platform behaviors).

