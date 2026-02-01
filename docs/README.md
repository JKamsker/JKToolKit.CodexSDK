# Documentation

This repo ships **one NuGet package**: `NCòdexSDK`. It provides three ways to integrate with the Codex CLI:

| Mode | What you get | When to use |
|---|---|---|
| `codex exec` | Start/resume sessions and **stream JSONL session events** | Programmatic control of “normal” Codex runs |
| `codex app-server` | **Threads / turns / items** + **streaming deltas** + server-initiated requests (approvals) | Deep, event-driven product integrations |
| `codex mcp-server` | MCP **`tools/list`** + **`tools/call`** wrappers (`codex`, `codex-reply`) | Treat Codex as a tool provider in an MCP-like architecture |

## Start here

- Root overview + quick examples: [`README.md`](../README.md)
- App Server (`codex app-server`): [`docs/AppServer/README.md`](AppServer/README.md)
- MCP Server (`codex mcp-server`): [`docs/McpServer/README.md`](McpServer/README.md)
- Design notes / historical task docs: [`docs/Tasks`](Tasks)

## Upgrading from split packages

If you previously installed `NCòdexSDK.AppServer` and/or `NCòdexSDK.McpServer`, you can remove them and keep only `NCòdexSDK`. The namespaces remain `NCodexSDK.AppServer` and `NCodexSDK.McpServer`.

## NuGet package README

The README embedded in the NuGet package lives at [`src/NCodexSDK/README.md`](../src/NCodexSDK/README.md).
