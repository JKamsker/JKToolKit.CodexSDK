# Semantic Kernel Adapter

[![NuGet](https://img.shields.io/nuget/v/JKToolKit.CodexSDK.SemanticKernel.svg?logo=nuget&label=JKToolKit.CodexSDK.SemanticKernel)](https://www.nuget.org/packages/JKToolKit.CodexSDK.SemanticKernel)
[![NuGet Downloads](https://img.shields.io/nuget/dt/JKToolKit.CodexSDK.SemanticKernel.svg?logo=nuget)](https://www.nuget.org/packages/JKToolKit.CodexSDK.SemanticKernel)

`JKToolKit.CodexSDK.SemanticKernel` adapts Semantic Kernel native functions to Codex app-server dynamic tools.

This is useful when you already have SK plugins marked with `[KernelFunction]` and want Codex app-server to call them through its `item/tool/call` flow.

NuGet package: [`JKToolKit.CodexSDK.SemanticKernel`](https://www.nuget.org/packages/JKToolKit.CodexSDK.SemanticKernel)

## Install

Install the adapter package into the project that owns your Semantic Kernel plugins:

```bash
dotnet add package JKToolKit.CodexSDK.SemanticKernel
```

The adapter package depends on `JKToolKit.CodexSDK` and `Microsoft.SemanticKernel.Abstractions`. If your project does not already reference Semantic Kernel's core package, add it too:

```bash
dotnet add package Microsoft.SemanticKernel.Core --version 1.75.0
```

## Usage

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.SemanticKernel;
using Microsoft.SemanticKernel;

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromObject(new OrderPizzaPlugin(), "OrderPizza");
var kernel = kernelBuilder.Build();

var skTools = SemanticKernelCodexToolAdapter.Create(kernel);

await using var sdk = CodexSdk.Create(builder =>
{
    builder.ConfigureAppServer(o =>
    {
        o.ExperimentalApi = true;
        o.ApprovalHandler = skTools.ApprovalHandler;
    });
});

await using var codex = await sdk.AppServer.StartAsync();
var thread = await codex.StartThreadAsync(new ThreadStartOptions
{
    Cwd = Environment.CurrentDirectory,
    Sandbox = CodexSandboxMode.ReadOnly,
    ApprovalPolicy = CodexApprovalPolicy.Never,
    DynamicTools = skTools.DynamicTools
});

await using var turn = await codex.StartTurnAsync(thread.Id, new TurnStartOptions
{
    Input = [TurnInputItem.Text("Order one large pepperoni pizza and checkout.")]
});
```

Run the end-to-end pizza demo:

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- sk-function-calling --repo "<repo-path>"
```

The demo inherits the default model from your Codex configuration. To force a model, pass `--model <MODEL>`, for example:

```bash
dotnet run --project src/JKToolKit.CodexSDK.Demo -- sk-function-calling --model gpt-5.5
```

## Notes

- Codex dynamic tools are currently behind the app-server experimental API capability, so set `CodexAppServerClientOptions.ExperimentalApi = true`.
- Tool names use Semantic Kernel's plugin/function naming pattern: `PluginName-function_name`.
- Unknown app-server server requests are delegated to the optional fallback handler passed to `SemanticKernelCodexToolAdapter.Create`.
