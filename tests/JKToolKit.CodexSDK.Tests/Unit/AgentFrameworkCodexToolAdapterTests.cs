using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AgentFrameworkCodexToolAdapterTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void Create_ConvertsAIFunctionsToDynamicTools()
    {
        var function = AIFunctionFactory.Create(
            (Func<PizzaSize, IReadOnlyList<PizzaTopping>, int, string>)AddPizzaToCart,
            "add_pizza_to_cart",
            "Add a pizza to the user's cart.",
            SerializerOptions);

        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);

        var tool = toolSet.DynamicTools.Should().ContainSingle().Subject;
        tool.Name.Should().Be("add_pizza_to_cart");
        tool.Description.Should().Be("Add a pizza to the user's cart.");

        var schema = tool.InputSchema;
        schema.GetProperty("type").GetString().Should().Be("object");
        schema.GetProperty("properties").GetProperty("size").GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo(["Small", "Medium", "Large"]);

        schema.GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo(["size", "toppings"]);
    }

    [Fact]
    public async Task ApprovalHandler_InvokesAIFunctionWithTypedArguments()
    {
        var function = AIFunctionFactory.Create(
            (Func<PizzaSize, IReadOnlyList<PizzaTopping>, int, string>)AddPizzaToCart,
            "add_pizza_to_cart",
            "Add a pizza to the user's cart.",
            SerializerOptions);
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = JsonSerializer.SerializeToElement(new DynamicToolCallParams
        {
            ThreadId = "thread-1",
            TurnId = "turn-1",
            CallId = "call-1",
            Tool = "add_pizza_to_cart",
            Arguments = JsonSerializer.SerializeToElement(new
            {
                size = "Large",
                toppings = new[] { "Pepperoni", "Mushrooms" },
                quantity = 2
            })
        });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("contentItems")[0]
            .GetProperty("text")
            .GetString()
            .Should()
            .Be("Large:Pepperoni,Mushrooms:2");
    }

    [Fact]
    public async Task ApprovalHandler_RejectsApprovalRequiredFunctionWithoutHostApproval()
    {
        var invoked = false;
        var function = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(
            (Func<string, string>)(location =>
            {
                invoked = true;
                return location;
            }),
            "get_weather"));
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = CreateToolCall("get_weather", new { location = "Amsterdam" });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("contentItems")[0].GetProperty("text").GetString()
            .Should()
            .Contain("not approved");
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task ApprovalHandler_InvokesApprovalRequiredFunctionWhenHostApproves()
    {
        var function = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(
            (Func<string, string>)(location => $"Weather in {location}: cloudy."),
            "get_weather"));
        var toolSet = AgentFrameworkCodexToolAdapter.Create(
            [function],
            new AgentFrameworkCodexToolAdapterOptions
            {
                ToolApprovalHandler = (request, _) =>
                {
                    request.ToolName.Should().Be("get_weather");
                    return ValueTask.FromResult(AgentFrameworkToolApprovalResponse.Approve());
                }
            });
        var request = CreateToolCall("get_weather", new { location = "Amsterdam" });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("contentItems")[0].GetProperty("text").GetString()
            .Should()
            .Be("Weather in Amsterdam: cloudy.");
    }

    [Fact]
    public async Task ApprovalHandler_PassesInvocationServicesToAIFunction()
    {
        var function = AIFunctionFactory.Create(
            (Func<IServiceProvider, string>)(services => services.GetService(typeof(ServiceValue)) is ServiceValue value ? value.Text : "missing"),
            "read_service");
        var toolSet = AgentFrameworkCodexToolAdapter.Create(
            [function],
            new AgentFrameworkCodexToolAdapterOptions
            {
                FunctionInvocationServices = new StaticServiceProvider(new ServiceValue("injected"))
            });
        var request = CreateToolCall("read_service", new { });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("contentItems")[0].GetProperty("text").GetString()
            .Should()
            .Be("injected");
    }

    [Fact]
    public async Task ApprovalHandler_ExposesFunctionInvocationContext()
    {
        var function = AIFunctionFactory.Create(
            (Func<string>)(() => FunctionInvokingChatClient.CurrentContext?.CallContent.CallId ?? "missing"),
            "read_context");
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = CreateToolCall("read_context", new { });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("contentItems")[0].GetProperty("text").GetString()
            .Should()
            .Be("call-1");
    }

    [Fact]
    public async Task CodexAgentSession_SerializesThreadIdAndStateBag()
    {
        var agent = new CodexAgentClient().AsAIAgent(name: "NativeCodex");
        var session = (CodexAgentSession)await agent.CreateSessionAsync();
        session.ThreadId = "thread-123";
        session.StateBag.SetValue("memory", new SessionMemory("Alice"), SerializerOptions);

        var serialized = await agent.SerializeSessionAsync(session, SerializerOptions);
        var resumed = (CodexAgentSession)await agent.DeserializeSessionAsync(serialized, SerializerOptions);

        resumed.ThreadId.Should().Be("thread-123");
        var memory = resumed.StateBag.GetValue<SessionMemory>("memory", SerializerOptions);
        memory.Should().NotBeNull();
        memory!.Name.Should().Be("Alice");
    }

    [Fact]
    public void CodexAgentClient_AsAIAgent_CreatesNativeAgentWithMetadata()
    {
        var agent = new CodexAgentClient().AsAIAgent(
            model: "gpt-5.5",
            instructions: "You are a helpful assistant.",
            name: "CodexNative",
            description: "Runs Codex as an Agent Framework agent.",
            tools: [AIFunctionFactory.Create((Func<string, string>)(location => location))]);

        agent.Should().BeAssignableTo<AIAgent>();
        agent.Name.Should().Be("CodexNative");
        agent.Description.Should().Be("Runs Codex as an Agent Framework agent.");
        var metadata = agent.GetService(typeof(AIAgentMetadata), serviceKey: null);
        metadata.Should().BeOfType<AIAgentMetadata>().Which.ProviderName.Should().Be("codex");
    }

    [Fact]
    public void CodexAgentClient_AsAIAgent_AdvertisesFunctionInvocationMiddlewareSupport()
    {
        var agent = new CodexAgentClient().AsAIAgent(name: "NativeCodex");
        Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>, CancellationToken, ValueTask<object?>> middleware =
            static (_, context, next, cancellationToken) => next(context, cancellationToken);

        var decorated = agent.AsBuilder().Use(middleware).Build();

        agent.GetService(typeof(FunctionInvokingChatClient)).Should().BeOfType<FunctionInvokingChatClient>();
        decorated.Should().BeAssignableTo<AIAgent>();
    }

    [Fact]
    public void ChatClientAgentRunOptions_CanCarryCodexConfiguration()
    {
        var options = new ChatClientAgentRunOptions(new ChatOptions())
            .ConfigureCodex(codex =>
            {
                codex.Cwd = "repo";
                codex.Effort = CodexReasoningEffort.High;
            });

        var configuration = options.GetCodexConfiguration();

        configuration.Should().NotBeNull();
        configuration!.Cwd.Should().Be("repo");
        configuration.Effort.Should().Be(CodexReasoningEffort.High);

        var clone = (ChatClientAgentRunOptions)options.Clone();
        clone.GetCodexConfiguration().Should().BeSameAs(configuration);
    }

    [Fact]
    public void CodexAgentRunOptions_Clone_PreservesCodexProperties()
    {
        var options = new CodexAgentRunOptions
        {
            Model = "gpt-5.5",
            Cwd = "repo",
            ApprovalPolicy = CodexApprovalPolicy.Never,
            Sandbox = CodexSandboxMode.ReadOnly,
            Effort = CodexReasoningEffort.High,
            Summary = "auto",
            Tools = [AIFunctionFactory.Create((Func<string>)(() => "ok"), "read_value")]
        };

        var clone = (CodexAgentRunOptions)options.Clone();

        clone.Model.Should().Be(options.Model);
        clone.Cwd.Should().Be(options.Cwd);
        clone.ApprovalPolicy.Should().Be(options.ApprovalPolicy);
        clone.Sandbox.Should().Be(options.Sandbox);
        clone.Effort.Should().Be(options.Effort);
        clone.Summary.Should().Be(options.Summary);
        clone.Tools.Should().NotBeSameAs(options.Tools);
        clone.Tools.Should().BeEquivalentTo(options.Tools);
    }

    private static JsonElement CreateToolCall(string toolName, object arguments)
    {
        return JsonSerializer.SerializeToElement(new DynamicToolCallParams
        {
            ThreadId = "thread-1",
            TurnId = "turn-1",
            CallId = "call-1",
            Tool = toolName,
            Arguments = JsonSerializer.SerializeToElement(arguments)
        });
    }

    [Description("Add a pizza to the user's cart.")]
    private static string AddPizzaToCart(
        [Description("Pizza size.")] PizzaSize size,
        [Description("Pizza toppings.")] IReadOnlyList<PizzaTopping> toppings,
        int quantity = 1)
    {
        return $"{size}:{string.Join(",", toppings)}:{quantity}";
    }

    private enum PizzaSize
    {
        Small,
        Medium,
        Large
    }

    private enum PizzaTopping
    {
        Cheese,
        Pepperoni,
        Mushrooms
    }

    private sealed record SessionMemory(string Name);

    private sealed record ServiceValue(string Text);

    private sealed class StaticServiceProvider(ServiceValue value) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(ServiceValue) ? value : null;
        }
    }
}
