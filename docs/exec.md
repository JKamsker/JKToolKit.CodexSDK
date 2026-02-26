# Exec Mode (`codex exec`)

The **exec** integration launches `codex exec` as a child process and streams the resulting JSONL session log as strongly-typed .NET events via `IAsyncEnumerable<T>`.

## How It Works

1. `CodexClient.StartSessionAsync(...)` launches `codex exec`
2. The SDK captures the **session id** from process output
3. Resolves the JSONL log file (`~/.codex/sessions/...`)
4. Tails the file as it grows and parses each line into a typed event

This gives you a stable, .NET-native streaming pipeline even when Codex outputs human-readable text to stdout/stderr.

## Quick Example

```csharp
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;

await using var client = new CodexClient(new CodexClientOptions());

var options = new CodexSessionOptions("<repo-path>", "Write a hello world program")
{
    Model = CodexModel.Gpt51Codex,
    ReasoningEffort = CodexReasoningEffort.Medium
};

await using var session = await client.StartSessionAsync(options);

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default))
{
    switch (evt)
    {
        case AgentMessageEvent msg:
            Console.WriteLine(msg.Text);
            break;
        case ResponseItemEvent item when item.Payload is MessageResponseItemPayload m:
            Console.WriteLine(string.Join("\n", m.TextParts));
            break;
        case TokenCountEvent tokens:
            Console.WriteLine($"Tokens: {tokens.InputTokens} in, {tokens.OutputTokens} out");
            break;
    }
}
```

## Structured Outputs (JSON to DTO)

Constrain the final assistant message to a JSON Schema and deserialize the result into a DTO:

```csharp
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.StructuredOutputs;

public sealed record MyResult(string Answer);

await using var client = new CodexClient(new CodexClientOptions());

var result = await client.RunStructuredAsync<MyResult>(
    new CodexSessionOptions("<repo-path>", "Return JSON only.")
    {
        Model = CodexModel.Gpt52Codex
    });

Console.WriteLine(result.Value.Answer);
```

With automatic retries for invalid JSON:

```csharp
var result = await client.RunStructuredWithRetryAsync<MyResult>(
    new CodexSessionOptions("<repo-path>", "Return JSON only."),
    retry: new CodexStructuredRetryOptions { MaxAttempts = 3 });
```

## Code Reviews

Run a non-interactive code review (`codex review`):

```csharp
var review = await client.ReviewAsync(new CodexReviewOptions("<repo-path>")
{
    CommitSha = "<sha>",
    Title = "Optional title"
});

Console.WriteLine(review.StandardOutput);
```

If you want to provide custom review instructions, use prompt-only mode (stdin):

```csharp
var review = await client.ReviewAsync(new CodexReviewOptions("<repo-path>")
{
    Prompt = "Focus on correctness, security, and performance."
});

Console.WriteLine(review.StandardOutput);
```

## Resuming Sessions

Attach to an existing session by id or log path:

```csharp
await using var session = await client.ResumeSessionAsync(sessionId);

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default))
{
    // ...
}
```

## Key Types

| Type | Purpose |
|------|---------|
| `CodexClient` | Main entry point for exec mode |
| `CodexSessionHandle` | Live or historical session handle (`IAsyncDisposable`) |
| `EventStreamOptions` | Controls event filtering and stream behavior |
| `AgentMessageEvent` | High-level agent text output |
| `ResponseItemEvent` | Individual response items with typed payloads |
| `TokenCountEvent` | Token usage statistics |

## Override Hooks (Forward Compatibility)

Exec-mode event payloads can evolve upstream. By default, unknown event `type`s are mapped to `UnknownCodexEvent` and the original JSON is preserved as `CodexEvent.RawPayload`.

If you need to keep working through upstream changes without forking the SDK, you can hook the JSONL parsing pipeline:

- `CodexClientOptions.EventTransformers` — rewrite `(type, rawJson)` before mapping
- `CodexClientOptions.EventMappers` — custom mapping by event `type` (first non-null wins)

Example:

```csharp
using System.Text.Json;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Overrides;

public sealed class RenameTypeTransformer : IExecEventTransformer
{
    public (string Type, JsonElement RawPayload) Transform(string type, JsonElement rawPayload) =>
        type == "agent_message_delta" ? ("agent_message", rawPayload) : (type, rawPayload);
}

public sealed class MyEventMapper : IExecEventMapper
{
    public CodexEvent? TryMap(DateTimeOffset timestamp, string type, JsonElement rawPayload) =>
        type == "my_new_event"
            ? new UnknownCodexEvent { Timestamp = timestamp, Type = type, RawPayload = rawPayload }
            : null;
}

await using var client = new CodexClient(new CodexClientOptions
{
    EventTransformers = new IExecEventTransformer[] { new RenameTypeTransformer() },
    EventMappers = new IExecEventMapper[] { new MyEventMapper() }
});
```

## Dependency Injection

```csharp
services.AddCodexClient();
```

Registers path resolution, process launching, and JSONL tailing/parsing.

## Troubleshooting

- **Session log not found** — ensure `~/.codex/sessions` exists and the session id was captured correctly
- **Process launch fails** — verify `codex --version` works; check `CodexClientOptions.CodexExecutablePath` if overridden
- **No events streaming** — confirm Codex is producing JSONL session logs; check the resolved log file path
