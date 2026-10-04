using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.SemanticKernel;
using Microsoft.SemanticKernel;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SemanticKernelAdapterEdgeCaseTests
{
    [Fact]
    public async Task StandaloneFunction_UsesItsNameAndDefaultArguments()
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<string?, string>)(value => value ?? "fallback"), "echo");
        var handler = new SemanticKernelToolCallHandler(new Kernel(), [function]);
        var result = await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("echo", new { value = (string?)null }), default);
        Text(result).Should().Be("fallback");
        result.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void PluginDescriptionFallback_AndOptionalParameters_ArePreserved()
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<string?, string>)(value => value ?? "fallback"), "echo", parameters:
            [new KernelParameterMetadata("value") { ParameterType = typeof(string), IsRequired = false }]);
        var plugin = KernelPluginFactory.CreateFromFunctions("Utilities", [function]);
        var tools = SemanticKernelCodexToolAdapter.Create(new Kernel(), [plugin]).DynamicTools;
        tools.Single().Name.Should().Be("Utilities-echo");
        tools.Single().Description.Should().Be("Utilities.echo");
        tools.Single().InputSchema.GetProperty("required").GetArrayLength().Should().Be(0);
    }

    public static IEnumerable<object?[]> SchemaTypes()
    {
        yield return [null, "object"];
        yield return [typeof(object), "object"];
        yield return [typeof(string), "string"];
        yield return [typeof(DayOfWeek), "string"];
        yield return [typeof(bool), "boolean"];
        yield return [typeof(byte), "integer"];
        yield return [typeof(short), "integer"];
        yield return [typeof(int), "integer"];
        yield return [typeof(long), "integer"];
        yield return [typeof(int?), "integer"];
        yield return [typeof(float), "number"];
        yield return [typeof(double), "number"];
        yield return [typeof(decimal), "number"];
    }

    [Theory]
    [MemberData(nameof(SchemaTypes))]
    public void MissingParameterSchema_FallsBackToPortableJsonTypes(Type? type, string expected)
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<object?, object?>)(value => value), "echo", parameters:
            [new KernelParameterMetadata("value") { ParameterType = type, Schema = KernelJsonSchema.Parse("null"), IsRequired = true }]);
        var plugin = KernelPluginFactory.CreateFromFunctions("Utilities", [function]);
        var tool = SemanticKernelCodexToolAdapter.Create(new Kernel(), [plugin]).DynamicTools.Single();
        tool.InputSchema.GetProperty("properties").GetProperty("value").GetProperty("type").GetString().Should().Be(expected);
        tool.InputSchema.GetProperty("required")[0].GetString().Should().Be("value");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public async Task MissingParams_ThrowArgumentException(string? json)
    {
        var handler = new SemanticKernelToolCallHandler(new Kernel(), []);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync("item/tool/call", json is null ? null : JsonSerializer.Deserialize<JsonElement>(json), default).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync("item/tool/call", default(JsonElement), default).AsTask());
    }

    [Fact]
    public void DuplicateFunctions_AreRejected()
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<string>)(() => "ok"), "echo");
        Assert.Throws<ArgumentException>(() => new SemanticKernelToolCallHandler(new Kernel(), [function, function]));
    }

    [Fact]
    public async Task NonToolRequests_DelegateToFallback()
    {
        using var cancellation = new CancellationTokenSource();
        var fallback = new AdapterToolEdgeCaseTests.RecordingFallback();
        var handler = new SemanticKernelToolCallHandler(new Kernel(), [], fallback);
        var request = JsonSerializer.SerializeToElement(42);
        (await handler.HandleAsync("approval", request, cancellation.Token)).GetString().Should().Be("forwarded");
        fallback.Method.Should().Be("approval");
        fallback.Parameters!.Value.GetInt32().Should().Be(42);
        fallback.Cancellation.Should().Be(cancellation.Token);
        await Assert.ThrowsAsync<NotSupportedException>(() => new SemanticKernelToolCallHandler(new Kernel(), []).HandleAsync("approval", request, default).AsTask());
    }

    [Fact]
    public async Task UnknownTools_ReturnFailureWithoutInvokingFunctions()
    {
        var handler = new SemanticKernelToolCallHandler(new Kernel(), []);
        var response = await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("unknown", new { }), default);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        Text(response).Should().Contain("unknown");
    }

    [Fact]
    public async Task NonObjectArguments_UseFunctionDefaults()
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<int, string>)OptionalValue, "optional");
        var handler = new SemanticKernelToolCallHandler(new Kernel(), [function]);
        Text(await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("optional", 17), default)).Should().Be("7");
        Text(await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("optional", new { ignored = "value" }), default)).Should().Be("7");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonArguments_RemainStructuredForObjectAndJsonElementParameters(bool useElement)
    {
        var function = useElement
            ? KernelFunctionFactory.CreateFromMethod((Func<JsonElement, JsonElement>)(value => value), "echo")
            : KernelFunctionFactory.CreateFromMethod((Func<object, object>)(value => value), "echo");
        var handler = new SemanticKernelToolCallHandler(new Kernel(), [function]);
        var response = await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("echo", new { value = new { nested = true } }), default);
        response.GetProperty("success").GetBoolean().Should().BeTrue();
        JsonSerializer.Deserialize<JsonElement>(Text(response)).GetProperty("nested").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UntypedMetadata_ClonesJsonArguments()
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<object, object>)(value => value), "echo", parameters: [new KernelParameterMetadata("value") { ParameterType = null }]);
        var handler = new SemanticKernelToolCallHandler(new Kernel(), [function]);
        var response = await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("echo", new { value = 12 }), default);
        Text(response).Should().Be("12");
    }

    [Fact]
    public async Task InvocationErrors_AreToolFailures_AndCancellationPropagates()
    {
        var failure = KernelFunctionFactory.CreateFromMethod((Func<string>)(() => throw new InvalidOperationException("unavailable")), "failure");
        using var cancellation = new CancellationTokenSource();
        var cancelled = KernelFunctionFactory.CreateFromMethod((Func<string>)(() => throw new OperationCanceledException(cancellation.Token)), "cancelled");
        var handler = new SemanticKernelToolCallHandler(new Kernel(), [failure, cancelled]);
        var response = await handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("failure", new { }), default);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        Text(response).Should().Be("unavailable");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("cancelled", new { }), cancellation.Token).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullAndObjectResults_AreFormattedAsText(bool nullResult)
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<object?>)(() => nullResult ? null : new { count = 2 }), "result");
        var response = await new SemanticKernelToolCallHandler(new Kernel(), [function]).HandleAsync("item/tool/call", AdapterToolEdgeCaseTests.Request("result", new { }), default);
        response.GetProperty("success").GetBoolean().Should().BeTrue();
        Text(response).Should().Be(nullResult ? "" : "{\"count\":2}");
    }

    [Fact]
    public void UntypedParameterWithoutSchema_UsesObjectFallback()
    {
        var function = KernelFunctionFactory.CreateFromMethod((Func<object, object>)(value => value), "echo", parameters: [new KernelParameterMetadata("value")]);
        var plugin = KernelPluginFactory.CreateFromFunctions("Utilities", [function]);
        var tool = SemanticKernelCodexToolAdapter.Create(new Kernel(), [plugin]).DynamicTools.Single();
        tool.InputSchema.GetProperty("properties").GetProperty("value").GetProperty("type").GetString().Should().Be("object");
    }

    private static string OptionalValue(int value = 7) => value.ToString();
    private static string Text(JsonElement response) => response.GetProperty("contentItems")[0].GetProperty("text").GetString()!;
}
