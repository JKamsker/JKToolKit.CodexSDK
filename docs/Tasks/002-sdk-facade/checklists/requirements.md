# Requirements Checklist: CodexSdk Facade

Use this checklist during implementation / PR review.

## API surface

- [x] `CodexSdk` exists in `namespace JKToolKit.CodexSDK`.
- [x] `CodexSdk` exposes:
  - [x] `Exec`
  - [x] `AppServer`
  - [x] `McpServer`
- [x] Existing entry points remain unchanged (`CodexClient`, `CodexAppServerClient`, `CodexMcpServerClient`).

## Non-DI path

- [x] `CodexSdk.Create(...)` works with no arguments.
- [x] `CodexSdkBuilder` can configure each mode.
- [x] Global `CodexExecutablePath` flows to all modes only when per-mode paths are null.

## DI path

- [x] `services.AddCodexSdk(...)` exists.
- [x] `AddCodexSdk` calls the existing registration helpers and registers `CodexSdk`.
- [x] DI overrides for abstractions (e.g., `ICodexPathProvider`) are respected.

## Testing

- [x] Builder precedence unit tests.
- [x] Facade delegation unit tests.
- [x] DI resolution test.

## Documentation

- [x] A facade example exists in `README.md` (or linked docs).
- [x] New public types have XML documentation.
