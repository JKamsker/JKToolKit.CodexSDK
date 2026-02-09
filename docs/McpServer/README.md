# JKToolKit.CodexSDK.McpServer

`JKToolKit.CodexSDK.McpServer` is a namespace/module in the main `JKToolKit.CodexSDK` package that integrates with **`codex mcp-server`**, exposing Codex as a minimal **MCP tool provider** over stdio JSON-RPC.

See also:

- Docs index: [`docs/README.md`](../README.md)
- App Server docs: [`docs/AppServer/README.md`](../AppServer/README.md)
- Core (`codex exec`) docs: [`src/JKToolKit.CodexSDK/README.md`](../../src/JKToolKit.CodexSDK/README.md)

Use it when you want to treat Codex as “just another tool” in an MCP-like architecture:

- list tools (`tools/list`)
- call tools (`tools/call`)
- specifically, call Codex’s built-in tools:
  - `codex` (start a session)
  - `codex-reply` (continue a session by thread id)

## What Is `codex mcp-server`?

`codex mcp-server` runs as a long-lived stdio server that speaks **JSON-RPC 2.0** and implements MCP methods.

From the client perspective:

1. Connect over stdio
2. MCP handshake (`initialize` + `notifications/initialized`)
3. Discover tools (`tools/list`)
4. Call tools (`tools/call`)

The server exits when the client closes the downstream connection (stdin/stdout).

## High-Level Concept

Unlike `app-server` (threads/turns/items with rich event notifications), MCP mode is primarily:

- **request/response**
- tool discovery + tool invocation

JKToolKit.CodexSDK.McpServer provides:

- strongly typed wrappers for the two Codex-specific tools
- a low-level escape hatch (`CallAsync` / `CallToolAsync`) for forward compatibility

## How It Works Internally

1. Launches `codex mcp-server` as a stdio process (`StdioProcess`)
2. Creates `JsonRpcConnection` with `IncludeJsonRpcHeader = true`
3. Performs MCP handshake:
   - `initialize`
   - `notifications/initialized`
4. Implements wrappers around:
   - `tools/list`
   - `tools/call`
5. Parses “Codex tool results” and extracts:
   - `threadId` from `structuredContent.threadId` (or legacy `conversationId`)
   - best-effort text from MCP `content` blocks

## Public API (Core Types)

- `CodexMcpServerClient`
  - `ListToolsAsync()`
  - `StartSessionAsync(CodexMcpStartOptions)` → calls tool `codex`
  - `ReplyAsync(threadId, prompt)` → calls tool `codex-reply`
  - `CallToolAsync(...)` and `CallAsync(...)` escape hatches

Models:

- `McpToolDescriptor`
- `CodexMcpSessionStartResult` / `CodexMcpReplyResult`
- `CodexMcpStartOptions`

## Getting Started

### Install

```bash
dotnet add package JKToolKit.CodexSDK
```

```csharp
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.Models;

await using var codex = await CodexMcpServerClient.StartAsync(new CodexMcpServerClientOptions());

var tools = await codex.ListToolsAsync();

var run = await codex.StartSessionAsync(new CodexMcpStartOptions
{
    Prompt = "Run tests and summarize failures.",
    Cwd = "<repo-path>",
    Sandbox = CodexSandboxMode.WorkspaceWrite,
    ApprovalPolicy = CodexApprovalPolicy.Never
});

Console.WriteLine(run.Text);

var followUp = await codex.ReplyAsync(run.ThreadId, "Now propose fixes.");
Console.WriteLine(followUp.Text);
```

## Elicitations / Server-Initiated Requests

MCP servers may send client-directed requests (often related to approvals/elicitation).

This add-on exposes:

- `CodexMcpServerClientOptions.ElicitationHandler` (`IMcpElicitationHandler`)

If not configured, server requests are rejected with a JSON-RPC error (safe default; avoids hanging).

## DI Integration

```csharp
services.AddCodexMcpServerClient(o =>
{
    o.Launch = CodexLaunch.CodexOnPath().WithArgs("mcp-server");
});
```

Then resolve `ICodexMcpServerClientFactory` and call `StartAsync()`.

## Demos

- `src/JKToolKit.CodexSDK.Demo` has a `mcpserver` command that lists tools, starts a session, and sends a follow-up via `codex-reply`.

Run:

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- mcpserver --repo "<repo-path>"
```

## Troubleshooting

- If `tools/list` returns empty: verify the server completed handshake and that Codex CLI supports `mcp-server`.
- If you get approvals/elicitation requests: configure `ElicitationHandler` or set `ApprovalPolicy = Never`.
- If the process exits early: check stderr output; ensure the CLI is present on PATH.
