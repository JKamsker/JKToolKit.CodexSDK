# Manual Testing Runbook (JKToolKit.CodexSDK)

This runbook describes how to manually verify **all major SDK features** end-to-end.

Primary test harness: the demo console app (`src/JKToolKit.CodexSDK.Demo`).

## Table of Contents

- [Checklist](Checklist.md)
- [Exec mode](Exec.md) (`codex exec`, JSONL streaming, attach/resume, structured outputs, `codex review`)
- [App-server mode](AppServer.md) (`codex app-server`, JSON-RPC, threads/turns, notifications, approvals, resiliency, config/skills/apps)
- [MCP-server mode](McpServer.md) (`codex mcp-server`, tools/list/tools/call, sessions, low-level escape hatches)
- [DI + override hooks](DI-and-Overrides.md) (service registration + transformers/mappers)
- [Troubleshooting](Troubleshooting.md)

## Prerequisites

1. **.NET SDK**: repo targets `.NET 10` (e.g. `dotnet --version` should be `10.x`).
2. **Codex CLI installed**: `codex --version` must work.
3. **Authentication** (only required for some features):
   - Check: `codex login status`
   - If you need remote skills / hazelnut scopes: run `codex login` and complete the flow.

## Safety / Process Handling

- Prefer **Ctrl+C** to stop a demo command.
- Avoid killing generic `codex` processes globally (it may stop unrelated sessions).
- If you must terminate something, target the **demo process** (or the specific PID) rather than `codex` broadly.

## Per-run Task Document (Required)

For **every** manual testing run, create a new “task document” under:

`/.tmp/manual_testing/<run_number>.md`

Use a monotonically increasing, zero-padded run number (e.g. `0001.md`, `0002.md`, …).

> These files are intended to be **local run artifacts** (they live under `.tmp` on purpose).

### Strict format

Create the file using the exact structure below, then update it as you execute tests.

- Every test case is a **checkbox** item.
- After each test case, mark it as either:
  - **passed**: set the checkbox to `[x]` and set status to `[PASS]`
  - **failed**: set the checkbox to `[x]` and set status to `[FAIL]` and add the required failure details

Template (copy/paste):

```markdown
# Manual Testing Run <RUN_NUMBER>

## Meta
- Date (UTC): <YYYY-MM-DD HH:mm>
- Tester: <name>
- Branch: <branch>
- HEAD: <sha>
- Codex CLI: <codex --version>
- .NET SDK: <dotnet --version>
- OS: <Windows/macOS/Linux + version>

## Status rules (strict)
- `[ ] [PENDING] ...` = not executed yet
- `[x] [PASS] ...`    = executed and passed
- `[x] [FAIL] ...`    = executed and failed (must include a Failure block)

## Test cases
- [ ] [PENDING] TC01 - Build + unit tests (`dotnet test`)
- [ ] [PENDING] TC02 - Exec: start/stream/resume (`demo exec`)
- [ ] [PENDING] TC03 - Exec: list sessions (`demo exec-list`)
- [ ] [PENDING] TC04 - Exec: attach to JSONL (`demo exec-attach`)
- [ ] [PENDING] TC05 - Structured output pipeline (`demo structured-review`)
- [ ] [PENDING] TC06 - Non-interactive review (commit scope) (`demo review --commit <sha>`)
- [ ] [PENDING] TC07 - App-server: stream deltas (`demo appserver-stream`)
- [ ] [PENDING] TC08 - App-server: typed + raw notifications (`demo appserver-notifications`)
- [ ] [PENDING] TC09 - App-server: steer + interrupt (`demo appserver-turn-control`)
- [ ] [PENDING] TC10 - App-server: approval handler (`demo appserver-approval`)
- [ ] [PENDING] TC11 - App-server: thread lifecycle commands (`demo appserver-thread ...`)
- [ ] [PENDING] TC12 - App-server: skills + apps (`demo appserver-skills-apps`)
- [ ] [PENDING] TC13 - App-server: config read (`demo appserver-config`)
- [ ] [PENDING] TC14 - App-server: config write (temp CODEX_HOME) (`demo appserver-config-write`)
- [ ] [PENDING] TC15 - App-server: MCP management (`demo appserver-mcp`)
- [ ] [PENDING] TC16 - App-server: fuzzy search (experimental) (`demo appserver-fuzzy --experimental-api`)
- [ ] [PENDING] TC17 - App-server: review/start (`demo appserver-review`)
- [ ] [PENDING] TC18 - App-server: resilient wrapper (`demo appserver-resilient-stream --restart-between-turns`)
- [ ] [PENDING] TC19 - MCP-server: tools + session + reply (`demo mcpserver`)
- [ ] [PENDING] TC20 - MCP-server: low-level escape hatches (`demo mcpserver --low-level`)
- [ ] [PENDING] TC21 - DI + override hooks (scratch app) (`DI-and-Overrides.md`)

## Failures
<!--
For each failed test case, append a block like:

### TCxx - <short title>
- Command:
- Expected:
- Actual:
- Error/output:
- Notes:
-->

## Summary
- Overall: <PASS|FAIL>
- Notes: <optional>
```

### Recommended workflow

1. Create the file:

```powershell
New-Item -ItemType Directory -Force .tmp/manual_testing | Out-Null

# Example run number:
Set-Content .tmp/manual_testing/0001.md -Value "# Manual Testing Run 0001`n"
```

2. Paste the template above into the new file and fill in **Meta**.
3. Execute tests using the sections below / linked pages.
4. After each test case:
   - flip it to `[x] [PASS] ...`, or
   - flip it to `[x] [FAIL] ...` and add an entry under **Failures**.

## Where to Find Artifacts

- **Exec session JSONL logs**: by default under `%USERPROFILE%\.codex\sessions\...` (Windows).
- `exec` prints the resolved log file path. Use it with `exec-attach`.

## Quick “Smoke” Sequence

Before you start, create your per-run task file (see: [Per-run Task Document](#per-run-task-document-required)).

Run these from the repository root:

```powershell
dotnet test

# Exec: start + stream + resume
dotnet run --project src/JKToolKit.CodexSDK.Demo -- exec --prompt "Say 'ok' and nothing else." --reasoning low
dotnet run --project src/JKToolKit.CodexSDK.Demo -- exec-list --limit 3

# App-server: streaming + notifications + steering/interrupt + approval handler
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-stream --timeout-seconds 60
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-notifications --timeout-seconds 60
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-turn-control --timeout-seconds 60
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-approval --timeout-seconds 120

# MCP: tool discovery + low-level calls
dotnet run --project src/JKToolKit.CodexSDK.Demo -- mcpserver --low-level --prompt "Say hi." --followup "Say bye."
```

Expected outcomes:

- Commands **complete** (no hangs).
- Exec prints a **session id** and **log path**, then shows streamed events.
- App-server demos print `Done: completed` (or `Done: interrupted` for turn-control).
- Approval demo creates a temp `test.txt`.
