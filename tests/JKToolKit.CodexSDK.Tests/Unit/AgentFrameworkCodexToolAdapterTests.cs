using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework;
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
}
