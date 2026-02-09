# Requirements Checklist: CodexSdk Facade

Use this checklist during implementation / PR review.

## API surface

- [ ] `CodexSdk` exists in `namespace JKToolKit.CodexSDK`.
- [ ] `CodexSdk` exposes:
  - [ ] `Exec`
  - [ ] `AppServer`
  - [ ] `McpServer`
- [ ] Existing entry points remain unchanged (`CodexClient`, `CodexAppServerClient`, `CodexMcpServerClient`).

## Non-DI path

- [ ] `CodexSdk.Create(...)` works with no arguments.
- [ ] `CodexSdkBuilder` can configure each mode.
- [ ] Global `CodexExecutablePath` flows to all modes only when per-mode paths are null.

## DI path

- [ ] `services.AddCodexSdk(...)` exists.
- [ ] `AddCodexSdk` calls the existing registration helpers and registers `CodexSdk`.
- [ ] DI overrides for abstractions (e.g., `ICodexPathProvider`) are respected.

## Testing

- [ ] Builder precedence unit tests.
- [ ] Facade delegation unit tests.
- [ ] DI resolution test.

## Documentation

- [ ] A facade example exists in `README.md` (or linked docs).
- [ ] New public types have XML documentation.
