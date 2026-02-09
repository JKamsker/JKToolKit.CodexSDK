# Feature Specification: CodexSdk Facade (Exec + AppServer + McpServer)

**Feature Branch**: `002-sdk-facade`  
**Created**: February 2, 2026  
**Status**: Draft  
**Input**: Add a "nice façade" entrypoint (`CodexSdk`) that makes the three supported Codex integration modes feel consistent and discoverable, while keeping the existing public APIs intact.

## Motivation

Today the SDK exposes three entry points that are each correct, but easy to miss or mix up:

- **`codex exec`** → `JKToolKit.CodexSDK.Exec.CodexClient`
- **`codex app-server`** → `JKToolKit.CodexSDK.AppServer.CodexAppServerClient` (+ `ICodexAppServerClientFactory` for DI)
- **`codex mcp-server`** → `JKToolKit.CodexSDK.McpServer.CodexMcpServerClient` (+ `ICodexMcpServerClientFactory` for DI)

A small facade layer should:

- Provide a single obvious “starting point” (`CodexSdk`) without forcing an API redesign.
- Preserve the current entry points (no breaking changes).
- Make the 3 modes feel consistent via **`sdk.Exec`**, **`sdk.AppServer`**, **`sdk.McpServer`**.
- Offer a one-call DI registration (`services.AddCodexSdk(...)`) for people who want “just give me everything”.

## User Scenarios & Testing

### User Story 1 — Non-DI consumer wants one discoverable entry point (Priority: P1)

A developer wants to use the SDK without setting up DI. They want a single root type to reach all modes.

**Acceptance Scenarios**

1. **Given** a default `CodexSdk` instance, **when** the developer uses `sdk.Exec`, **then** they can start and stream an `exec` session as before.
2. **Given** a default `CodexSdk` instance, **when** the developer uses `sdk.AppServer`, **then** they can start an app-server client with default options.
3. **Given** a default `CodexSdk` instance, **when** the developer uses `sdk.McpServer`, **then** they can start an mcp-server client with default options.
4. **Given** the developer provides a custom Codex executable path once, **when** they start any mode, **then** that path is used consistently.

**Independent Test**: A unit test that constructs `CodexSdk` with test doubles for factories and verifies calls are routed correctly; plus at least one compile-time doc snippet test (example code compiles).

---

### User Story 2 — DI consumer wants “one registration call” (Priority: P1)

A developer using `Microsoft.Extensions.DependencyInjection` wants to register the SDK with one call and then inject a single object.

**Acceptance Scenarios**

1. **Given** `services.AddCodexSdk(...)`, **when** the developer resolves `CodexSdk`, **then** `sdk.Exec`, `sdk.AppServer`, and `sdk.McpServer` are all usable.
2. **Given** DI config delegates for each options object, **when** the developer starts a client, **then** the configured options are applied.
3. **Given** an override of an abstraction (e.g., `ICodexPathProvider`), **when** `sdk` is resolved from DI, **then** the override is respected.

**Independent Test**: Container setup test verifying the service graph resolves and factories are invoked.

---

### User Story 3 — Consistent surface area + minimal ceremony (Priority: P2)

A developer wants the three modes to look and feel consistent.

**Acceptance Scenarios**

1. **Given** the developer learns `sdk.Exec.StartSessionAsync(...)`, **then** they can reasonably guess `sdk.AppServer.StartAsync(...)` and `sdk.McpServer.StartAsync(...)`.
2. **Given** cancellation tokens, **when** the developer cancels `StartAsync` / `StartSessionAsync`, **then** the cancellation flows to the underlying mode.
3. **Given** disposal, **when** the developer disposes a started server client, **then** the underlying process is terminated just as with the existing API.

---

### User Story 4 — Backwards compatibility (Priority: P0)

Existing callers should not be forced onto the facade, and no current APIs should break.

**Acceptance Scenarios**

1. Existing examples using `new CodexClient(...)` still compile and behave the same.
2. Existing examples using `CodexAppServerClient.StartAsync(...)` still compile and behave the same.
3. Existing examples using `CodexMcpServerClient.StartAsync(...)` still compile and behave the same.

## Requirements

### Functional Requirements

- **FR-001**: The SDK MUST add a new, single entry point `CodexSdk` exposing three sub-facades: `Exec`, `AppServer`, `McpServer`.
- **FR-002**: The facade MUST be additive only; existing public APIs MUST remain supported and unchanged.
- **FR-003**: `sdk.Exec` MUST enable the same capabilities as `CodexClient` (start/resume/attach/list/review, etc.) by delegating to the existing implementation.
- **FR-004**: `sdk.AppServer` MUST be able to start `CodexAppServerClient` using either:
  - DI-provided `ICodexAppServerClientFactory`, OR
  - direct options-driven startup (non-DI path).
- **FR-005**: `sdk.McpServer` MUST be able to start `CodexMcpServerClient` using either:
  - DI-provided `ICodexMcpServerClientFactory`, OR
  - direct options-driven startup (non-DI path).
- **FR-006**: The facade MUST support configuring **one Codex executable path** once (globally) and applying it across all modes unless a mode-specific override is provided.
- **FR-007**: The facade SHOULD support configuring an `ILoggerFactory` once and using it consistently across modes.
- **FR-008**: The SDK MUST add `services.AddCodexSdk(...)` as a convenience DI registration that calls:
  - `AddCodexClient(...)`
  - `AddCodexAppServerClient(...)`
  - `AddCodexMcpServerClient(...)`
  - and registers `CodexSdk` itself.

### Non-Functional Requirements

- **NFR-001**: The facade SHOULD avoid introducing heavy new dependencies. (If non-DI construction is provided, prefer direct wiring over building an internal service provider.)
- **NFR-002**: The facade MUST remain thread-safe for typical usage patterns (e.g., starting multiple sessions/clients concurrently) provided the underlying clients are used safely.
- **NFR-003**: Public API additions MUST include XML docs and an example in docs/README or a dedicated quickstart.

## Proposed Public API (Draft)

> Names are flexible, but the overall shape should match this.

```csharp
// Namespace: JKToolKit.CodexSDK
public sealed class CodexSdk : IAsyncDisposable
{
    public CodexExecFacade Exec { get; }
    public CodexAppServerFacade AppServer { get; }
    public CodexMcpServerFacade McpServer { get; }

    // DI-friendly constructor
    public CodexSdk(
        JKToolKit.CodexSDK.Abstractions.ICodexClient exec,
        JKToolKit.CodexSDK.AppServer.ICodexAppServerClientFactory appServer,
        JKToolKit.CodexSDK.McpServer.ICodexMcpServerClientFactory mcpServer);

    // Non-DI convenience
    public static CodexSdk Create(Action<CodexSdkBuilder>? configure = null);

    public ValueTask DisposeAsync();
}

public sealed class CodexSdkBuilder
{
    public CodexSdkBuilder UseLoggerFactory(ILoggerFactory loggerFactory);

    public CodexSdkBuilder ConfigureExec(Action<CodexClientOptions> configure);
    public CodexSdkBuilder ConfigureAppServer(Action<CodexAppServerClientOptions> configure);
    public CodexSdkBuilder ConfigureMcpServer(Action<CodexMcpServerClientOptions> configure);

    public CodexSdk Build();
}

public sealed class CodexExecFacade
{
    public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken ct = default);
    public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken ct = default);
    public Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken ct = default);
    public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter = null, CancellationToken ct = default);
    public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken ct = default);
}

public sealed class CodexAppServerFacade
{
    public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default);
    public Task<CodexAppServerClient> StartAsync(CodexAppServerClientOptions options, CancellationToken ct = default);
}

public sealed class CodexMcpServerFacade
{
    public Task<CodexMcpServerClient> StartAsync(CancellationToken ct = default);
    public Task<CodexMcpServerClient> StartAsync(CodexMcpServerClientOptions options, CancellationToken ct = default);
}
```

### Example (target usage)

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;

await using var sdk = CodexSdk.Create(b =>
{
    b.ConfigureExec(o => o.SessionsRootDirectory = "<sessions-root>");
    b.ConfigureAppServer(o => o.DefaultClientInfo = new("my_app", "My App", "1.0"));
});

await using var session = await sdk.Exec.StartSessionAsync(
    new CodexSessionOptions("<repo>", "Summarize this repo")
    {
        Model = CodexModel.Gpt52Codex,
        ReasoningEffort = CodexReasoningEffort.Medium
    });

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default))
{
    // ...
}

await using var app = await sdk.AppServer.StartAsync();
await using var mcp = await sdk.McpServer.StartAsync();
```

## Edge Cases

- Starting AppServer/McpServer via facade should not hide startup errors; exceptions must surface as they do today.
- Global `CodexExecutablePath` must not silently override an explicitly set per-mode path.
- Disposing `CodexSdk` must not dispose externally owned dependencies (e.g., an injected `ILoggerFactory`) unless the API explicitly documents ownership.

## Out of Scope

- Redesigning `CodexClient`, `CodexAppServerClient`, or `CodexMcpServerClient` public APIs.
- Forcing a unified protocol surface between modes (they remain different by nature).
- Adding new Codex CLI features.
