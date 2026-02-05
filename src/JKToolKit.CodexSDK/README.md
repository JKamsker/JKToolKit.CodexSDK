# JKToolKit.CodexSDK (core)

JKToolKit.CodexSDK is a .NET client library that wraps the **Codex CLI** as a local subprocess and provides a **strongly-typed, streaming-first** API for:

- Starting Codex sessions (`codex exec`) and streaming the resulting JSONL session log as typed .NET events
- Resuming/attaching to existing sessions by session id / log path
- Running non-interactive reviews (`codex review`) through the same process-launch infrastructure

This package also includes two stdio JSON-RPC integrations:

- `JKToolKit.CodexSDK.AppServer` (`codex app-server`)
- `JKToolKit.CodexSDK.McpServer` (`codex mcp-server`)

Docs live under `docs/`:

- [`docs/README.md`](../../docs/README.md)
- [`docs/AppServer/README.md`](../../docs/AppServer/README.md)
- [`docs/McpServer/README.md`](../../docs/McpServer/README.md)

## What This Library Is (and isn’t)

**It is:**

- A **local CLI wrapper**: it starts `codex` as a process and interacts with it through stdin/stdout/stderr and local files.
- **Streaming-first**: it turns the Codex JSONL log into `IAsyncEnumerable<T>` of typed events.
- **Testable**: core dependencies are abstracted (`IFileSystem`, `ICodexPathProvider`, `ICodexProcessLauncher`, …).

**It is not:**

- A general “OpenAI HTTP API” client.
- A full MCP client library (that’s intentionally minimal and scoped in `JKToolKit.CodexSDK.McpServer`).

## Core Concept

When you start a session via `codex exec`, Codex writes a **JSONL session log** to a file under the Codex sessions directory (commonly `%USERPROFILE%\.codex\sessions` on Windows).

JKToolKit.CodexSDK:

1. Launches `codex exec` as a child process.
2. Captures the **session id** from process output.
3. Resolves the JSONL log file for that session id.
4. Tails the file as it grows (like `tail -f`) and parses each JSON line into a typed event model.

This gives you a stable, .NET-native streaming pipeline even if Codex outputs “human text” to stdout/stderr.

## How It Works (Pipeline)

For a live session:

1. `CodexClient.StartSessionAsync(...)`
2. `ICodexProcessLauncher` starts the process (`codex exec ... -`)
3. `ICodexSessionLocator` finds the session JSONL file
4. `IJsonlTailer` yields appended JSONL lines
5. `IJsonlEventParser` maps JSONL records to typed event models
6. Your code consumes events via `await foreach`

For a resumed session:

1. `CodexClient.ResumeSessionAsync(...)`
2. `ICodexSessionLocator` finds the session JSONL file (by id or path)
3. Same tail+parse pipeline

## Key Types

- `JKToolKit.CodexSDK.Exec.CodexClient`: main entry point
- `JKToolKit.CodexSDK.Exec.CodexSessionHandle`: a live or historical session handle (`IAsyncDisposable`)
- `JKToolKit.CodexSDK.Exec.EventStreamOptions`: controls event filtering/stream options
- `JKToolKit.CodexSDK.Models.*`: strongly-typed **Exec JSONL** event models (`SessionMetaEvent`, `ResponseItemEvent`, …)

## Getting Started

### Prerequisites

- .NET 10 SDK
- Codex CLI installed (`codex` / `codex.cmd` on PATH)

### Start a session and stream events

```csharp
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;

await using var client = new CodexClient(new CodexClientOptions());

var options = new CodexSessionOptions("<repo-path>", "Write a hello world program")
{
    Model = CodexModel.Gpt52Codex,
    ReasoningEffort = CodexReasoningEffort.Medium
};

await using var session = await client.StartSessionAsync(options);

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default))
{
    switch (evt)
    {
        case AgentMessageEvent msg:
            Console.WriteLine(msg.Content);
            break;
        case ResponseItemEvent item when item.Payload.Message is { } m:
            Console.WriteLine(string.Join("", m.TextParts));
            break;
    }
}
```

### Run a non-interactive code review (`codex review`)

```csharp
var review = await client.ReviewAsync(new CodexReviewOptions("<repo-path>")
{
    CommitSha = "<sha>",
    Prompt = "Focus on correctness, security, and performance."
});

Console.WriteLine(review.StandardOutput);
```

## Dependency Injection (Optional)

The core library can register defaults via:

```csharp
services.AddCodexClient();
```

This wires up:

- Path resolution (`DefaultCodexPathProvider`)
- Process launching (`CodexProcessLauncher`)
- JSONL tailing/parsing (`JsonlTailer`, `JsonlEventParser`)

## Extensibility / Forward Compatibility

Codex evolves quickly. JKToolKit.CodexSDK is designed so that:

- Unknown event shapes won’t break streaming; raw JSON is preserved where helpful.
- “Value object” model identifiers (e.g. `CodexModel`) accept arbitrary strings.

To add support for new `response_item` payloads, extend the normalization logic in:

- `src/JKToolKit.CodexSDK/Infrastructure/JsonlEventParser.cs`

## Troubleshooting

- **Session log not found**: ensure Codex created `%USERPROFILE%\.codex\sessions` and that the session id was captured correctly.
- **Process launch fails**: validate `codex --version` works and `CodexClientOptions.CodexExecutablePath` if overridden.
- **No events streaming**: confirm Codex is producing JSONL session logs for your command; check the resolved log file path.

