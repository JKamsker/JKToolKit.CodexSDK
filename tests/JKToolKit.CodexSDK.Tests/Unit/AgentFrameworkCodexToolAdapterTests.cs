using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
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
}
