# <img src="res/logo.png" align="left" width="120" height="120" /> JKToolKit.CodexSDK

[![CI](https://github.com/JKamsker/JKToolKit.CodexSDK/actions/workflows/ci.yml/badge.svg)](https://github.com/JKamsker/JKToolKit.CodexSDK/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/badge/Nuget-Download-blue?logo=nuget)](https://www.nuget.org/packages/JKToolKit.CodexSDK)

<br clear="left"/>

A strongly-typed .NET SDK for interacting with the **Codex CLI** as a local subprocess.

It supports three integration styles:

- **`codex exec`**: start/resume sessions and stream the JSONL session log as typed .NET events
- **[`codex app-server`](docs/AppServer/README.md)**: JSON-RPC server mode (threads/turns/items + streaming deltas + approvals)
- **[`codex mcp-server`](docs/McpServer/README.md)**: MCP tool provider mode (`tools/list`, `tools/call` → `codex` / `codex-reply`)

`JKToolKit.CodexSDK.AppServer` and `JKToolKit.CodexSDK.McpServer` are now **part of the core `JKToolKit.CodexSDK` package** (same assembly / same install).

## Installation

### Prerequisites

  * .NET 10 SDK or later
  * Codex CLI installed and available on PATH (`codex` / `codex.cmd`)

### NuGet

```bash
dotnet add package JKToolKit.CodexSDK
```

> Upgrading from older versions: replace `NCodexSDK` with `JKToolKit.CodexSDK` and remove `NCodexSDK.AppServer` / `NCodexSDK.McpServer` package references (if you had them). The namespaces remain `JKToolKit.CodexSDK.AppServer` and `JKToolKit.CodexSDK.McpServer`.

## Quickstart

### Recommended: `CodexSdk` facade

Use a single, discoverable entry point that exposes all three modes via `sdk.Exec`, `sdk.AppServer`, and `sdk.McpServer`:

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;

await using var sdk = CodexSdk.Create();

// Exec mode (start/resume sessions and stream JSONL events)
await using var session = await sdk.Exec.StartSessionAsync(
    new CodexSessionOptions("<workdir>", "Write a hello world program")
    {
        Model = CodexModel.Gpt51Codex,
        ReasoningEffort = CodexReasoningEffort.Medium
    });

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    // ...
}

// AppServer / McpServer modes
await using var app = await sdk.AppServer.StartAsync();
await using var mcp = await sdk.McpServer.StartAsync();
```

### `codex exec` (direct client)

Fastest way to start a session and stream events:

```csharp
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;

var clientOptions = new CodexClientOptions();
await using var client = new CodexClient(clientOptions);

// Configure the session
var sessionOptions = new CodexSessionOptions("<workdir>", "Write a hello world program")
{
    Model = CodexModel.Gpt51Codex,
    ReasoningEffort = CodexReasoningEffort.Medium
};

// Start and stream
await using var session = await client.StartSessionAsync(sessionOptions, CancellationToken.None);

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default, CancellationToken.None))
{
    switch (evt)
    {
        case AgentMessageEvent msg:
            Console.WriteLine($"Agent: {msg.Text}");
            break;
        case ResponseItemEvent item when item.Payload is MessageResponseItemPayload m:
            Console.WriteLine($"[{m.Role}] {string.Join("\n", m.TextParts)}");
            break;
        case TokenCountEvent tokens:
            Console.WriteLine($"Tokens: {tokens.InputTokens} in, {tokens.OutputTokens} out");
            break;
    }
}
```

## App Server vs MCP Server

Codex offers two stdio JSON-RPC modes that this repo supports:

- [`codex app-server`](docs/AppServer/README.md): best for **deep, event-driven integrations** (threads/turns/items + streaming deltas).
- [`codex mcp-server`](docs/McpServer/README.md): best for using Codex as an **MCP tool provider** (`tools/list`, `tools/call` for `codex` + `codex-reply`).

## [`codex app-server`](docs/AppServer/README.md) (deep integration)

```csharp
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Models;

await using var codex = await CodexAppServerClient.StartAsync(new CodexAppServerClientOptions
{
    DefaultClientInfo = new("my_product", "My Product", "1.0.0")
});

var thread = await codex.StartThreadAsync(new ThreadStartOptions
{
    Model = CodexModel.Gpt51Codex,
    Cwd = "<repo-path>",
    ApprovalPolicy = CodexApprovalPolicy.Never,
    Sandbox = CodexSandboxMode.WorkspaceWrite
});

await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
{
    Input = [TurnInputItem.Text("Summarize this repo.")]
});

await foreach (var e in turn.Events())
{
    if (e is AgentMessageDeltaNotification d) Console.Write(d.Delta);
}

Console.WriteLine($"\nDone: {(await turn.Completion).Status}");
```

## [`codex mcp-server`](docs/McpServer/README.md) (Codex as a tool)

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

## Demo Application

Demos (console apps):

```bash
# Exec demo (default command)
dotnet run --project src/JKToolKit.CodexSDK.Demo -- "Your prompt here"

# Review demo
dotnet run --project src/JKToolKit.CodexSDK.Demo -- review --commit <sha>

# AppServer demos
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-stream --repo "<repo-path>"
dotnet run --project src/JKToolKit.CodexSDK.Demo -- appserver-approval --timeout-seconds 30

# McpServer demo
dotnet run --project src/JKToolKit.CodexSDK.Demo -- mcpserver --repo "<repo-path>"
```

## Documentation

- Start here: [`docs/README.md`](docs/README.md)
- App Server docs: [`docs/AppServer/README.md`](docs/AppServer/README.md)
- MCP Server docs: [`docs/McpServer/README.md`](docs/McpServer/README.md)
- NuGet package README: [`src/JKToolKit.CodexSDK/README.md`](src/JKToolKit.CodexSDK/README.md)

## Troubleshooting

  * **File locked during build:** Stop any running demo processes: `Get-Process JKToolKit.CodexSDK.Demo | Stop-Process -Force`
  * **Session log not found:** Ensure Codex CLI is installed and `%USERPROFILE%\.codex\sessions` exists.
  * **Process launch fails:** Verify `codex` is on your PATH by running `codex --version`.

## License & Contributing

See the repository for license details. Contributions welcome — please open issues or pull requests for bugs, features, or documentation improvements.
