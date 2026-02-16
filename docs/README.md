# Documentation

JKToolKit.CodexSDK ships as **one NuGet package** with three integration modes for the Codex CLI.

## Guides

| Guide | Description |
|-------|-------------|
| [Exec Mode](exec.md) | Launch `codex exec`, stream JSONL events, structured outputs, code reviews |
| [App Server](AppServer/README.md) | `codex app-server` — threads, turns, streaming deltas, approvals, DI, resiliency |
| [MCP Server](McpServer/README.md) | `codex mcp-server` — tool discovery (`tools/list`, `tools/call`), sessions, follow-ups |

## At a Glance

| | Exec | App Server | MCP Server |
|---|---|---|---|
| **Protocol** | JSONL session log (file tail) | JSON-RPC over stdio | JSON-RPC over stdio (MCP) |
| **Streaming** | `IAsyncEnumerable<T>` of session events | Real-time delta notifications | Request/response |
| **Lifecycle** | Session (start / resume) | Thread → Turn | Tool call |
| **Best for** | Scripting, automation, CI | Rich IDE/product integrations | Plugging Codex into MCP toolchains |

## Install

```bash
dotnet add package JKToolKit.CodexSDK
```

> **Prerequisites:** .NET 10+ SDK and Codex CLI on PATH (`codex --version`).

## Upgrading from Split Packages

If you previously installed `NCodexSDK.AppServer` or `NCodexSDK.McpServer`, remove them — everything is now in `JKToolKit.CodexSDK`. The namespaces remain `JKToolKit.CodexSDK.AppServer` and `JKToolKit.CodexSDK.McpServer`.

## Demos

The demo console app covers all three modes:

```bash
# Exec (default)
dotnet run --project src/JKToolKit.CodexSDK.Demo -- "Your prompt here"

# Code review
dotnet run --project src/JKToolKit.CodexSDK.Demo -- review --commit <sha>

# App Server
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-stream --repo "<repo-path>"
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-approval --timeout-seconds 30

# MCP Server
dotnet run --project src/JKToolKit.CodexSDK.Demo -- mcpserver --repo "<repo-path>"
```

## Troubleshooting

- **File locked during build** — stop running demo processes: `Get-Process JKToolKit.CodexSDK.Demo | Stop-Process -Force`
- **Session log not found** — ensure `~/.codex/sessions` exists
- **Process launch fails** — verify `codex` is on your PATH
- Mode-specific issues are covered in each guide above
