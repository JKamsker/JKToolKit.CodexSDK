# Microsoft Agent Framework Adapter

[![NuGet](https://img.shields.io/nuget/v/JKToolKit.CodexSDK.AgentFramework.svg?logo=nuget&label=JKToolKit.CodexSDK.AgentFramework)](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)
[![NuGet Downloads](https://img.shields.io/nuget/dt/JKToolKit.CodexSDK.AgentFramework.svg?logo=nuget)](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)

`JKToolKit.CodexSDK.AgentFramework` makes Codex usable from Microsoft Agent Framework and adapts Agent Framework function tools to Codex app-server dynamic tools.

Use it when you want a Codex-backed `AIAgent`, or when you already have Agent Framework tools represented as `Microsoft.Extensions.AI.AIFunction` instances and want Codex app-server to call them through its `item/tool/call` flow.

NuGet package: [`JKToolKit.CodexSDK.AgentFramework`](https://www.nuget.org/packages/JKToolKit.CodexSDK.AgentFramework)

Microsoft docs: [Agent Framework](https://learn.microsoft.com/en-us/agent-framework/), [function tools](https://learn.microsoft.com/en-us/agent-framework/agents/tools/function-tools), and [context providers](https://learn.microsoft.com/en-us/agent-framework/agents/conversations/context-providers).

## Install

Install the adapter package into the project that owns your Agent Framework tools:

```bash
dotnet add package JKToolKit.CodexSDK.AgentFramework
```

The adapter package depends on `JKToolKit.CodexSDK`, `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, and `Microsoft.Extensions.AI.Abstractions`.

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

Use Agent Framework `ChatClientAgentRunOptions` when you also want Agent Framework chat options, structured output, reasoning options, or function invocation middleware:

```csharp
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

var runOptions = new ChatClientAgentRunOptions(new ChatOptions
{
    ModelId = "gpt-5.5",
    ResponseFormat = ChatResponseFormat.ForJsonSchema<PizzaOrder>(),
    Reasoning = new ReasoningOptions
    {
        Effort = ReasoningEffort.High,
        Output = ReasoningOutput.Summary
    },
    Tools = [getWeather]
}).ConfigureCodex(codex =>
{
    codex.Cwd = Environment.CurrentDirectory;
    codex.ApprovalPolicy = CodexApprovalPolicy.Never;
});

AgentResponse<PizzaOrder> response =
    await agent.RunAsync<PizzaOrder>("Return a small pizza order as JSON.", options: runOptions);
```

Attach Agent Framework function invocation middleware the same way you would for other `AIAgent` implementations:

```csharp
AIAgent middlewareAgent = agent
    .AsBuilder()
    .Use(async (innerAgent, context, next, cancellationToken) =>
    {
        context.Arguments["requestSource"] = "codex";
        return await next(context, cancellationToken);
    })
    .Build();

Console.WriteLine(await middlewareAgent.RunAsync(
    "What is the weather like in Amsterdam?",
    options: new ChatClientAgentRunOptions().ConfigureCodex(codex =>
    {
        codex.Cwd = Environment.CurrentDirectory;
    })));
```

Provide services for `AIFunctionFactory` functions that take an `IServiceProvider` parameter:

```csharp
AIAgent agent = new CodexAgentClient().AsAIAgent(new CodexAIAgentOptions
{
    Instructions = "You are a helpful assistant.",
    Tools = [AIFunctionFactory.Create(ReadFromServices)],
    FunctionInvocationServices = serviceProvider
});
```

Handle `ApprovalRequiredAIFunction` calls with a Codex-side approval callback:

```csharp
using JKToolKit.CodexSDK.AgentFramework.Tools;

AIFunction deleteFile = new ApprovalRequiredAIFunction(
    AIFunctionFactory.Create(DeleteFile, name: "delete_file"));

AIAgent agent = new CodexAgentClient().AsAIAgent(new CodexAIAgentOptions
{
    Tools = [deleteFile],
    ToolApprovalHandler = (request, cancellationToken) =>
    {
        return ValueTask.FromResult(
            request.ToolName == "delete_file"
                ? AgentFrameworkToolApprovalResponse.Reject("File deletion is disabled.")
                : AgentFrameworkToolApprovalResponse.Approve());
    }
});
```

Agent Framework runtime context is available inside tools and function middleware:

```csharp
static string RememberTopic(string topic)
{
    var session = AIAgent.CurrentRunContext?.Session;
    if (session is null)
    {
        return "No session is available.";
    }

    session.StateBag.SetValue("topic", topic);
    return $"Remembered {topic}.";
}

AIAgent agent = new CodexAgentClient().AsAIAgent(
    instructions: "Remember useful project context.",
    tools: [AIFunctionFactory.Create(RememberTopic)]);
```

Use Agent Framework context providers when you want the normal memory/RAG pipeline to enrich Codex runs:

```csharp
AIContextProvider projectContextProvider = new MyProjectContextProvider();

AIAgent agent = new CodexAgentClient().AsAIAgent(new CodexAIAgentOptions
{
    Instructions = "Use project context when it is relevant.",
    AIContextProviders = [projectContextProvider],
    ChatHistoryProvider = new InMemoryChatHistoryProvider(),
    Tools = [AIFunctionFactory.Create(GetWeather)]
});
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
- `CodexAgentSession` stores the backing Codex thread id and the Agent Framework session state bag. Serialize and deserialize the session through the Agent Framework APIs to resume the same Codex thread later.
- When a run does not pass a session, the Codex agent creates one and updates `AIAgent.CurrentRunContext` so Agent Framework tools can still read `CurrentRunContext.Session` and `CurrentRunContext.RunOptions`.
- `AIContextProviders` run before Codex starts the turn. Provider instructions and tools are merged into `ChatOptions`; provider messages are sent as turn input; providers are notified after success or failure.
- `ChatHistoryProvider` is opt-in because Codex threads already maintain conversation state. Add one only when you explicitly want Agent Framework-managed history or memory enrichment.
- Codex dynamic tools are currently behind the app-server experimental API capability. The native `AIAgent` surface enables it automatically when tools are present.
- Tool names are the `AIFunction.Name` values supplied by Agent Framework.
- Agent Framework tools must currently be `AIFunction` instances. Hosted Agent Framework tools such as provider-native code interpreter, file search, or web search are not translated to Codex dynamic tools.
- Per-run tools and context-provider tools can be used when creating a new Codex thread. Once a `CodexAgentSession` has a thread id, create a new session to use a different tool set.
- Function invocation middleware is supported for default tools and `ChatClientAgentRunOptions` tools through `ChatClientFactory`. When combining it with Codex-specific settings, attach those settings with `ConfigureCodex(...)`; Agent Framework's function middleware only accepts no options, base `AgentRunOptions`, or `ChatClientAgentRunOptions`.
- `ChatClientAgentRunOptions.ChatClientFactory` is used only to apply Agent Framework option/tool transformations before Codex runs. Codex is not an `IChatClient`, so chat-client middleware cannot observe the raw Codex model request or response.
- `ApprovalRequiredAIFunction` is supported through `toolApprovalHandler`. Codex dynamic tool calls are synchronous, so the adapter does not use Agent Framework `FunctionApprovalRequestContent` / `FunctionApprovalResponseContent` round trips. Without an approval handler, approval-required functions are rejected before invocation.
