# Microsoft Agent Framework Adapter

[![NuGet](https://img.shields.io/nuget/v/JKToolKit.CodexSDK.AgentFramework.svg?logo=nuget&label=JKToolKit.CodexSDK.AgentFramework)](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)
[![NuGet Downloads](https://img.shields.io/nuget/dt/JKToolKit.CodexSDK.AgentFramework.svg?logo=nuget)](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)

`JKToolKit.CodexSDK.AgentFramework` makes Codex usable from Microsoft Agent Framework and adapts Agent Framework function tools to Codex app-server dynamic tools.

Use it when you want a Codex-backed `AIAgent`, or when you already have Agent Framework tools represented as `Microsoft.Extensions.AI.AIFunction` instances and want Codex app-server to call them through its `item/tool/call` flow.

NuGet package: [`JKToolKit.CodexSDK.AgentFramework`](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)

Microsoft docs: [Agent Framework](https://learn.microsoft.com/en-us/agent-framework/) and [function tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools).

## Install

Install the adapter package into the project that owns your Agent Framework tools:

```bash
dotnet add package JKToolKit.CodexSDK.AgentFramework
```

The adapter package depends on `JKToolKit.CodexSDK`, `Microsoft.Agents.AI`, and `Microsoft.Extensions.AI.Abstractions`.

## Usage

Create a Codex-backed Agent Framework agent:

```csharp
using System.Text.Json;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIFunction getWeather = AIFunctionFactory.Create(
    (Func<string, string>)(location => $"Weather in {location}: cloudy."),
    name: "get_weather",
    description: "Gets the weather for a location.");

AIAgent agent = new CodexAgentClient()
    .AsAIAgent(
        model: "gpt-5.5",
        instructions: "You are a helpful assistant.",
        tools: [getWeather]);

Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

Use sessions for multi-turn conversations:

```csharp
AgentSession session = await agent.CreateSessionAsync();

Console.WriteLine(await agent.RunAsync("My name is Alice.", session));
Console.WriteLine(await agent.RunAsync("What is my name?", session));

JsonElement serialized = await agent.SerializeSessionAsync(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

Stream responses:

```csharp
await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("Order a pizza."))
{
    Console.Write(update.Text);
}
```

Configure Codex-specific run options:

```csharp
var runOptions = new CodexAgentRunOptions
{
    Cwd = Environment.CurrentDirectory,
    ApprovalPolicy = CodexApprovalPolicy.Never,
    Sandbox = CodexSandboxMode.ReadOnly
};

Console.WriteLine(await agent.RunAsync("Inspect the current project.", options: runOptions));
```

The lower-level adapter remains available when you want to manually wire Codex app-server:

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AgentFramework.Tools;
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

- `CodexAgentClient().AsAIAgent(...)` returns a normal Agent Framework `AIAgent`, so Agent Framework middleware, workflows, `RunAsync<T>`, `RunStreamingAsync`, and `AIAgent.AsAIFunction(...)` can be used on top of it.
- `CodexAgentSession` stores the backing Codex thread id. Serialize and deserialize the session through the Agent Framework APIs to resume the same Codex thread later.
- Codex dynamic tools are currently behind the app-server experimental API capability. The native `AIAgent` surface enables it automatically when tools are present.
- Tool names are the `AIFunction.Name` values supplied by Agent Framework.
- Agent Framework tools must currently be `AIFunction` instances. Hosted Agent Framework tools such as provider-native code interpreter, file search, or web search are not translated to Codex dynamic tools.
- Per-run tools can be used when creating a new Codex thread. Once a `CodexAgentSession` has a thread id, create a new session to use a different tool set.
- Agent Framework `ApprovalRequiredAIFunction` is callable because it is an `AIFunction`, but Codex does not yet surface Agent Framework `ToolApprovalRequestContent` / `ToolApprovalResponseContent` in the same HITL shape.
