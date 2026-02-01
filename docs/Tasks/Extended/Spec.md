Below is a concrete, “engineering-plan level” roadmap to add **first-class support** for:

* `codex app-server` (JSONL / JSON-RPC over stdio, **no** `"jsonrpc":"2.0"` field) ([OpenAI Developers][1])
* `codex mcp-server` (standard MCP over stdio; `tools/list` exposes `codex` + `codex-reply`) ([OpenAI Developers][2])

…into **JKToolKit.CodexSDK**, while keeping it **clean**, **maintainable**, and **.NET-native**.

---

## 0) Goals and non-goals

### Goals

1. **Two new “connection-style” clients** in JKToolKit.CodexSDK:

   * `CodexAppServerClient` → deep integration & streaming turn/item events. ([OpenAI Developers][1])
   * `CodexMcpServerClient` → Codex as a tool provider via MCP (`tools/list` / `tools/call`). ([OpenAI Developers][2])

2. **Shared infrastructure**:

   * A robust stdio process host (stdout JSONL reader + stderr drain + graceful shutdown).
   * A generic JSON-RPC dispatcher (request/response correlation, notifications, **server-initiated requests**).

3. **Dotnet-affine public surface**:

   * Options objects, `CancellationToken` everywhere, `IAsyncDisposable`, `Task`/`ValueTask`, `IAsyncEnumerable<T>` for streaming.
   * Strongly typed convenience APIs **plus** low-level escape hatches for forward-compat.

### Non-goals (for first iteration)

* Implement every single app-server method on day 1. The app-server API is broad and explicitly intended for rich clients and may change. ([OpenAI Developers][3])
* A full “general MCP client” library. We only need the MCP subset required to call `codex` / `codex-reply` reliably. ([OpenAI Developers][2])

---

## 1) Repository and packaging strategy

### Recommended: add two optional subpackages (best maintainability)

Keep the existing `JKToolKit.CodexSDK` package (exec/resume + JSONL log tailing) untouched, and add:

* `JKToolKit.CodexSDK.AppServer`
* `JKToolKit.CodexSDK.McpServer`

Why:

* `codex exec` support remains stable and independent.
* App-server and MCP server are both marked **Experimental** in the CLI reference; isolating risk is good. ([OpenAI Developers][3])
* Dependencies can stay minimal and targeted.

**Solution changes**

* Add new projects under `src/` and add them to `JKToolKit.CodexSDK.sln`.
* Add new test projects or extend existing test project with new test suites.

> If you strongly prefer “one NuGet package”, you can still keep them as separate namespaces in the same assembly—but the multi-package approach is the most maintainable over time.

---

## 2) Shared infrastructure layer (core building blocks)

This is the key “clean architecture” move: build the hard parts once, then implement app-server and MCP server on top.

### 2.1 `StdioProcess` host

Create an internal abstraction to spawn and manage a long-running process.

**Key requirements**

* `RedirectStandardInput = true`
* `RedirectStandardOutput = true` (protocol messages only)
* `RedirectStandardError = true` (drain it to avoid deadlocks; optionally expose as logs)
* **Never write anything to stdout** from the SDK when running app-server/mcp-server; stdout is the protocol channel. (Codex does protocol on stdout and typically uses stderr for logs.) ([OpenAI Developers][1])

**Proposed internal types**

* `internal sealed class StdioProcess : IAsyncDisposable`

  * `StreamWriter Stdin`
  * `StreamReader Stdout`
  * `StreamReader Stderr`
  * `Task Completion` (process exit)
  * `ValueTask DisposeAsync()` gracefully closes stdin, waits with timeout, kills if needed.
* `internal sealed record ProcessLaunchOptions`

  * `string FileName` (default resolved from existing `ICodexPathProvider`)
  * `IReadOnlyList<string> Arguments`
  * `string? WorkingDirectory`
  * `IDictionary<string,string> Environment`
  * `TimeSpan StartupTimeout`
  * `TimeSpan ShutdownTimeout`

**Nice-to-have**

* Support “launcher prefix” (so users can run `npx -y codex app-server` like the docs show for MCP workflows). ([OpenAI Developers][2])

  * This can be done via `FileName="npx"`, `Arguments=["-y","codex","app-server"]`.

### 2.2 Generic JSONL JSON-RPC engine

Both protocols are JSON-RPC shaped, line-delimited JSON objects. App-server omits `jsonrpc`, MCP includes it. ([OpenAI Developers][1])

Build a single engine with small knobs:

**Proposed internal types**

* `internal sealed class JsonRpcConnection : IAsyncDisposable`

  * `Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)`
  * `Task SendNotificationAsync(string method, object? @params, CancellationToken ct)`
  * `event Func<JsonRpcNotification, ValueTask>? OnNotification`
  * `Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest` (for approvals / elicitation)
  * `bool IncludeJsonRpcHeader` (false for app-server, true for MCP)
  * `JsonSerializerOptions SerializerOptions`

* Message DTOs:

  * `JsonRpcRequest { id, method, params, jsonrpc? }`
  * `JsonRpcResponse { id, result?, error?, jsonrpc? }`
  * `JsonRpcNotification { method, params, jsonrpc? }`

**Important capability:** server-initiated requests
App-server integrations include approvals and interactive flows; MCP can also send “approval elicitations” for exec/patch. ([OpenAI Developers][1])

So the read loop must distinguish:

* Response: has `id` and (`result` or `error`)
* Request: has `id` and `method` (no `result`)
* Notification: has `method` and no `id`

### 2.3 Backpressure and routing

A long-running server can output a lot of events. You need:

* A dedicated read loop that **always drains stdout**.
* A routing layer that can deliver events to consumers without blocking the reader.

**Recommendation**

* Use `System.Threading.Channels` internally:

  * Global channel for all notifications (optional).
  * Per-turn channel(s) for turn-scoped streaming.

Expose to users as `IAsyncEnumerable<T>`:

* `.ReadAllAsync(ct)` from the channel reader.

---

## 3) `codex app-server` support plan

### 3.1 Protocol facts we must implement

From the official docs:

* JSONL over stdio, JSON-RPC 2.0 **without** `"jsonrpc":"2.0"`. ([OpenAI Developers][1])
* Must `initialize` then send `initialized` notification, or server rejects requests. ([OpenAI Developers][1])
* Threads + turns + items; after `turn/start`, read notifications like `item/agentMessage/delta` and `turn/completed`. ([OpenAI Developers][1])
* Can generate JSON Schema bundle per installed Codex version. ([OpenAI Developers][1])
* The CLI describes app-server as for local dev/debugging and may change. ([OpenAI Developers][3])

### 3.2 Public API design (dotnet-native)

#### Top-level client

```csharp
public sealed class CodexAppServerClient : IAsyncDisposable
{
    public static Task<CodexAppServerClient> StartAsync(
        CodexAppServerClientOptions options,
        CancellationToken ct = default);

    public Task<AppServerInitializeResult> InitializeAsync(
        AppServerClientInfo clientInfo,
        CancellationToken ct = default);

    public Task<CodexThread> StartThreadAsync(
        ThreadStartOptions options,
        CancellationToken ct = default);

    public Task<CodexThread> ResumeThreadAsync(
        string threadId,
        CancellationToken ct = default);

    public Task<CodexTurnHandle> StartTurnAsync(
        string threadId,
        TurnStartOptions options,
        CancellationToken ct = default);

    // optional escape hatch:
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default);

    // optional: global notification stream
    public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct = default);
}
```

#### Turn handle (streaming)

```csharp
public sealed class CodexTurnHandle : IAsyncDisposable
{
    public string ThreadId { get; }
    public string TurnId { get; }

    // Stream *only* notifications for this turn
    public IAsyncEnumerable<AppServerTurnEvent> Events(CancellationToken ct = default);

    // Completes when turn/completed arrives
    public Task<TurnCompletedEvent> Completion { get; }

    public Task InterruptAsync(CancellationToken ct = default);
}
```

This matches how app-server is described: start thread, start turn, stream notifications, finish on `turn/completed`. ([OpenAI Developers][1])

### 3.3 Options & models (strong typing without fragility)

#### Options

* `CodexAppServerClientOptions`

  * `ProcessLaunchOptions Launch` (command, args, env, cwd)
  * `AppServerClientInfo DefaultClientInfo` (name/title/version)
  * `JsonSerializerOptions? SerializerOptionsOverride`
  * `int NotificationBufferCapacity` (default e.g. 5000)

* `ThreadStartOptions`

  * `string? Model`
  * `string? Cwd`
  * `ApprovalPolicy? ApprovalPolicy`
  * `SandboxPolicy? SandboxPolicy`
  * etc.

The docs explicitly show `thread/start` accepting `model`, `cwd`, `approvalPolicy`, `sandbox`. ([OpenAI Developers][1])

* `TurnStartOptions`

  * `IReadOnlyList<TurnInputItem> Input` (text/image/localImage/skill)
  * Overrides: `model`, `effort`, `cwd`, `summary`, `outputSchema`, etc. ([OpenAI Developers][1])

#### Notifications (initial set)

Start with the “must-have” streaming notifications:

* `item/agentMessage/delta`
* `item/started`
* `item/completed`
* `turn/completed` ([OpenAI Developers][1])

Model them as:

```csharp
public abstract record AppServerNotification(string Method, JsonElement Params);

public sealed record AgentMessageDeltaNotification(
    string ThreadId, string TurnId, string ItemId, string Delta);

public sealed record TurnCompletedEvent(
    string ThreadId, string TurnId, string Status, JsonElement? Error);
```

Include an “unknown notification” fallback:

```csharp
public sealed record UnknownNotification(string Method, JsonElement Params);
```

This is critical because app-server is experimental and may evolve. ([OpenAI Developers][3])

### 3.4 Initialization and lifecycle

Implement `StartAsync()` to:

1. Launch `codex app-server` process.
2. Create `JsonRpcConnection` with `IncludeJsonRpcHeader=false`.
3. Send `initialize` request and await response.
4. Send `initialized` notification. ([OpenAI Developers][1])

Expose `InitializeAsync` if you want manual control, but default to automatic handshake because it’s what a .NET dev expects.

### 3.5 Approvals and interactive server requests

App-server is explicitly for “approvals” and rich integrations. ([OpenAI Developers][1])
Design an **approval hook** that is optional but first-class:

* `IAppServerApprovalHandler` (or generic `IServerRequestHandler`)

  * Receives the raw request (method, params) and returns an approve/deny result.
* `CodexAppServerClientOptions.ApprovalHandler = ...`
* Provide built-in handler implementations:

  * `AlwaysApproveHandler`
  * `AlwaysDenyHandler`
  * `PromptConsoleApprovalHandler` (demo-only)

Even if users set `approvalPolicy: "never"` often, your SDK should not deadlock when Codex asks anyway.

### 3.6 Schema-driven strong typing (maintainable approach)

Codex supports generating version-matched schema bundles: ([OpenAI Developers][1])

* `codex app-server generate-json-schema --out ./schemas`

Plan:

1. Add a `scripts/` helper that runs schema generation (manual step, not part of normal builds).
2. Use `NJsonSchema` (or similar) to generate DTOs into:

   * `src/JKToolKit.CodexSDK.AppServer/Generated/*`
3. Keep your public API *handwritten* and stable.
4. Use generated DTOs internally to deserialize `result` payloads when available; fall back to `JsonElement` for unknowns.

This keeps the SDK pleasant while avoiding “generated types become your public surface” lock-in.

---

## 4) `codex mcp-server` support plan

### 4.1 Protocol facts we must implement

From the official “Agents SDK” guide:

* Start with: `codex mcp-server` ([OpenAI Developers][2])
* Client sends `tools/list` and sees two tools:

  * `codex` (start session) with args including:

    * `prompt` (required), `approval-policy`, `sandbox`, `cwd`, `model`, etc. ([OpenAI Developers][2])
  * `codex-reply` with args: `threadId` + `prompt`; `conversationId` is deprecated alias. ([OpenAI Developers][2])
* Use `structuredContent.threadId` from tool result. ([OpenAI Developers][2])
* CLI reference: MCP server exits when downstream closes the connection. ([OpenAI Developers][3])
* MCP is JSON-RPC 2.0 over stdio. ([Model Context Protocol][4])

### 4.2 Implementation approach

Use the same shared `JsonRpcConnection` engine, but:

* `IncludeJsonRpcHeader = true`
* Implement minimal MCP methods needed:

  * `initialize` (capability negotiation)
  * `notifications/initialized` (or `initialized` depending on MCP revision—follow current MCP spec)
  * `tools/list`
  * `tools/call` ([Model Context Protocol][5])

> If you prefer, you can optionally add a second implementation using the official MCP C# SDK later. But building the minimal subset on your own keeps dependencies light and under your control.

### 4.3 Public API design (dotnet-native)

#### Top-level client

```csharp
public sealed class CodexMcpServerClient : IAsyncDisposable
{
    public static Task<CodexMcpServerClient> StartAsync(
        CodexMcpServerClientOptions options,
        CancellationToken ct = default);

    public Task<IReadOnlyList<McpToolDescriptor>> ListToolsAsync(CancellationToken ct = default);

    public Task<CodexMcpSessionStartResult> StartSessionAsync(
        CodexMcpStartOptions options,
        CancellationToken ct = default);

    public Task<CodexMcpReplyResult> ReplyAsync(
        string threadId,
        string prompt,
        CancellationToken ct = default);

    // escape hatch
    public Task<McpToolCallResult> CallToolAsync(string toolName, object arguments, CancellationToken ct = default);
}
```

#### Options / result types

* `CodexMcpStartOptions`

  * `string Prompt` (required)
  * `CodexApprovalPolicy ApprovalPolicy` (maps to `approval-policy`)
  * `CodexSandboxMode Sandbox` (maps to `read-only`, `workspace-write`, `danger-full-access`) ([OpenAI Developers][2])
  * `string? Cwd`
  * `string? Model`
  * `bool? IncludePlanTool`
  * etc.

* `CodexMcpSessionStartResult`

  * `string ThreadId`
  * `string? Text` (best-effort extracted from `content` or `structuredContent.content`)
  * `JsonElement StructuredContent` (raw)

This is aligned with docs noting modern clients prefer `structuredContent`. ([OpenAI Developers][2])

### 4.4 Approvals / elicitation handling

Docs explicitly mention “Approval elicitations (exec/patch) include threadId in params.” ([OpenAI Developers][2])
So your MCP client should support server → client requests and route them to a handler.

Plan:

* Add `IMcpElicitationHandler` (or reuse a generic `IServerRequestHandler`).
* Provide:

  * auto-approve / auto-deny policies
  * callback-based handling
* Ensure that if no handler is configured, the SDK replies with a clear “unsupported” error (instead of hanging).

---

## 5) Clean integration with existing JKToolKit.CodexSDK

### 5.1 Keep existing `CodexClient` intact

Your current `CodexClient` + `CodexSessionHandle` pipeline (exec/resume + session JSONL tailing) should remain unchanged.

### 5.2 Add new DI registration helpers

Mirror the existing `.AddCodexClient(...)` pattern.

* `services.AddCodexAppServerClient(Action<CodexAppServerClientOptions>? configure = null)`
* `services.AddCodexMcpServerClient(Action<CodexMcpServerClientOptions>? configure = null)`

Each registers:

* `ICodexPathProvider` reuse
* `ILogger<>` integration
* new internal `StdioProcessFactory`
* new internal `JsonRpcConnectionFactory`

### 5.3 Unified “Codex configuration” enums

Avoid duplicate stringly-typed fields across exec/app-server/mcp-server:

* `CodexApprovalPolicy` enum

  * `Untrusted`, `OnRequest`, `OnFailure`, `Never` (per docs) ([OpenAI Developers][2])
* `CodexSandboxMode` enum

  * `ReadOnly`, `WorkspaceWrite`, `DangerFullAccess` ([OpenAI Developers][2])

Then have protocol-specific converters:

* MCP expects kebab-case (`workspace-write` etc). ([OpenAI Developers][2])
* App-server uses its own schema (`sandboxPolicy.type` etc). ([OpenAI Developers][1])

This keeps the public surface consistent and .NET-like.

---

## 6) Testing strategy (practical and maintainable)

### 6.1 Unit tests: JSON-RPC engine

Create tests that run against a **fake stdio server** implemented in-process:

* Use `AnonymousPipeServerStream`/`AnonymousPipeClientStream` or spawn a tiny `dotnet` console “echo server” used only for tests.
* Validate:

  * request/response correlation
  * notification dispatch
  * server request handling (approval path)
  * cancellation + shutdown behavior
  * parse errors produce actionable exceptions

### 6.2 Contract tests: app-server shapes

* Feed recorded JSONL fixtures (from docs or from a real codex run) into the parser/router.
* Assert typed mapping: `item/agentMessage/delta` → `AgentMessageDeltaNotification`, etc. ([OpenAI Developers][1])

### 6.3 Contract tests: MCP tool calls

Simulate MCP server responses:

* `tools/list` returns the tool descriptors
* `tools/call` returns `structuredContent.threadId` etc. ([OpenAI Developers][2])

### 6.4 Optional integration tests (guarded)

If you want true end-to-end tests:

* Mark as `[Trait("Category","Integration")]`
* Skip unless `CODEX_E2E=1` and `codex` is available on PATH
* Run minimal `thread/start` + `turn/start` and read until `turn/completed`. ([OpenAI Developers][1])

---

## 7) Docs and examples (developer experience)

### 7.1 Add README sections

* “Using `codex app-server` from C#”
* “Using `codex mcp-server` from C#”
* When to choose which (brief decision guide)

### 7.2 Add demos

Add two console demos (parallel to existing `JKToolKit.CodexSDK.Demo`):

* `JKToolKit.CodexSDK.AppServer.Demo`

  * start client, create thread, start turn, stream deltas, print final
* `JKToolKit.CodexSDK.McpServer.Demo`

  * list tools, run `codex`, extract threadId, run `codex-reply`

Both should show cancellation and graceful shutdown.

---

## 8) Suggested delivery milestones (keeps risk low)

### Milestone 1 — shared core

* Add `StdioProcess`
* Add `JsonRpcConnection` with:

  * request/response
  * notifications
  * server requests
* Add unit tests

### Milestone 2 — app-server minimal happy path

* Auto-handshake (`initialize` + `initialized`) ([OpenAI Developers][1])
* Implement `thread/start`, `turn/start`, `turn/interrupt` ([OpenAI Developers][1])
* Implement streaming for:

  * `item/agentMessage/delta`
  * `turn/completed` ([OpenAI Developers][1])
* Demo app

### Milestone 3 — MCP server minimal happy path

* MCP handshake + `tools/list` + `tools/call` ([OpenAI Developers][2])
* Wrapper methods:

  * `StartSessionAsync` (tool `codex`)
  * `ReplyAsync` (tool `codex-reply`) ([OpenAI Developers][2])
* Demo app

### Milestone 4 — approvals/elicitation

* Pluggable approval handlers (both app-server and MCP)
* “safe defaults”: deny/unsupported with clear exception if unhandled ([OpenAI Developers][2])

### Milestone 5 — expand app-server coverage

Add higher-level APIs:

* `thread/list`, `thread/archive`, `thread/fork`, etc. ([OpenAI Developers][1])
* `model/list`, `skills/list`, etc. ([OpenAI Developers][1])

### Milestone 6 — schema/codegen workflow

* Add schema generation script hooks:

  * `codex app-server generate-json-schema` ([OpenAI Developers][1])
* Add optional generated DTOs, keep public API stable.

---

## 9) What this will look like for a .NET user (target UX)

### App-server (deep integration)

```csharp
await using var codex = await CodexAppServerClient.StartAsync(new()
{
    Launch = CodexLaunch.CodexOnPath().WithArgs("app-server"),
    DefaultClientInfo = new("my_product", "My Product", "1.0.0"),
});

var thread = await codex.StartThreadAsync(new ThreadStartOptions
{
    Model = "gpt-5.1-codex",
    Cwd = repoPath,
    ApprovalPolicy = CodexApprovalPolicy.Never,
});

await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
{
    Input = [ TurnInputItem.Text("Summarize this repo.") ],
});

await foreach (var e in turn.Events(ct))
{
    if (e is AgentMessageDeltaEvent d) Console.Write(d.Delta);
}

var completed = await turn.Completion;
Console.WriteLine($"\nDone: {completed.Status}");
```

### MCP server (Codex as a tool)

```csharp
await using var codex = await CodexMcpServerClient.StartAsync(new()
{
    Launch = CodexLaunch.CodexOnPath().WithArgs("mcp-server")
});

var run = await codex.StartSessionAsync(new CodexMcpStartOptions
{
    Prompt = "Run tests and summarize failures.",
    Sandbox = CodexSandboxMode.WorkspaceWrite,
    ApprovalPolicy = CodexApprovalPolicy.Never,
    Cwd = repoPath
});

Console.WriteLine(run.Text);

var followUp = await codex.ReplyAsync(run.ThreadId, "Now propose fixes.");
Console.WriteLine(followUp.Text);
```

These shapes are “.NET-native” (options classes, async APIs, turn handles) while matching what Codex actually expects on the wire. ([OpenAI Developers][1])

---

If you want, I can take this plan and map it directly onto **the existing JKToolKit.CodexSDK folder structure and naming conventions** (exact namespaces, which classes should be `internal`, which should be public, and where to plug into the current DI registrations), but the outline above is already designed to drop cleanly into the repo you shared.

[1]: https://developers.openai.com/codex/app-server "https://developers.openai.com/codex/app-server"
[2]: https://developers.openai.com/codex/guides/agents-sdk/ "https://developers.openai.com/codex/guides/agents-sdk/"
[3]: https://developers.openai.com/codex/cli/reference/ "Command line options"
[4]: https://modelcontextprotocol.io/specification/2025-06-18/basic/transports?utm_source=chatgpt.com "Transports"
[5]: https://modelcontextprotocol.io/docs/learn/architecture?utm_source=chatgpt.com "Architecture overview"
