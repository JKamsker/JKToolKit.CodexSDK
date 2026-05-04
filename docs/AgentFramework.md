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
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

AIFunction getWeather = AIFunctionFactory.Create(
    (Func<string, string>)(location => $"Weather in {location}: cloudy."),
    name: "get_weather",
    description: "Gets the weather for a location.");

await using var sdk = CodexSdk.Create(builder =>
{
    builder.ConfigureAppServer(options =>
    {
        options.CodexHomeDirectory = Environment.GetEnvironmentVariable("CODEX_HOME");
    });
});

AIAgent agent = sdk
    .AsAIAgent(
        model: "gpt-5.5",
        instructions: "You are a helpful assistant.",
        tools: [getWeather]);

Console.WriteLine(await agent.RunAsync("What is the weather like in Amsterdam?"));
```

You can also use `new CodexAgentClient(configureSdk).AsAIAgent(...)` when you want the agent to create a fresh SDK facade for each run.

## Remote App Servers

The Agent Framework adapter can run against the same remote app-server transports as the core SDK.

For process-bound remote stdio, configure the app-server launch on the SDK builder. This starts `codex app-server` over SSH or Docker for each agent run:

```csharp
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;

AIAgent sshAgent = new CodexAgentClient(builder =>
{
    builder.ConfigureAppServer(options =>
    {
        options.Launch = CodexLaunchRemote.SshAppServer(
            host: "devbox",
            remoteWorkingDirectory: "/home/me/project");
    });
}).AsAIAgent(
    model: "gpt-5.5",
    instructions: "Work in the remote checkout.");

AIAgent dockerAgent = new CodexAgentClient(builder =>
{
    builder.ConfigureAppServer(options =>
    {
        options.Launch = CodexLaunchRemote.DockerAppServer(
            container: "codex-dev",
            workingDirectory: "/workspace",
            codexHome: "/home/codex/.codex");
    });
}).AsAIAgent(
    model: "gpt-5.5",
    instructions: "Work inside the container.");
```

For detached WebSocket app-servers, start or load a managed remote entry with `CodexRemoteAppServerManager`, then attach the agent to that entry:

```csharp
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Agents.Remote;
using JKToolKit.CodexSDK.AppServer.Remote;
using JKToolKit.CodexSDK.AppServer.Remote.Registry;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;

var registry = new JsonFileCodexRemoteAppServerRegistry("codexsdk-appservers.json");
var manager = new CodexRemoteAppServerManager(registry);

var entry = await manager.StartDockerContainerWebSocketAsync(new CodexDockerContainerWebSocketAppServerOptions
{
    Image = "codex-dev",
    WorkingDirectory = "/workspace",
    CodexHome = "/home/codex/.codex",
    AdditionalDockerRunArguments =
    [
        "-v", "/host/project:/workspace",
        "-v", "/host/.codex:/home/codex/.codex"
    ]
});

AIAgent remoteAgent = new CodexAgentClient().AsAIAgent(new CodexAIAgentOptions
{
    Model = "gpt-5.5",
    Cwd = "/workspace",
    ApprovalPolicy = CodexApprovalPolicy.Never,
    Instructions = "Work in the managed remote app-server.",
    RemoteAppServer = new CodexAgentRemoteAppServerOptions
    {
        Manager = manager,
        EntryId = entry.Id
    }
});

Console.WriteLine(await remoteAgent.RunAsync("Run pwd and summarize the result."));

await manager.StopAsync(entry.Id, new CodexRemoteStopOptions { RemoveFromRegistry = true });
```

Each agent run attaches to the registered app-server and disposes only that attachment when the run completes. For SSH entries this closes the local tunnel; for detached Docker and SSH WebSocket entries, the remote app-server keeps running until you stop it through the manager. Dynamic Agent Framework tools still work: the adapter applies the required app-server experimental capability and approval handler to the remote attachment.

Use Agent Framework `ChatClientAgentOptions` when you already configure agents that way:

```csharp
AIAgent agent = sdk.AsAIAgent(
    model: "gpt-5.5",
    options: new ChatClientAgentOptions
    {
        Name = "CodexAgent",
        Description = "Codex with Agent Framework tools.",
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a helpful assistant.",
            Tools = [getWeather]
        },
        ChatHistoryProvider = new InMemoryChatHistoryProvider()
    });
```

Layer Codex-specific defaults on top of Agent Framework options when needed:

```csharp
var options = new ChatClientAgentOptions
{
    Name = "CodexAgent",
    ChatOptions = new ChatOptions
    {
        Instructions = "You are a helpful assistant.",
        Tools = [getWeather]
    }
}.ToCodexAIAgentOptions(model: "gpt-5.5");

options.Cwd = Environment.CurrentDirectory;
options.ApprovalPolicy = CodexApprovalPolicy.Never;

AIAgent agent = sdk.AsAIAgent(options);
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

Register the agent with dependency injection when your host resolves `AIAgent` instances:

```csharp
using JKToolKit.CodexSDK;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;

static string GetWeather(string location) => $"Weather in {location}: cloudy.";

var services = new ServiceCollection();

services.AddCodexSdk(appServer: options =>
{
    options.CodexHomeDirectory = Environment.GetEnvironmentVariable("CODEX_HOME");
});

services.AddKeyedCodexAIAgent("pizza", agent =>
{
    agent.Name = "PizzaAgent";
    agent.Instructions = "Use the pizza tools for menu, cart, and checkout state.";
    agent.Tools = [AIFunctionFactory.Create(GetWeather)];
});

await using var provider = services.BuildServiceProvider();
AIAgent agent = provider.GetRequiredKeyedService<AIAgent>("pizza");
```

DI registration also accepts native Agent Framework options:

```csharp
services.AddCodexAIAgent(
    new ChatClientAgentOptions
    {
        Name = "CodexAgent",
        ChatOptions = new ChatOptions
        {
            Instructions = "Use the configured tools when helpful.",
            Tools = [AIFunctionFactory.Create(GetWeather)]
        }
    },
    model: "gpt-5.5");
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

Compose or host the Codex agent with Agent Framework packages that accept `AIAgent`:

```csharp
// dotnet add package Microsoft.Agents.AI.Workflows --prerelease
using Microsoft.Agents.AI.Workflows;

var workflow = new WorkflowBuilder(codexAgent)
    .AddEdge(codexAgent, reviewerAgent)
    .Build();
```

```csharp
// dotnet add package Microsoft.Agents.AI.Hosting.OpenAI --prerelease
using Microsoft.Agents.AI.Hosting;

app.MapOpenAIChatCompletions(codexAgent);
app.MapOpenAIResponses(codexAgent);
```

```csharp
// dotnet add package Microsoft.Agents.AI.Hosting.A2A.AspNetCore --prerelease
using Microsoft.Agents.AI.Hosting;

app.MapA2A(codexAgent, path: "/a2a/codex");
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
- `CodexSdk.AsAIAgent(...)` adapts an existing SDK facade and leaves its lifetime with the caller. This is the closest match to provider APIs such as `AIProjectClient.AsAIAgent(...)`.
- Workflow and hosting packages are intentionally not referenced by this adapter. Add `Microsoft.Agents.AI.Workflows`, `Microsoft.Agents.AI.Hosting.OpenAI`, `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`, or another Agent Framework host package in the application that composes or exposes the Codex agent.
- `ChatClientAgentOptions` maps its metadata, default `ChatOptions`, `ChatHistoryProvider`, and `AIContextProviders` into the Codex agent. `CodexAIAgent` exposes `ChatOptions`, `Instructions`, `ChatHistoryProvider`, and `AIContextProviders` for the same native inspection style. Chat-client pipeline flags such as `UseProvidedChatClientAsIs` are specific to `IChatClient` agents and are not used by Codex.
- `ChatClientAgentOptions.ToCodexAIAgentOptions(...)` keeps existing Agent Framework configuration reusable when you also need Codex-specific agent defaults.
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
