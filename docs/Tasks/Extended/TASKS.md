# Extended: `codex app-server` + `codex mcp-server` Support

Source spec: `docs/Tasks/Extended/Spec.md`

## Milestone 1 — Shared core

- [x] Add `StdioProcess` host (stdout JSONL reader, stderr drain, graceful shutdown)
- [x] Add `ProcessLaunchOptions` + `CodexLaunch` helpers (supports `npx -y codex …` style)
- [x] Add `JsonRpcConnection` (request/response, notifications, server requests, include/omit `jsonrpc`)
- [x] Add internal routing/backpressure via `System.Threading.Channels`

## Milestone 2 — App-server minimal happy path

- [ ] Add `NCodexSDK.AppServer` project + wire into `NCodexSDK.sln`
- [ ] Implement `CodexAppServerClient` (start, initialize+initialized, `thread/start`, `turn/start`, `turn/interrupt`, escape hatch)
- [ ] Implement typed notification mapping + unknown fallback
- [ ] Implement per-turn streaming via `CodexTurnHandle` (`Events`, `Completion`, `InterruptAsync`)
- [ ] Add approvals hook (`IAppServerApprovalHandler`) + built-ins (always approve/deny; console prompt demo-only)
- [ ] Add `NCodexSDK.AppServer.Demo` console app

## Milestone 3 — MCP server minimal happy path

- [ ] Add `NCodexSDK.McpServer` project + wire into `NCodexSDK.sln`
- [ ] Implement MCP handshake + minimal methods (`tools/list`, `tools/call`)
- [ ] Implement `CodexMcpServerClient` wrapper methods (`ListToolsAsync`, `StartSessionAsync`, `ReplyAsync`, escape hatch)
- [ ] Add elicitation handling (`IMcpElicitationHandler`) with safe defaults
- [ ] Add `NCodexSDK.McpServer.Demo` console app

## Milestone 4 — Unified config enums

- [ ] Add `CodexApprovalPolicy` + `CodexSandboxMode` value objects and protocol converters
- [ ] Use unified config types across exec/app-server/mcp where applicable

## Milestone 5 — Tests

- [ ] Unit tests for `JsonRpcConnection` (correlation, notifications, server requests, cancellation/shutdown, parse errors)
- [ ] Contract tests for app-server notification mapping (JSONL fixtures)
- [ ] Contract tests for MCP tool calls (`tools/list` + `tools/call` result parsing)
- [ ] Optional E2E tests for app-server + MCP guarded by env vars (skip by default)

## Milestone 6 — Docs + schema workflow

- [ ] Update `README.md` with app-server + mcp-server usage and decision guide
- [ ] Add schema generation helper script (manual, not part of build)
- [ ] Document generated DTO strategy (optional) and keep public API handwritten
