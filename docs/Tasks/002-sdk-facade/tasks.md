---
description: "Task breakdown for CodexSdk facade + unified DI registration"
---

# Tasks: CodexSdk Facade

**Input**: `specs/002-sdk-facade/{spec.md,plan.md}`  
**Goal**: Add an additive facade (`CodexSdk`) that exposes `Exec`, `AppServer`, and `McpServer` from a single root, plus a one-call DI registration `AddCodexSdk(...)`.

**Notes**

- Keep existing entry points intact (`CodexClient`, `CodexAppServerClient`, `CodexMcpServerClient`).
- Prefer *thin delegation* (no new behavior).
- Use the existing internal factories (`CodexAppServerClientFactory`, `CodexMcpServerClientFactory`) for the non-DI build path to avoid duplicating bootstrap logic.

## Phase 0 — API decisions (must happen first)

- [ ] T001 Decide final namespaces + file locations for new public types:
  - `CodexSdk` should live in `namespace JKToolKit.CodexSDK`.
  - New source folder: `src/JKToolKit.CodexSDK/Facade/`.
- [ ] T002 Decide whether `sdk.AppServer.StartAsync(CodexAppServerClientOptions)` (options override per-call) is required for v1.
  - If not required, keep `StartAsync()` as the only method and rely on builder/DI for configuration.

## Phase 1 — Implement the facade types

- [ ] T010 [P] Add `CodexExecFacade` (thin wrapper over `ICodexClient`).
  - File: `src/JKToolKit.CodexSDK/Facade/CodexExecFacade.cs`
  - Delegates: `StartSessionAsync`, `ResumeSessionAsync`, `AttachToLogAsync`, `ListSessionsAsync`, `ReviewAsync`, `GetRateLimitsAsync` (if desired).

- [ ] T011 [P] Add `CodexAppServerFacade` (thin wrapper over `ICodexAppServerClientFactory`).
  - File: `src/JKToolKit.CodexSDK/Facade/CodexAppServerFacade.cs`
  - Required: `StartAsync(CancellationToken)` delegates to factory.
  - Optional: `StartAsync(CodexAppServerClientOptions, CancellationToken)` for per-call overrides.

- [ ] T012 [P] Add `CodexMcpServerFacade` (thin wrapper over `ICodexMcpServerClientFactory`).
  - File: `src/JKToolKit.CodexSDK/Facade/CodexMcpServerFacade.cs`
  - Required: `StartAsync(CancellationToken)` delegates to factory.
  - Optional: `StartAsync(CodexMcpServerClientOptions, CancellationToken)` for per-call overrides.

- [ ] T013 Add `CodexSdk` root type.
  - File: `src/JKToolKit.CodexSDK/Facade/CodexSdk.cs`
  - DI-friendly constructor: `CodexSdk(ICodexClient exec, ICodexAppServerClientFactory app, ICodexMcpServerClientFactory mcp)`.
  - Properties: `Exec`, `AppServer`, `McpServer`.
  - Implement `IAsyncDisposable` (dispose only what `CodexSdk` owns).

## Phase 2 — Non-DI convenience builder

- [ ] T020 Add `CodexSdkBuilder`.
  - File: `src/JKToolKit.CodexSDK/Facade/CodexSdkBuilder.cs`
  - Holds options:
    - `CodexClientOptions` (exec)
    - `CodexAppServerClientOptions`
    - `CodexMcpServerClientOptions`
  - Holds shared settings:
    - `string? CodexExecutablePath` (global)
    - `ILoggerFactory? LoggerFactory`
  - Applies precedence rules from spec (global path applies only if per-mode path is null).
  - Builds using:
    - `new CodexClient(Options.Create(execOptions), loggerFactory: loggerFactory)`
    - internal `CodexAppServerClientFactory` + `CodexMcpServerClientFactory`
    - `CodexJsonRpcBootstrap.CreateDefaultStdioFactory(loggerFactory)`

- [ ] T021 Add `CodexSdk.Create(Action<CodexSdkBuilder>? configure = null)`.
  - Convenience entry point for non-DI users.
  - Must work with defaults (no configuration).

## Phase 3 — Unified DI registration

- [ ] T030 Add `services.AddCodexSdk(...)`.
  - File: `src/JKToolKit.CodexSDK/ServiceCollectionExtensions.cs` (namespace `JKToolKit.CodexSDK`).
  - Implementation:
    - Calls existing `AddCodexClient`, `AddCodexAppServerClient`, `AddCodexMcpServerClient`.
    - Registers `CodexSdk` as singleton.
  - Do **not** require logging providers; docs should mention `services.AddLogging()`.

## Phase 4 — Tests

- [ ] T040 [P] Unit tests: `CodexSdkBuilder` precedence rules.
  - File: `tests/JKToolKit.CodexSDK.Tests/Unit/CodexSdkBuilderTests.cs`

- [ ] T041 [P] Unit tests: `CodexExecFacade` delegates to `ICodexClient`.
  - Use a minimal fake/mock `ICodexClient` implementation.

- [ ] T042 [P] Unit tests: `CodexAppServerFacade` + `CodexMcpServerFacade` delegate to factories.
  - Use minimal fake factories that record invocations.

- [ ] T043 DI test: `AddCodexSdk` resolves.
  - Build a `ServiceCollection`, register `ILoggerFactory` (e.g. `NullLoggerFactory.Instance`), call `AddCodexSdk`, build provider, resolve `CodexSdk`.

- [ ] T044 (Optional) E2E smoke test (guarded by `CodexE2EFact`) that uses `CodexSdk.Create(...)` and starts:
  - `sdk.AppServer.StartAsync()`
  - `sdk.McpServer.StartAsync()`
  - (Exec mode is already covered elsewhere)

## Phase 5 — Documentation

- [ ] T050 Add `specs/002-sdk-facade/quickstart.md` with copy/paste examples for all three modes.
- [ ] T051 Update root `README.md` to mention the new facade as the recommended entry point for new users.
- [ ] T052 Ensure XML docs for all new public types and members.

## Phase 6 — Polish

- [ ] T060 Verify no public API breaks (build + run unit tests).
- [ ] T061 Verify analyzers/style (nullable annotations, argument validation, cancellation token passthrough).
