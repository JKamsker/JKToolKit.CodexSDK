# JKToolKit.CodexSDK.AppServer

`JKToolKit.CodexSDK.AppServer` is a namespace/module in the main `JKToolKit.CodexSDK` package that integrates with **`codex app-server`**, a long-running **JSON-RPC-over-stdio** mode of the Codex CLI.

See also:

- Docs index: [`docs/README.md`](../README.md)
- MCP Server docs: [`docs/McpServer/README.md`](../McpServer/README.md)
- Core (`codex exec`) docs: [`src/JKToolKit.CodexSDK/README.md`](../../src/JKToolKit.CodexSDK/README.md)

Use it when you need **deep, event-driven integration**:

- “threads / turns / items” lifecycle
- streaming text deltas (token-by-token / chunk-by-chunk)
- server-initiated requests (approvals / interactive flows)

## What Is `codex app-server`?

`codex app-server` runs Codex as a long-lived stdio server that speaks **JSONL-delimited JSON-RPC** messages:

- Each line on stdout is a JSON object (request/response/notification)
- Clients send requests (e.g. `initialize`, `thread/start`, `turn/start`)
- Codex pushes notifications (e.g. `item/agentMessage/delta`, `turn/completed`)

This library turns that protocol into a .NET-friendly API.

## High-Level Concept

There are two primary concepts:

- **Thread**: a conversation container (like “session state”)
- **Turn**: a unit of work inside a thread (a prompt + resulting items/events)

When you start a turn, you typically want to:

1. Start the turn (`turn/start`)
2. Stream events until `turn/completed`
3. Stop or interrupt the turn if needed

JKToolKit.CodexSDK.AppServer provides `CodexTurnHandle` to model that lifecycle.

## How It Works Internally

1. Launches `codex app-server` as a stdio process (`StdioProcess`)
2. Creates a `JsonRpcConnection` (JSONL read loop + request correlation)
3. Performs handshake:
   - `initialize` request
   - `initialized` notification
4. Routes server notifications:
   - to a global stream (`CodexAppServerClient.Notifications()`)
   - to per-turn streams (`CodexTurnHandle.Events()`) keyed by `turnId`

## Public API (Core Types)

- `CodexAppServerClient`
  - `StartAsync(...)` + initialization handshake
  - `StartThreadAsync(...)`, `ResumeThreadAsync(...)`
  - `StartTurnAsync(...)` → returns a `CodexTurnHandle`
  - `CallAsync(...)` escape hatch for forward compatibility
- `CodexTurnHandle`
  - `Events()` → `IAsyncEnumerable<AppServerNotification>`
  - `Completion` → completes when `turn/completed` arrives
  - `InterruptAsync()` → calls `turn/interrupt`

### Typed notifications (initial set)

The library maps a small must-have subset of notifications into typed records:

- `AgentMessageDeltaNotification` (`item/agentMessage/delta`)
- `ItemStartedNotification` (`item/started`)
- `ItemCompletedNotification` (`item/completed`)
- `TurnCompletedNotification` (`turn/completed`)
- `UnknownNotification` fallback for forward-compatibility

## Getting Started

### Prerequisites

- .NET 10 SDK
- Codex CLI installed

### Install

```bash
dotnet add package JKToolKit.CodexSDK
```

### Minimal example (thread + turn + streaming deltas)

```csharp
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Models;

await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
{
    DefaultClientInfo = new("my_app", "My App", "1.0.0")
});

var thread = await codex.StartThreadAsync(new ThreadStartOptions
{
    Cwd = "<repo-path>",
    Model = CodexModel.Gpt51Codex,
    ApprovalPolicy = CodexApprovalPolicy.Never,
    Sandbox = CodexSandboxMode.WorkspaceWrite
});

await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
{
    Input = [TurnInputItem.Text("Summarize this repo.")]
});

await foreach (var e in turn.Events())
{
    if (e is AgentMessageDeltaNotification d)
        Console.Write(d.Delta);
}

var completed = await turn.Completion;
Console.WriteLine($"\nDone: {completed.Status}");
```

## Approvals / Server-Initiated Requests

Codex may send server-initiated requests (for approvals or interactive actions). This add-on exposes a hook:

- `CodexAppServerClientOptions.ApprovalHandler` (`IAppServerApprovalHandler`)

Built-in handlers:

- `AlwaysApproveHandler`
- `AlwaysDenyHandler`
- `PromptConsoleApprovalHandler` (demo-oriented; writes prompts to stderr/console)

If no handler is configured, server requests are rejected with a JSON-RPC error to avoid deadlocks.

## DI Integration

You can register a factory for dependency injection:

```csharp
services.AddCodexAppServerClient(o =>
{
    o.Launch = CodexLaunch.CodexOnPath().WithArgs("app-server");
});
```

Then resolve `ICodexAppServerClientFactory` and call `StartAsync()`.

## Demos

- `src/JKToolKit.CodexSDK.Demo` includes commands that demonstrate:
  - starting the client
  - creating a thread
  - starting a turn
  - printing streaming deltas

Run:

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-stream --repo "<repo-path>"
```

Approval demo (restrictive allow-list):

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-approval --timeout-seconds 30
```

## Troubleshooting

- If you see no events: confirm you called `initialize` + `initialized` (handled by `StartAsync`).
- If Codex exits immediately: check stderr output (the SDK drains stderr to logs; consider raising log level).
- If you hit interactive prompts unexpectedly: configure an `ApprovalHandler` or set `ApprovalPolicy = Never`.
