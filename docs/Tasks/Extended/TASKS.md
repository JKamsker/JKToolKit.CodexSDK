# Extended: `codex app-server` + `codex mcp-server` Support

Source spec: `docs/Tasks/Extended/Spec.md`

## Milestone 1 — Shared core

- [x] Add `StdioProcess` host (stdout JSONL reader, stderr drain, graceful shutdown)
- [x] Add `ProcessLaunchOptions` + `CodexLaunch` helpers (supports `npx -y codex …` style)
- [x] Add `JsonRpcConnection` (request/response, notifications, server requests, include/omit `jsonrpc`)
- [x] Add internal routing/backpressure via `System.Threading.Channels`

## Milestone 2 — App-server minimal happy path

- [x] Add `JKToolKit.CodexSDK.AppServer` project + wire into `JKToolKit.CodexSDK.sln`
- [x] Implement `CodexAppServerClient` (start, initialize+initialized, `thread/start`, `turn/start`, `turn/interrupt`, escape hatch)
- [x] Implement typed notification mapping + unknown fallback
- [x] Implement per-turn streaming via `CodexTurnHandle` (`Events`, `Completion`, `InterruptAsync`)
- [x] Add approvals hook (`IAppServerApprovalHandler`) + built-ins (always approve/deny; console prompt demo-only)
- [x] Add `JKToolKit.CodexSDK.AppServer.Demo` console app

## Milestone 3 — MCP server minimal happy path

- [x] Add `JKToolKit.CodexSDK.McpServer` project + wire into `JKToolKit.CodexSDK.sln`
- [x] Implement MCP handshake + minimal methods (`tools/list`, `tools/call`)
- [x] Implement `CodexMcpServerClient` wrapper methods (`ListToolsAsync`, `StartSessionAsync`, `ReplyAsync`, escape hatch)
- [x] Add elicitation handling (`IMcpElicitationHandler`) with safe defaults
- [x] Add `JKToolKit.CodexSDK.McpServer.Demo` console app

## Milestone 4 — Unified config enums

- [x] Add `CodexApprovalPolicy` + `CodexSandboxMode` value objects and protocol converters
- [x] Use unified config types across exec/app-server/mcp where applicable

## Milestone 5 — Tests

- [x] Unit tests for `JsonRpcConnection` (correlation, notifications, server requests, cancellation/shutdown, parse errors)
- [x] Contract tests for app-server notification mapping (JSONL fixtures)
- [x] Contract tests for MCP tool calls (`tools/list` + `tools/call` result parsing)
- [x] Optional E2E tests for app-server + MCP guarded by env vars (skip by default)

## Milestone 6 — Docs + schema workflow

- [x] Update `README.md` with app-server + mcp-server usage and decision guide
- [x] Add schema generation helper script (manual, not part of build)
- [x] Document generated DTO strategy (optional) and keep public API handwritten
