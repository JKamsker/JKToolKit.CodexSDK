using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
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
        toolSet.ToolSchemaHash.Should().NotBeNullOrWhiteSpace();

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
    public async Task ApprovalHandler_RequiresApprovalForAllToolsWhenConfigured()
    {
        var invoked = false;
        var function = AIFunctionFactory.Create(
            (Func<string, string>)(location =>
            {
                invoked = true;
                return location;
            }),
            "get_weather");
        var toolSet = AgentFrameworkCodexToolAdapter.Create(
            [function],
            new AgentFrameworkCodexToolAdapterOptions
            {
                SafetyOptions = new CodexAgentSafetyOptions
                {
                    RequireApprovalForAllAgentFrameworkTools = true
                }
            });
        var request = CreateToolCall("get_weather", new { location = "Amsterdam" });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeFalse();
        var error = ReadError(response);
        error.GetProperty("error_code").GetString().Should().Be("approval_required");
        error.GetProperty("tool_name").GetString().Should().Be("get_weather");
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task ApprovalHandler_DeniesToolBySafetyPolicy()
    {
        var invoked = false;
        var function = AIFunctionFactory.Create(
            (Func<string>)(() =>
            {
                invoked = true;
                return "ok";
            }),
            "delete_file");
        var toolSet = AgentFrameworkCodexToolAdapter.Create(
            [function],
            new AgentFrameworkCodexToolAdapterOptions
            {
                SafetyOptions = new CodexAgentSafetyOptions
                {
                    DeniedToolNames = new HashSet<string>(StringComparer.Ordinal) { "delete_file" }
                }
            });
        var request = CreateToolCall("delete_file", new { });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeFalse();
        ReadError(response).GetProperty("error_code").GetString().Should().Be("tool_denied_by_policy");
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task ApprovalHandler_RedactsToolExceptionsByDefault()
    {
        var function = AIFunctionFactory.Create(
            (Func<string>)(() => throw new InvalidOperationException("secret path")),
            "fail_tool");
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = CreateToolCall("fail_tool", new { });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeFalse();
        var error = ReadError(response);
        error.GetProperty("error_code").GetString().Should().Be("tool_invocation_failed");
        error.GetProperty("error_message").GetString().Should().NotContain("secret path");
        error.TryGetProperty("diagnostics", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ApprovalHandler_IncludesToolExceptionDiagnosticsWhenOptedIn()
    {
        var function = AIFunctionFactory.Create(
            (Func<string>)(() => throw new InvalidOperationException("visible failure")),
            "fail_tool");
        var toolSet = AgentFrameworkCodexToolAdapter.Create(
            [function],
            new AgentFrameworkCodexToolAdapterOptions
            {
                SafetyOptions = new CodexAgentSafetyOptions
                {
                    RedactToolExceptionDetails = false
                }
            });
        var request = CreateToolCall("fail_tool", new { });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        var error = ReadError(response);
        error.GetProperty("error_message").GetString().Should().Contain("visible failure");
        error.GetProperty("diagnostics").GetProperty("exception_message").GetString()
            .Should().Contain("visible failure");
    }

    [Fact]
    public async Task ApprovalHandler_MapsAIContentResultItems()
    {
        var function = AIFunctionFactory.Create(
            (Func<AIContent[]>)(() =>
            [
                new TextContent("hello"),
                new UriContent("https://example.com/image.png", "image/png")
            ]),
            "get_content");
        var toolSet = AgentFrameworkCodexToolAdapter.Create([function]);
        var request = CreateToolCall("get_content", new { });

        var response = await toolSet.ApprovalHandler.HandleAsync("item/tool/call", request, ct: default);

        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("contentItems")[0].GetProperty("type").GetString().Should().Be("inputText");
        response.GetProperty("contentItems")[0].GetProperty("text").GetString().Should().Be("hello");
        response.GetProperty("contentItems")[1].GetProperty("type").GetString().Should().Be("inputImage");
        response.GetProperty("contentItems")[1].GetProperty("imageUrl").GetString()
            .Should().Be("https://example.com/image.png");
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

    private static JsonElement ReadError(JsonElement response)
    {
        using var document = JsonDocument.Parse(response.GetProperty("contentItems")[0].GetProperty("text").GetString()!);
        return document.RootElement.Clone();
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
