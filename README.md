# <img src="res/logo.png" align="left" width="120" height="120" /> NCodexSDK

[![CI](https://github.com/JKamsker/NCodexSDK/actions/workflows/ci.yml/badge.svg)](https://github.com/JKamsker/NCodexSDK/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/badge/Nuget-Download-blue?logo=nuget)](https://www.nuget.org/packages/NC%C3%B2dexSDK)

<br clear="left"/>

A strongly-typed .NET SDK for interacting with the **Codex CLI** as a local subprocess.

It supports three integration styles:

- **`codex exec`**: start/resume sessions and stream the JSONL session log as typed .NET events
- **`codex app-server`**: JSON-RPC server mode (threads/turns/items + streaming deltas + approvals)
- **`codex mcp-server`**: MCP tool provider mode (`tools/list`, `tools/call` → `codex` / `codex-reply`)

`NCodexSDK.AppServer` and `NCodexSDK.McpServer` are now **part of the core `NCòdexSDK` package** (same assembly / same install).

## Installation

### Prerequisites

  * .NET 10 SDK or later
  * Codex CLI installed and available on PATH (`codex` / `codex.cmd`)

### NuGet

```bash
dotnet add package NCòdexSDK
```

> Upgrading from older versions: remove `NCòdexSDK.AppServer` / `NCòdexSDK.McpServer` package references (if you had them). The namespaces remain `NCodexSDK.AppServer` and `NCodexSDK.McpServer`.

## Quickstart (`codex exec`)

Fastest way to start a session and stream events:

```csharp
using NCodexSDK.Public;
using NCodexSDK.Public.Models;

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
            Console.WriteLine($"Agent: {msg.Content}");
            break;
        case ResponseItemEvent item when item.Payload.Message != null:
            var text = string.Join("", item.Payload.Message.Value.TextParts);
            Console.WriteLine($"[{item.Payload.Message.Value.Role}] {text}");
            break;
        case TokenCountEvent tokens:
            Console.WriteLine($"Tokens: {tokens.InputTokens} in, {tokens.OutputTokens} out");
            break;
    }
}
```

## App Server vs MCP Server

Codex offers two stdio JSON-RPC modes that this repo supports:

- `codex app-server`: best for **deep, event-driven integrations** (threads/turns/items + streaming deltas).
- `codex mcp-server`: best for using Codex as an **MCP tool provider** (`tools/list`, `tools/call` for `codex` + `codex-reply`).

## `codex app-server` (deep integration)

```csharp
using NCodexSDK.AppServer;
using NCodexSDK.AppServer.Notifications;
using NCodexSDK.Public.Models;

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

## `codex mcp-server` (Codex as a tool)

```csharp
using NCodexSDK.McpServer;
using NCodexSDK.Public.Models;

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
dotnet run --project src/NCodexSDK.Demo -- "Your prompt here"
dotnet run --project src/NCodexSDK.Demo.Review -- --commit <sha>
dotnet run --project src/NCodexSDK.AppServer.Demo -- "<repo-path>"
dotnet run --project src/NCodexSDK.McpServer.Demo -- "<repo-path>"
```

## Documentation

- Start here: [`docs/README.md`](docs/README.md)
- App Server docs: [`docs/AppServer/README.md`](docs/AppServer/README.md)
- MCP Server docs: [`docs/McpServer/README.md`](docs/McpServer/README.md)
- NuGet package README: [`src/NCodexSDK/README.md`](src/NCodexSDK/README.md)

## Troubleshooting

  * **File locked during build:** Stop any running demo processes: `Get-Process NCodexSDK.Demo | Stop-Process -Force`
  * **Session log not found:** Ensure Codex CLI is installed and `%USERPROFILE%\.codex\sessions` exists.
  * **Process launch fails:** Verify `codex` is on your PATH by running `codex --version`.

## License & Contributing

See the repository for license details. Contributions welcome — please open issues or pull requests for bugs, features, or documentation improvements.
