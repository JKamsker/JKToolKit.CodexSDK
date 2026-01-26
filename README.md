# <img src="res/logo.png" align="left" width="120" height="120" /> NCodexSDK

[![CI](https://github.com/JKamsker/NCodexSDK/actions/workflows/ci.yml/badge.svg)](https://github.com/JKamsker/NCodexSDK/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/badge/Nuget-Download-blue?logo=nuget)](https://www.nuget.org/packages/NC%C3%B2dexSDK)

<br clear="left"/>

A strongly-typed .NET client library for interacting with the Codex CLI, enabling programmatic control of AI coding sessions with full event streaming and session management.

## Installation

### Prerequisites

  * .NET 10 SDK or later
  * Codex CLI ≥ 0.60.1 installed and available on PATH (`codex` / `codex.cmd`)

### NuGet

```bash
dotnet add package NCòdexSDK
```

## Quickstart

Here is the fastest way to start a session and stream the output events.

```csharp
using NCodexSDK;

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

## Common Patterns

### Resuming Sessions

You can attach to existing sessions (read-only) or continue them with new prompts.

**Attach (Read-Only):**

```csharp
await using var historical = await client.ResumeSessionAsync("session-id-here", CancellationToken.None);
// Stream historical events...
```

**Resume with Follow-up:**

```csharp
var followUpOptions = sessionOptions.Clone();
followUpOptions.Prompt = "Now add error handling";

await using var resumed = await client.ResumeSessionAsync(session.Info.Id, followUpOptions, CancellationToken.None);
// Continue streaming events...
```

### Checking Rate Limits

Query current limits without starting a full session:

```csharp
var limits = await client.GetRateLimitsAsync(noCache: true, CancellationToken.None);

Console.WriteLine($"Requests: {limits.RequestsRemaining}/{limits.RequestsLimit}");
Console.WriteLine($"Tokens:   {limits.TokensRemaining}/{limits.TokensLimit}");
Console.WriteLine($"Resets:   {limits.RequestsResetAt}");
```

### Running Code Reviews

Run Codex's non-interactive reviewer against a specific commit (or use <c>Uncommitted</c>/<c>BaseBranch</c>):

```csharp
var review = await client.ReviewAsync(new CodexReviewOptions("<repo-dir>")
{
    CommitSha = "9a8ff41389e6684f222fb982f50efc04b59e0d50",
    Prompt = "Focus on correctness, security, and performance."
});

Console.WriteLine(review.StandardOutput);
```

### Handling Complex Responses

The `ResponseItemEvent` normalizes payloads (reasoning, messages, function calls) while preserving raw JSON for future compatibility.

```csharp
await foreach (var evt in session.GetEventsAsync())
{
    if (evt is ResponseItemEvent item)
    {
        switch (item.Payload)
        {
            case { Reasoning: var reasoning }:
                Console.WriteLine($"Thinking: {reasoning.SummaryText}");
                break;
            case { Message: var msg }:
                Console.WriteLine($"Response: {string.Join("", msg.TextParts)}");
                break;
            case { FunctionCall: var call }:
                Console.WriteLine($"Calling: {call.Name}({call.Arguments})");
                break;
            case { GhostSnapshot: var ghost }:
                Console.WriteLine($"Snapshot: {ghost.Path} - {ghost.Action}");
                break;
            default:
                // Fallback for new/unknown payload types
                Console.WriteLine($"Raw JSON: {item.Payload.Raw}");
                break;
        }
    }
}
```

## Features Overview

  - **Strongly-typed session management:** Launch, monitor, and control sessions with type-safe options.
  - **Real-time event streaming:** Parse JSONL session events into typed models immediately.
  - **Session resumption:** Seamlessly pick up where a conversation left off.
  - **Rate limit tracking:** Built-in queries for API limits.

-----

## Technical Reference

### Event Models

The library provides strongly typed models for all Codex session events:

  * `SessionMetaEvent` — Session metadata and configuration
  * `UserMessageEvent`, `AgentMessageEvent`, `AgentReasoningEvent` — Conversation flow
  * `TokenCountEvent` — Token usage with embedded `RateLimits`
  * `TurnContextEvent` — Turn-level context and state
  * `ResponseItemEvent` — Normalized response items (reasoning, messages, function calls, ghost snapshots).

### Architecture

  * **Session log resolution:** Captures session ID from Codex stderr/stdout; resolves logs by ID first with time-based polling fallback for robustness.
  * **Pluggable infrastructure:** All file system operations, path resolution, and process management use interfaces (`ICodexProcessLauncher`, `IJsonlTailer`, `IFileSystem`) to allow for easy mocking and testing.
  * **Graceful shutdown:** `CodexSessionHandle.DisposeAsync` terminates live processes with configurable timeouts.

### Extensibility

To add support for new `response_item` payload types:

1.  Extend `NormalizeResponseItemPayload` in `JsonlEventParser.cs`.
2.  Add new properties to the `ResponseItemPayload` record.
3.  (Note: Raw JSON is automatically preserved for unknown types regardless of this step).

## Demo Application

A sample console application is included at `src/NCodexSDK.Demo`.

```bash
dotnet run --project src/NCodexSDK.Demo -- "Your prompt here"
```

There is also a review demo that forwards to `codex review` via the SDK:

```bash
dotnet run --project src/NCodexSDK.Demo.Review -- --commit 9a8ff41389e6684f222fb982f50efc04b59e0d50
```

## Troubleshooting

  * **File locked during build:** Stop any running demo processes: `Get-Process NCodexSDK.Demo | Stop-Process -Force`
  * **Session log not found:** Ensure Codex CLI is installed and `%USERPROFILE%\.codex\sessions` exists.
  * **Process launch fails:** Verify `codex` is on your PATH by running `codex --version`.

## License & Contributing

See the repository for license details. Contributions welcome — please open issues or pull requests for bugs, features, or documentation improvements.
