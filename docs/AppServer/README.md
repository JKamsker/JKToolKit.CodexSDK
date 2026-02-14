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

## Stable vs Experimental (Upstream Compatibility)

Newer upstream Codex builds increasingly gate fields/methods behind an initialize-time capability:

- `initialize.params.capabilities.experimentalApi = true`

This SDK is **stable-only by default** and avoids sending known experimental-gated fields unless explicitly requested.

### Stable-only subset (works without experimental opt-in)

- `initialize` + `initialized`
- `thread/start` (stable subset)
- `thread/resume` by `threadId` (stable subset)
- `turn/start` (stable subset; no `collaborationMode`)
- `turn/interrupt`

### Known experimental-gated fields (blocked by default)

If you set any of these while experimental opt-in is disabled, the SDK throws `CodexExperimentalApiRequiredException`
before sending the request:

- `thread/resume.history`
- `thread/resume.path`
- `turn/start.collaborationMode`
- `thread/start.experimentalRawEvents` (when `true`)

### Enabling experimental API opt-in (advanced)

If you need experimental-gated fields/methods, opt in explicitly at initialize time:

```csharp
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.Initialize;

await using var client = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
{
    Capabilities = new InitializeCapabilities
    {
        ExperimentalApi = true,

        // Optional: reduce notification volume (method names are upstream-defined).
        OptOutNotificationMethods = new[]
        {
            "item/agentMessage/delta"
        }
    }
});
```

Notes:

- Experimental surfaces are upstream-unstable and may break across Codex updates.
- If your Codex app-server is too old to understand a capability field, initialize may fail with a JSON-RPC invalid-params error.

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

## Resiliency (auto-restart)

If you want the SDK to automatically restart `codex app-server` when the subprocess dies, register the resilient factory:

```csharp
using JKToolKit.CodexSDK.AppServer.Resiliency;

services.AddCodexResilientAppServerClient(o =>
{
    // Safe defaults:
    // - AutoRestart = true
    // - RetryPolicy = NeverRetry (user decides what to retry)
});
```

Then resolve `ICodexResilientAppServerClientFactory` and call `StartAsync()`:

```csharp
var resilientFactory = sp.GetRequiredService<ICodexResilientAppServerClientFactory>();
await using var codex = await resilientFactory.StartAsync();

var thread = await codex.StartThreadAsync(new ThreadStartOptions { /* ... */ });
```

Notes:

- The resilient client may emit a local marker notification `client/restarted` (`ClientRestartedNotification`) after restarts.
- In-flight turns cannot be safely resumed mid-flight; failures surface as `CodexAppServerDisconnectedException` (includes exit code + best-effort stderr tail). Your retry policy can decide whether to start a new turn, re-resume a thread, etc.

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
- If you see `"<descriptor> requires experimentalApi capability"`: the upstream app-server rejected an experimental-gated field/method. Remove the experimental field/method or enable experimental API opt-in via `CodexAppServerClientOptions.Capabilities.ExperimentalApi = true`.
- If the Codex subprocess dies mid-turn: the SDK now faults the global notification stream and any in-progress `CodexTurnHandle` streams/completions with `CodexAppServerDisconnectedException` (includes exit code and a best-effort stderr tail).
