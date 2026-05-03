using System.ComponentModel;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.SemanticKernel;
using Microsoft.SemanticKernel;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SemanticKernelCodexToolAdapterTests
{
    [Fact]
    public void Create_ConvertsKernelFunctionsToDynamicTools()
    {
        var kernel = CreateKernel(new TestPizzaPlugin());

        var toolSet = SemanticKernelCodexToolAdapter.Create(kernel);

        var tool = toolSet.DynamicTools.Should().ContainSingle().Subject;
        tool.Name.Should().Be("OrderPizza-add_pizza_to_cart");
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
    public async Task ApprovalHandler_InvokesKernelFunctionWithTypedArguments()
    {
        var kernel = CreateKernel(new TestPizzaPlugin());
        var toolSet = SemanticKernelCodexToolAdapter.Create(kernel);
        var request = JsonSerializer.SerializeToElement(new DynamicToolCallParams
        {
            ThreadId = "thread-1",
            TurnId = "turn-1",
            CallId = "call-1",
            Tool = "OrderPizza-add_pizza_to_cart",
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

    private static Kernel CreateKernel(object plugin)
    {
        var builder = Kernel.CreateBuilder();
        builder.Plugins.AddFromObject(plugin, "OrderPizza");
        return builder.Build();
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

    private sealed class TestPizzaPlugin
    {
        [KernelFunction("add_pizza_to_cart")]
        [Description("Add a pizza to the user's cart.")]
        public string AddPizzaToCart(
            [Description("Pizza size.")] PizzaSize size,
            [Description("Pizza toppings.")] IReadOnlyList<PizzaTopping> toppings,
            int quantity = 1)
        {
            return $"{size}:{string.Join(",", toppings)}:{quantity}";
        }
    }
}
