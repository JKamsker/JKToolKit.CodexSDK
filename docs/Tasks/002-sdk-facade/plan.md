````markdown
# Implementation Plan: CodexSdk Facade (Exec + AppServer + McpServer)

**Branch**: `002-sdk-facade` | **Date**: February 2, 2026 | **Spec**: [spec.md](./spec.md)

## Summary

Add a small, additive facade layer that provides a single, discoverable entry point (`CodexSdk`) for the three supported Codex integration modes:

- `sdk.Exec` → wraps `JKToolKit.CodexSDK.Exec.CodexClient`
- `sdk.AppServer` → starts `JKToolKit.CodexSDK.AppServer.CodexAppServerClient`
- `sdk.McpServer` → starts `JKToolKit.CodexSDK.McpServer.CodexMcpServerClient`

The facade does **not** replace existing entry points. It is a convenience surface for:

1. Non-DI consumers who want one root object.
2. DI consumers who want one registration call (`services.AddCodexSdk(...)`).

## Technical Context

- **Language/Version**: .NET 10 (`net10.0`)
- **Existing entry points**:
  - Exec: `Public/CodexClient.cs`
  - AppServer: `AppServer/CodexAppServerClient.cs` (+ `ICodexAppServerClientFactory`)
  - McpServer: `McpServer/CodexMcpServerClient.cs` (+ `ICodexMcpServerClientFactory`)
- **Shared bootstrap** already exists for stdio JSON-RPC:
  - `Infrastructure/CodexJsonRpcBootstrap.cs`
- **Shared DI infra** already exists:
  - `Infrastructure/InternalServiceCollectionExtensions.cs`

### Design Constraints

- **No breaking changes**: all existing public APIs keep working.
- **No heavy new dependencies**: avoid introducing an internal `ServiceProvider` build path.
- **Respect advanced overrides when using DI**: if a user overrides `ICodexPathProvider`, etc., the DI-built `CodexSdk` must use them.

## Proposed Public API

### `CodexSdk`

Namespace: `JKToolKit.CodexSDK`

- Properties:
  - `CodexExecFacade Exec`
  - `CodexAppServerFacade AppServer`
  - `CodexMcpServerFacade McpServer`

- Construction paths:
  1. **DI-first** constructor:
     - `CodexSdk(ICodexClient exec, ICodexAppServerClientFactory appServer, ICodexMcpServerClientFactory mcpServer)`
     - This path is used by `services.AddCodexSdk(...)`
  2. **Non-DI convenience**:
     - `CodexSdk.Create(Action<CodexSdkBuilder>? configure = null)`
     - Internally wires defaults using existing internal factories (`CodexAppServerClientFactory`, `CodexMcpServerClientFactory`) and `CodexClient`.

### `CodexSdkBuilder`

- Holds three option objects:
  - `CodexClientOptions` (exec)
  - `CodexAppServerClientOptions`
  - `CodexMcpServerClientOptions`

- Holds optional shared configuration:
  - `string? CodexExecutablePath` (global)
  - `ILoggerFactory? LoggerFactory`

- Applies precedence:
  - If `builder.CodexExecutablePath` is set and a mode-specific `CodexExecutablePath` is **null**, propagate.
  - If a mode-specific `CodexExecutablePath` is set explicitly, keep it.

- Builds:
  - `ICodexClient` via `new CodexClient(Options.Create(execOptions), loggerFactory: loggerFactory)`
  - `ICodexAppServerClientFactory` via `new CodexAppServerClientFactory(Options.Create(appOptions), stdioFactory, loggerFactory)`
  - `ICodexMcpServerClientFactory` via `new CodexMcpServerClientFactory(Options.Create(mcpOptions), stdioFactory, loggerFactory)`

Then constructs `CodexSdk` using the DI-first constructor.

### Facades

- `CodexExecFacade`:
  - Thin delegation layer around `ICodexClient` to keep the “sdk.Exec.*” surface coherent.

- `CodexAppServerFacade`:
  - `StartAsync(CancellationToken ct)` uses the injected `ICodexAppServerClientFactory`.
  - `StartAsync(CodexAppServerClientOptions options, CancellationToken ct)` is optional (nice-to-have). If implemented, it should start a client directly using the same logger factory (or use internal factory w/ `Options.Create`).

- `CodexMcpServerFacade`:
  - Same as AppServer.

## DI Integration

Add a new extension method:

- `services.AddCodexSdk(Action<CodexClientOptions>? exec = null, Action<CodexAppServerClientOptions>? appServer = null, Action<CodexMcpServerClientOptions>? mcpServer = null)`

Implementation approach:

1. Call existing registration helpers:
   - `services.AddCodexClient(exec)`
   - `services.AddCodexAppServerClient(appServer)`
   - `services.AddCodexMcpServerClient(mcpServer)`
2. Register `CodexSdk` as singleton:
   - Resolve `ICodexClient`, `ICodexAppServerClientFactory`, `ICodexMcpServerClientFactory`
   - Construct `CodexSdk`

**Logging note**: `AddCodexSdk` should not force logging providers. Docs should mention that callers typically want `services.AddLogging()`.

## Code Layout

Add new files under `src/JKToolKit.CodexSDK/Facade/`:

```text
src/JKToolKit.CodexSDK/
├── Facade/
│   ├── CodexSdk.cs
│   ├── CodexSdkBuilder.cs
│   ├── CodexExecFacade.cs
│   ├── CodexAppServerFacade.cs
│   └── CodexMcpServerFacade.cs
└── ServiceCollectionExtensions.cs   # new (namespace JKToolKit.CodexSDK) OR add to existing Public extensions (pick one)
```

## Testing Strategy

### Unit tests (fast)

- Facade delegates correctly:
  - `CodexExecFacade` calls underlying `ICodexClient`.
  - `CodexAppServerFacade.StartAsync()` calls the factory.
  - `CodexMcpServerFacade.StartAsync()` calls the factory.

- Builder precedence rules:
  - Global `CodexExecutablePath` flows to all modes when mode-specific path is null.
  - Mode-specific path wins over global.

### DI resolution test

- Build a minimal `ServiceCollection`:
  - Call `AddCodexSdk(...)`.
  - Register required `ILoggerFactory` (can use `NullLoggerFactory.Instance`).
  - Ensure `CodexSdk` resolves and its properties are non-null.

### E2E tests (optional)

- No additional E2E tests are required beyond existing Exec/AppServer/Mcp E2E coverage. If desired, add a single E2E smoke test that uses `CodexSdk.Create(...)` to start each mode (guarded by `CodexE2EFact`).

## Documentation Updates

- Add a new `specs/002-sdk-facade/quickstart.md` demonstrating the facade.
- Update root `README.md` to include a short "Facade" section (optional but recommended).
- Ensure XML docs for new public types.

## Rollout

- Ship as additive changes in the same package.
- Keep existing docs/examples valid.
- Prefer recommending `CodexSdk` for new users while keeping older docs working.

````
