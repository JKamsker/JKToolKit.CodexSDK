# Microsoft Agent Framework Adapter

[![NuGet](https://img.shields.io/nuget/v/JKToolKit.CodexSDK.AgentFramework.svg?logo=nuget&label=JKToolKit.CodexSDK.AgentFramework)](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)
[![NuGet Downloads](https://img.shields.io/nuget/dt/JKToolKit.CodexSDK.AgentFramework.svg?logo=nuget)](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)

`JKToolKit.CodexSDK.AgentFramework` adapts Microsoft Agent Framework function tools to Codex app-server dynamic tools.

This is useful when you already have Agent Framework tools represented as `Microsoft.Extensions.AI.AIFunction` instances and want Codex app-server to call them through its `item/tool/call` flow.

NuGet package: [`JKToolKit.CodexSDK.AgentFramework`](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)

Microsoft docs: [Agent Framework](https://learn.microsoft.com/en-us/agent-framework/) and [function tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools).

## Install

Install the adapter package into the project that owns your Agent Framework tools:

```bash
dotnet add package JKToolKit.CodexSDK.AgentFramework
```

The adapter package depends on `JKToolKit.CodexSDK` and `Microsoft.Extensions.AI.Abstractions`.

## Usage

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AgentFramework;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using Microsoft.Extensions.AI;

AIFunction getWeather = AIFunctionFactory.Create(
    (Func<string, string>)(location => $"Weather in {location}: cloudy."),
    name: "get_weather",
    description: "Gets the weather for a location.");

var agentFrameworkTools = AgentFrameworkCodexToolAdapter.Create([getWeather]);

await using var sdk = CodexSdk.Create(builder =>
{
    builder.ConfigureAppServer(o =>
    {
        o.ExperimentalApi = true;
        o.ApprovalHandler = agentFrameworkTools.ApprovalHandler;
    });
});

await using var codex = await sdk.AppServer.StartAsync();
var thread = await codex.StartThreadAsync(new ThreadStartOptions
{
    Cwd = Environment.CurrentDirectory,
    Sandbox = CodexSandboxMode.ReadOnly,
    ApprovalPolicy = CodexApprovalPolicy.Never,
    DynamicTools = agentFrameworkTools.DynamicTools
});

await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
{
    Input = [TurnInputItem.Text("Use the weather tool for Vienna.")]
});
```

Run the end-to-end pizza demo:

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- agent-framework-function-calling --repo "<repo-path>"
```

The demo inherits the default model from your Codex configuration. To force a model, pass `--model <MODEL>`, for example:

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- agent-framework-function-calling --model gpt-5.5
```

## Notes

- Codex dynamic tools are currently behind the app-server experimental API capability, so set `CodexAppServerClientOptions.ExperimentalApi = true`.
- Tool names are the `AIFunction.Name` values supplied by Agent Framework.
- Unknown app-server server requests are delegated to the optional fallback handler passed to `AgentFrameworkCodexToolAdapter.Create`.
- Agent Framework agents can also be exposed to other tool callers with `AIAgent.AsAIFunction(...)`; pass those `AIFunction` instances to the adapter the same way.
