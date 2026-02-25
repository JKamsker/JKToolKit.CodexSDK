# Manual Testing: DI + Override Hooks

Validate:

- Microsoft.Extensions.DependencyInjection registration helpers (`AddCodexSdk`, `AddCodexAppServerClient`, etc.)
- Override hooks are invoked:
  - App-server: `IAppServerMessageObserver`, `IAppServerRequestParamsTransformer`
  - MCP-server: `IMcpServerResponseTransformer`, `IMcpToolsListMapper`

## 1) Demo command (recommended)

```powershell
dotnet run --project src/JKToolKit.CodexSDK.Demo -- di-overrides --timeout-seconds 120
```

Verify:

- You see `[observer] request skills/list ...` and a corresponding response line.
- You see `[request-transformer] skills/list ...`.
- You see `[mcp-response-transformer] tools/list`.
- You see `[tools-list-mapper] invoked`.
- It prints `ok` and exits successfully.

## 2) Scratch console app (optional)

This option is closer to a real consumer app and avoids committing temporary code into the repo.

### 2.1) Create a scratch console app

```powershell
$repo = (Get-Location).Path
$tmp = Join-Path $env:TEMP ("codexsdk-scratch-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tmp | Out-Null

Push-Location $tmp
dotnet new console -n CodexSdkScratch | Out-Null
Set-Location .\\CodexSdkScratch

# Reference the SDK project directly (tests the repo version you’re working on)
dotnet add reference "$repo\\src\\JKToolKit.CodexSDK\\JKToolKit.CodexSDK.csproj"
dotnet add package Microsoft.Extensions.DependencyInjection
```

### 2.2) DI: `AddCodexSdk(...)` smoke test

Replace `Program.cs`:

```powershell
@'
using JKToolKit.CodexSDK;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var services = new ServiceCollection();

// Minimal logging registrations (the SDK depends on ILogger<T> via DI)
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

services.AddCodexSdk();

// ServiceProvider must be disposed asynchronously because CodexSdk is IAsyncDisposable.
await using var sp = services.BuildServiceProvider();
var sdk = sp.GetRequiredService<CodexSdk>();

// Just ensure all facades can start; keep it quick.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

await using var app = await sdk.AppServer.StartAsync(cts.Token);
await using var mcp = await sdk.McpServer.StartAsync(cts.Token);

Console.WriteLine("ok");
'@ | Set-Content .\\Program.cs

dotnet run
```

Verify it prints `ok` and exits successfully.

### 2.3) App-server message observers + request param transformers (invocation check)

Replace `Program.cs`:

```powershell
@'
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Overrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var services = new ServiceCollection();
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

services.AddCodexAppServerClient(o =>
{
    o.MessageObservers = new IAppServerMessageObserver[] { new ConsoleObserver() };
    o.RequestParamsTransformers = new IAppServerRequestParamsTransformer[] { new NoopTransformer() };
});

await using var sp = services.BuildServiceProvider();
var factory = sp.GetRequiredService<ICodexAppServerClientFactory>();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
await using var codex = await factory.StartAsync(cts.Token);

// Trigger at least one request/response.
_ = await codex.ListSkillsAsync(new SkillsListOptions { Cwd = Environment.CurrentDirectory }, cts.Token);

Console.WriteLine("ok");

file sealed class ConsoleObserver : IAppServerMessageObserver
{
    public void OnRequest(string method, System.Text.Json.JsonElement @params) =>
        Console.WriteLine($"[observer] request {method} paramsKind={@params.ValueKind}");

    public void OnResponse(string method, System.Text.Json.JsonElement result) =>
        Console.WriteLine($"[observer] response {method} resultKind={result.ValueKind}");

    public void OnNotification(string method, System.Text.Json.JsonElement @params) =>
        Console.WriteLine($"[observer] notification {method} paramsKind={@params.ValueKind}");
}

file sealed class NoopTransformer : IAppServerRequestParamsTransformer
{
    public System.Text.Json.JsonElement Transform(string method, System.Text.Json.JsonElement @params) => @params;
}
'@ | Set-Content .\\Program.cs

dotnet run
```

Verify:

- You see `[observer] request skills/list ...` and a corresponding response line.
- It prints `ok`.

### 2.4) MCP override hooks: response transformer + tools list mapper

Replace `Program.cs`:

```powershell
@'
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.McpServer.Overrides;

var opts = new CodexMcpServerClientOptions
{
    ResponseTransformers = new IMcpServerResponseTransformer[] { new MarkerTransformer() },
    ToolsListMappers = new IMcpToolsListMapper[] { new PassthroughMapper() }
};

await using var client = await CodexMcpServerClient.StartAsync(opts);
_ = await client.ListToolsAsync();

Console.WriteLine("ok");

file sealed class MarkerTransformer : IMcpServerResponseTransformer
{
    public System.Text.Json.JsonElement Transform(string method, System.Text.Json.JsonElement result)
    {
        Console.WriteLine($"[transformer] {method}");
        return result;
    }
}

file sealed class PassthroughMapper : IMcpToolsListMapper
{
    public IReadOnlyList<McpToolDescriptor>? TryMap(System.Text.Json.JsonElement raw)
    {
        Console.WriteLine("[tools-list-mapper] invoked");
        return null; // fall back to default parser
    }
}
'@ | Set-Content .\\Program.cs

dotnet run
```

Verify:

- You see `[transformer] tools/list`
- You see `[tools-list-mapper] invoked`
- It prints `ok`

### Cleanup

```powershell
Pop-Location

# Use either:
Remove-Item -Recurse -Force $tmp

# ...or (when Remove-Item is restricted):
cmd /c rmdir /s /q "$tmp"
```
