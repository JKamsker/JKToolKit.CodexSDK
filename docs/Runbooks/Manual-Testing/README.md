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

## Where to Find Artifacts

- **Exec session JSONL logs**: by default under `%USERPROFILE%\.codex\sessions\...` (Windows).
- `exec` prints the resolved log file path. Use it with `exec-attach`.

## Quick “Smoke” Sequence

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

