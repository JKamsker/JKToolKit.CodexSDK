# Quickstart: CodexSdk Facade

This quickstart shows the intended "nice facade" usage once `CodexSdk` is implemented.

> This is additive. All existing entry points (`CodexClient`, `CodexAppServerClient`, `CodexMcpServerClient`) continue to work.

## Non-DI usage

### Create once, start any mode

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Models;

await using var sdk = CodexSdk.Create(b =>
{
    // One place to set the Codex executable path (optional)
    b.CodexExecutablePath = "<path-to-codex>";

    // Mode-specific config (optional)
    b.ConfigureExec(o =>
    {
        o.SessionsRootDirectory = "<sessions-root>";
    });

    b.ConfigureAppServer(o =>
    {
        o.DefaultClientInfo = new("my_app", "My App", "1.0.0");
    });
});

// Exec mode
await using var session = await sdk.Exec.StartSessionAsync(
    new CodexSessionOptions("<repo>", "Summarize this repository")
    {
        Model = CodexModel.Gpt52Codex,
        ReasoningEffort = CodexReasoningEffort.Medium
    });

await foreach (var evt in session.GetEventsAsync(EventStreamOptions.Default))
{
    // ...
}

// AppServer mode
await using var app = await sdk.AppServer.StartAsync();

// McpServer mode
await using var mcp = await sdk.McpServer.StartAsync();
```

## DI usage

### Register everything

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.Exec;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Typical host apps will also have:
// services.AddLogging();

services.AddCodexSdk(
    exec: o => { /* configure CodexClientOptions */ },
    appServer: o => { /* configure CodexAppServerClientOptions */ },
    mcpServer: o => { /* configure CodexMcpServerClientOptions */ });

var sp = services.BuildServiceProvider();

var sdk = sp.GetRequiredService<CodexSdk>();

await using var app = await sdk.AppServer.StartAsync();
```

### Notes

- `AddCodexSdk(...)` is expected to call:
  - `AddCodexClient(...)`
  - `AddCodexAppServerClient(...)`
  - `AddCodexMcpServerClient(...)`
  - and register `CodexSdk`.
- If you override abstractions like `ICodexPathProvider`, do so before building the provider; the `CodexSdk` resolved from DI will respect those overrides.
