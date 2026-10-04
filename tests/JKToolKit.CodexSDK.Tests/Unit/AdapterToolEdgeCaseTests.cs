using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AgentFramework.Agents;
using JKToolKit.CodexSDK.AgentFramework.Tools;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using Microsoft.Extensions.AI;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AdapterToolEdgeCaseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")] 
    public async Task InvalidRequests_AreStructuredNonRetryableFailures(string? json)
    {
        var handler = new AgentFrameworkToolCallHandler([]);
        var result = await handler.HandleAsync("item/tool/call", json is null ? null : JsonSerializer.Deserialize<JsonElement>(json), default);
        result.GetProperty("success").GetBoolean().Should().BeFalse();
        var error = Error(result);
        error.GetProperty("error_code").GetString().Should().Be("invalid_tool_request");
        error.GetProperty("is_retryable").GetBoolean().Should().BeFalse();
        error.TryGetProperty("diagnostics", out _).Should().BeFalse();
        error.TryGetProperty("tool_name", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UnknownTools_ReportCallIdentity()
    {
        var handler = new AgentFrameworkToolCallHandler([]);
        var error = Error(await handler.HandleAsync("item/tool/call", Request("missing", new { }), default));
        error.GetProperty("error_code").GetString().Should().Be("unknown_tool");
        error.GetProperty("tool_name").GetString().Should().Be("missing");
        error.GetProperty("call_id").GetString().Should().Be("call-1");
    }

    [Fact]
    public async Task NonToolRequests_AreForwardedWithCancellationAndParams()
    {
        using var cancellation = new CancellationTokenSource();
        var fallback = new RecordingFallback();
        var handler = new AgentFrameworkToolCallHandler([], fallback);
        var parameters = JsonSerializer.SerializeToElement(new { command = "status" });
        (await handler.HandleAsync("approval", parameters, cancellation.Token)).GetString().Should().Be("forwarded");
        fallback.Method.Should().Be("approval");
        fallback.Parameters!.Value.GetProperty("command").GetString().Should().Be("status");
        fallback.Cancellation.Should().Be(cancellation.Token);
        await Assert.ThrowsAsync<NotSupportedException>(() => new AgentFrameworkToolCallHandler([]).HandleAsync("approval", null, default).AsTask());
    }

    [Fact]
    public void Create_RejectsDuplicateNames_AndPreservesExplicitSchema()
    {
        var function = new StubFunction(() => null, JsonSerializer.SerializeToElement(new { type = "object", additionalProperties = false }));
        Assert.Throws<ArgumentException>(() => AgentFrameworkCodexToolAdapter.Create([function, function]));
        var spec = AgentFrameworkCodexToolAdapter.Create([function]).DynamicTools.Single();
        spec.Description.Should().Be("stub");
        spec.InputSchema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        AgentFrameworkCodexToolAdapter.Create([]).ToolSchemaHash.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSchema_ProducesValidObjectSchema(bool useJsonNull)
    {
        var function = new StubFunction(() => null, useJsonNull ? JsonSerializer.SerializeToElement<object?>(null) : default);
        var schema = AgentFrameworkCodexToolAdapter.Create([function]).DynamicTools.Single().InputSchema;
        schema.GetProperty("type").GetString().Should().Be("object");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("denied by host")]
    public async Task ApprovalRejection_PreventsInvocation_AndPreservesReason(string? reason)
    {
        var invoked = false;
        var function = new StubFunction(() => { invoked = true; return "done"; });
        using var cancellation = new CancellationTokenSource();
        var set = AgentFrameworkCodexToolAdapter.Create([function], new AgentFrameworkCodexToolAdapterOptions
        {
            SafetyOptions = new() { RequireApproval = (tool, args) => tool.Name == "stub" && args.GetProperty("approve").GetBoolean() },
            ToolApprovalHandler = (request, token) =>
            {
                request.ThreadId.Should().Be("thread-1");
                request.TurnId.Should().Be("turn-1");
                request.CallId.Should().Be("call-1");
                request.Function.Should().BeSameAs(function);
                request.Arguments.GetProperty("approve").GetBoolean().Should().BeTrue();
                token.Should().Be(cancellation.Token);
                return ValueTask.FromResult(AgentFrameworkToolApprovalResponse.Reject(reason));
            }
        });
        var error = Error(await set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { approve = true }), cancellation.Token));
        invoked.Should().BeFalse();
        error.GetProperty("error_code").GetString().Should().Be("approval_denied");
        error.GetProperty("error_message").GetString().Should().Be(string.IsNullOrWhiteSpace(reason) ? "Agent Framework tool 'stub' was not approved." : reason);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public async Task AllowAndDenyPolicies_ApplyBeforeInvocation(bool allowTool, bool denyTool, bool expected)
    {
        var invoked = false;
        var function = new StubFunction(() => { invoked = true; return "ok"; });
        var set = AgentFrameworkCodexToolAdapter.Create([function], new AgentFrameworkCodexToolAdapterOptions
        {
            SafetyOptions = new()
            {
                AllowedToolNames = allowTool ? new HashSet<string> { "stub" } : new HashSet<string>(),
                DeniedToolNames = denyTool ? new HashSet<string> { "stub" } : new HashSet<string>(),
                RequireApproval = (_, _) => false
            }
        });
        var response = await set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", 123), default);
        response.GetProperty("success").GetBoolean().Should().Be(expected);
        invoked.Should().Be(expected);
    }

    [Fact]
    public async Task NonEmptyAllowList_RejectsUnlistedFunction()
    {
        var set = AgentFrameworkCodexToolAdapter.Create([new StubFunction(() => throw new Exception("must not run"))], new AgentFrameworkCodexToolAdapterOptions
        { SafetyOptions = new() { AllowedToolNames = new HashSet<string> { "other" } } });
        Error(await set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { }), default))
            .GetProperty("error_code").GetString().Should().Be("tool_denied_by_policy");
    }

    [Fact]
    public async Task InvocationCancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var set = AgentFrameworkCodexToolAdapter.Create([new StubFunction(() => throw new OperationCanceledException(cancellation.Token))]);
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { }), cancellation.Token).AsTask());
        exception.CancellationToken.Should().Be(cancellation.Token);
    }

    public static IEnumerable<object?[]> Results()
    {
        yield return [null, ""];
        yield return [Array.Empty<AIContent>(), ""];
        yield return ["hello", "hello"];
        yield return [JsonSerializer.SerializeToElement("json text"), "json text"];
        yield return [JsonSerializer.SerializeToElement(new { count = 2 }), "{\"count\":2}"];
        yield return [new { count = 3 }, "{\"count\":3}"];
        yield return [new TextContent("content"), "content"];
        yield return [new FunctionResultContent("call", "nested"), "nested"];
        yield return [new AIContent[] { new FunctionResultContent("call", "nested list") }, "nested list"];
    }

    [Theory]
    [MemberData(nameof(Results))]
    public async Task Results_AreMappedIntoToolText(object? value, string expected)
    {
        var set = AgentFrameworkCodexToolAdapter.Create([new StubFunction(() => value)]);
        var response = await set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { }), default);
        response.GetProperty("success").GetBoolean().Should().BeTrue();
        response.GetProperty("contentItems").GetArrayLength().Should().Be(1);
        var text = response.GetProperty("contentItems")[0].GetProperty("text").GetString()!;
        if (expected.StartsWith("{"))
            JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(text), JsonSerializer.Deserialize<JsonElement>(expected)).Should().BeTrue();
        else
            text.Should().Be(expected);
    }

    [Fact]
    public async Task Images_KeepTheirMediaPayload_AndOtherContentRetainsStructuredData()
    {
        AIContent[] contents = [new DataContent(new byte[] { 1, 2 }, "image/png"), new UriContent("https://example.org/img", "image/jpeg"), new DataContent(new byte[] { 3 }, "audio/wav"), new UriContent("https://example.org/doc", "application/pdf")];
        var set = AgentFrameworkCodexToolAdapter.Create([new StubFunction(() => contents)]);
        var response = await set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { }), default);
        response.GetProperty("success").GetBoolean().Should().BeTrue();
        var items = response.GetProperty("contentItems");
        items.GetArrayLength().Should().Be(4);
        items[0].GetProperty("imageUrl").GetString().Should().Be("data:image/png;base64,AQI=");
        items[1].GetProperty("imageUrl").GetString().Should().Be("https://example.org/img");
        items[2].GetProperty("text").GetString().Should().Contain("audio/wav");
        items[3].GetProperty("text").GetString().Should().Contain("https://example.org/doc");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResultExceptions_AreFailuresEvenWhenWrappedInAContentList(bool list)
    {
        var failed = new FunctionResultContent("call", null) { Exception = new InvalidOperationException("secret") };
        var set = AgentFrameworkCodexToolAdapter.Create([new StubFunction(() => list ? new AIContent[] { failed } : failed)]);
        var response = await set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { }), default);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        Error(response).GetProperty("error_code").GetString().Should().Be("tool_invocation_failed");
        response.ToString().Should().NotContain("secret");
    }

    [Fact]
    public async Task ApprovalCancellation_PropagatesWithoutInvokingTool()
    {
        using var cancellation = new CancellationTokenSource();
        var invoked = false;
        var set = AgentFrameworkCodexToolAdapter.Create([new StubFunction(() => { invoked = true; return "done"; })], new AgentFrameworkCodexToolAdapterOptions
        {
            SafetyOptions = new() { RequireApprovalForAllAgentFrameworkTools = true },
            ToolApprovalHandler = (_, _) => throw new OperationCanceledException(cancellation.Token)
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => set.ApprovalHandler.HandleAsync("item/tool/call", Request("stub", new { }), cancellation.Token).AsTask());
        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task InvocationArguments_ContainClonedJsonAndCodexIdentity()
    {
        var function = new CapturingFunction();
        var set = AgentFrameworkCodexToolAdapter.Create([function]);
        var result = await set.ApprovalHandler.HandleAsync("item/tool/call", Request("capture", new { item = new { number = 7 } }), default);
        result.GetProperty("success").GetBoolean().Should().BeTrue();
        function.Arguments!["item"].Should().BeOfType<JsonElement>().Which.GetProperty("number").GetInt32().Should().Be(7);
        function.Arguments.Context.Should().Contain("codex.threadId", "thread-1").And.Contain("codex.turnId", "turn-1").And.Contain("codex.callId", "call-1").And.Contain("codex.toolName", "capture");
        function.Call!.CallId.Should().Be("call-1");
        function.Call.Name.Should().Be("capture");
        function.Call.Arguments!["item"].Should().BeOfType<JsonElement>().Which.GetProperty("number").GetInt32().Should().Be(7);
    }

    private sealed class CapturingFunction : AIFunction
    {
        public override string Name => "capture";
        public AIFunctionArguments? Arguments { get; private set; }
        public FunctionCallContent? Call { get; private set; }
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            Call = FunctionInvokingChatClient.CurrentContext!.CallContent;
            return ValueTask.FromResult<object?>("ok");
        }
    }

    internal static JsonElement Request(string name, object? args) => JsonSerializer.SerializeToElement(new DynamicToolCallParams
    { ThreadId = "thread-1", TurnId = "turn-1", CallId = "call-1", Tool = name, Arguments = JsonSerializer.SerializeToElement(args) });
    private static JsonElement Error(JsonElement response) => JsonSerializer.Deserialize<JsonElement>(response.GetProperty("contentItems")[0].GetProperty("text").GetString()!);

    private sealed class StubFunction(Func<object?> invoke, JsonElement schema = default) : AIFunction
    {
        public override string Name => "stub";
        public override JsonElement JsonSchema => schema;
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) => ValueTask.FromResult(invoke());
    }

    internal sealed class RecordingFallback : IAppServerApprovalHandler
    {
        public string? Method { get; private set; }
        public JsonElement? Parameters { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct)
        { Method = method; Parameters = @params; Cancellation = ct; return ValueTask.FromResult(JsonSerializer.SerializeToElement("forwarded")); }
    }
}
