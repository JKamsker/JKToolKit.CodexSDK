using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
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

    private sealed record ServiceValue(string Text);

    private sealed class StaticServiceProvider(ServiceValue value) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return serviceType == typeof(ServiceValue) ? value : null;
        }
    }
}
