# Manual Testing: DI + Override Hooks (Scratch)

Some features are easiest to validate by running a tiny scratch console app that:

- uses the DI registration helpers (`AddCodexSdk`, `AddCodexAppServerClient`, etc.)
- installs transformers/mappers/observers and confirms they run

This avoids committing temporary code into the repo.

## 0) Create a scratch console app

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

## 1) DI: `AddCodexSdk(...)` smoke test

Replace `Program.cs`:

```powershell
@'
using JKToolKit.CodexSDK;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCodexSdk();

using var sp = services.BuildServiceProvider();
await using var sdk = sp.GetRequiredService<CodexSdk>();

// Just ensure all facades can start; keep it quick.
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

await using var app = await sdk.AppServer.StartAsync(cts.Token);
await using var mcp = await sdk.McpServer.StartAsync(cts.Token);

Console.WriteLine("ok");
'@ | Set-Content .\\Program.cs

dotnet run
```

Verify it prints `ok` and exits successfully.

## 2) App-server message observers + request param transformers (invocation check)

Replace `Program.cs`:

```powershell
@'
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCodexAppServerClient(o =>
{
    o.MessageObservers.Add(new ConsoleObserver());
    o.RequestParamsTransformers.Add(new NoopTransformer());
});

using var sp = services.BuildServiceProvider();
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

## 3) MCP override hooks: response transformer + tools list mapper

Replace `Program.cs`:

```powershell
@'
using JKToolKit.CodexSDK.McpServer;

var opts = new CodexMcpServerClientOptions();
opts.ResponseTransformers.Add(new MarkerTransformer());
opts.ToolsListMappers.Add(new PassthroughMapper());

await using var client = await CodexMcpServerClient.StartAsync(opts);
_ = await client.ListToolsAsync();

Console.WriteLine("ok");

file sealed class MarkerTransformer : ICodexMcpResponseTransformer
{
    public System.Text.Json.JsonElement Transform(string method, System.Text.Json.JsonElement result)
    {
        Console.WriteLine($"[transformer] {method}");
        return result;
    }
}

file sealed class PassthroughMapper : ICodexMcpToolsListMapper
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

## Cleanup

```powershell
Pop-Location
Remove-Item -Recurse -Force $tmp
```
