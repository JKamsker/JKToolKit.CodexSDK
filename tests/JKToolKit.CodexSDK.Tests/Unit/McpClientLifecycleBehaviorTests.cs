using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpClientLifecycleBehaviorTests
{
    [Fact]
    public async Task Dispose_WhenRpcFails_StillDisposesProcess_AndIsIdempotent()
    {
        var process = new ProcessStub();
        var failure = new IOException("rpc cleanup failed");
        var rpc = new RpcStub { DisposeFailure = failure };
        var client = Create(rpc, process);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(async () => await client.DisposeAsync()));
        Assert.Equal(1, process.DisposeCount);
        await client.DisposeAsync();
        Assert.Equal(1, rpc.DisposeCount);
        Assert.Equal(1, process.DisposeCount);
    }

    [Fact]
    public async Task HandshakeFailure_PreservesOriginalException_WhenCleanupAlsoFails()
    {
        var process = new ProcessStub();
        var original = new InvalidOperationException("handshake failed");
        var rpc = new RpcStub { DisposeFailure = new IOException("cleanup failed"), Request = (_, _) => throw original };
        Assert.Same(original, await Assert.ThrowsAsync<InvalidOperationException>(() => CodexMcpServerClient.CreateInitializedAsync(
            new(), process, rpc, NullLogger<CodexMcpServerClient>.Instance, default)));
        Assert.Equal(1, rpc.DisposeCount);
        Assert.Equal(1, process.DisposeCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SchemaPaginationCap_IsEnforced_AndNeverMakesMoreThan100Requests(bool strict)
    {
        var rpc = new RpcStub
        {
            Request = (method, _) => JsonSerializer.SerializeToElement(method == "tools/list"
                ? (object)new { tools = new[] { new { name = "tool", inputSchema = new { additionalProperties = false, properties = new { keep = new { } } } } }, nextCursor = "another" }
                : new { content = new object[0] })
        };
        await using var client = Create(rpc, options: new() { StrictParsing = strict });
        if (strict)
            await Assert.ThrowsAsync<JsonException>(() => client.CallToolAsync("tool", new Dictionary<string, object?> { ["drop"] = true }));
        else
        {
            await client.CallToolAsync("tool", new Dictionary<string, object?> { ["keep"] = 1, ["drop"] = true });
            Assert.Single(rpc.Calls.Last().Args!.Value.GetProperty("arguments").EnumerateObject());
            Assert.Equal(1, rpc.Calls.Last().Args!.Value.GetProperty("arguments").GetProperty("keep").GetInt32());
            await client.CallToolAsync("tool", new Dictionary<string, object?> { ["keep"] = 2 });
        }
        Assert.Equal(100, rpc.Calls.Count(c => c.Method == "tools/list"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidSchemaList_StrictThrows_LenientPreservesArguments(bool strict)
    {
        var rpc = new RpcStub { Request = (_, _) => JsonSerializer.SerializeToElement(new { invalid = new string('x', 4100) }) };
        await using var client = Create(rpc, options: new() { StrictParsing = strict });
        if (strict) await Assert.ThrowsAsync<JsonException>(() => client.CallToolAsync("tool", new Dictionary<string, object?> { ["custom"] = 4 }));
        else
        {
            await client.CallToolAsync("tool", new Dictionary<string, object?> { ["custom"] = 4 });
            Assert.Equal(4, rpc.Calls.Last().Args!.Value.GetProperty("arguments").GetProperty("custom").GetInt32());
        }
    }

    [Fact]
    public async Task UnknownToolAndNonObjectSchema_PreserveArguments_AndReuseSchemaCache()
    {
        var rpc = new RpcStub { Request = (_, _) => JsonSerializer.SerializeToElement(new { tools = new object[] { new { name = "bad", inputSchema = true }, new { name = "absent" } } }) };
        await using var client = Create(rpc);
        foreach (var tool in new[] { "bad", "absent", "unknown", "unknown" })
        {
            await client.CallToolAsync(tool, new Dictionary<string, object?> { ["custom"] = 4 });
            Assert.Equal(4, rpc.Calls.Last().Args!.Value.GetProperty("arguments").GetProperty("custom").GetInt32());
        }
        Assert.Single(rpc.Calls, c => c.Method == "tools/list");
    }

    [Fact]
    public async Task InvalidArguments_AreRejectedBeforeRpc()
    {
        var rpc = new RpcStub();
        await using var client = Create(rpc);
        await Assert.ThrowsAsync<ArgumentException>(() => client.CallToolAsync(" ", new { }));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.CallToolAsync("tool", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.StartSessionAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => client.StartSessionAsync(new() { Prompt = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReplyAsync(" ", "prompt"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReplyAsync("thread", " "));
        Assert.Empty(rpc.Calls);
    }

    [Fact]
    public async Task SuccessfulHandshake_NotifiesInitialized_AndArbitraryCallPreservesPayload()
    {
        var rpc = new RpcStub();
        var process = new ProcessStub();
        await using var client = await CodexMcpServerClient.CreateInitializedAsync(new(), process, rpc, NullLogger<CodexMcpServerClient>.Instance, default);
        Assert.Equal("initialize", rpc.Calls.Single().Method);
        Assert.Equal("2025-11-25", rpc.Calls[0].Args!.Value.GetProperty("protocolVersion").GetString());
        Assert.Empty(rpc.Calls[0].Args!.Value.GetProperty("capabilities").EnumerateObject());
        Assert.Equal(["notifications/initialized"], rpc.Notifications);
        var result = await client.CallAsync("custom", new { value = 3 });
        Assert.Equal("{}", result.GetRawText());
        Assert.Equal(3, rpc.Calls.Last().Args!.Value.GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ElicitationHandler_ReceivesServerRequest_AndFailuresBecomeServerErrors(bool fails)
    {
        var handler = new HandlerStub(fails);
        var rpc = new RpcStub();
        await using var client = Create(rpc, options: new() { ElicitationHandler = handler });
        await client.InitializeAsync(default);
        Assert.True(rpc.Calls[0].Args!.Value.GetProperty("capabilities").GetProperty("elicitation").TryGetProperty("form", out _));
        var request = new JsonRpcRequest(JsonRpcId.FromNumber(42), "elicitation/create", JsonSerializer.SerializeToElement(new { text = "approve" }));
        var response = await rpc.OnServerRequest!(request);
        Assert.Equal(request.Id, response.Id);
        Assert.Equal("elicitation/create", handler.Method);
        Assert.Equal("approve", handler.Params!.Value.GetProperty("text").GetString());
        if (fails)
        {
            Assert.Null(response.Result);
            Assert.Equal(-32000, response.Error!.Code);
            Assert.Equal("handler failed", response.Error.Message);
        }
        else
        {
            Assert.Null(response.Error);
            Assert.Equal("accepted", response.Result!.Value.GetProperty("action").GetString());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListTools_PaginationCap_RejectsOrReturnsBoundedPartialList(bool strict)
    {
        var rpc = new RpcStub { Request = (_, _) => JsonSerializer.SerializeToElement(new { tools = new[] { new { name = "tool" } }, nextCursor = "next" }) };
        await using var client = Create(rpc, options: new() { StrictParsing = strict });
        if (strict) await Assert.ThrowsAsync<JsonException>(() => client.ListToolsAsync());
        else Assert.Equal(100, (await client.ListToolsAsync()).Count);
        Assert.Equal(100, rpc.Calls.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ListTools_InvalidSecondPage_RejectsOrPreservesFirstPage(bool strict)
    {
        var count = 0;
        var rpc = new RpcStub { Request = (_, _) => ++count == 1
            ? JsonSerializer.SerializeToElement(new { tools = new[] { new { name = "first" } }, nextCursor = "next" })
            : JsonSerializer.SerializeToElement(new { malformed = true }) };
        await using var client = Create(rpc, options: new() { StrictParsing = strict });
        if (strict) await Assert.ThrowsAsync<JsonException>(() => client.ListToolsAsync());
        else Assert.Equal("first", Assert.Single(await client.ListToolsAsync()).Name);
        Assert.Equal(2, rpc.Calls.Count);
        Assert.Equal("next", rpc.Calls[1].Args!.Value.GetProperty("cursor").GetString());
    }

    [Fact]
    public async Task UnknownServerRequestWithoutHandler_ReturnsMethodNotFound()
    {
        var rpc = new RpcStub();
        await using var client = Create(rpc);
        var request = new JsonRpcRequest(JsonRpcId.FromNumber(91), "unknown/method", null);
        var response = await rpc.OnServerRequest!(request);
        Assert.Equal(request.Id, response.Id);
        Assert.Null(response.Result);
        Assert.Equal(-32601, response.Error!.Code);
        Assert.Contains("unknown/method", response.Error.Message);
    }

    [Fact]
    public async Task FailedSchemaLoad_ReleasesGate_AndCanBeRetriedWithoutCachingFailure()
    {
        var schemaRequests = 0;
        var rpc = new RpcStub
        {
            Request = (method, _) => method == "tools/list" && ++schemaRequests == 1
                ? JsonSerializer.SerializeToElement(new { malformed = true })
                : JsonSerializer.SerializeToElement(new { tools = new[] { new { name = "tool", inputSchema = new { additionalProperties = false, properties = new { keep = new { } } } } } })
        };
        await using var client = Create(rpc, options: new() { StrictParsing = true });
        await Assert.ThrowsAsync<JsonException>(() => client.CallToolAsync("tool", new Dictionary<string, object?> { ["drop"] = 1 }));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await client.CallToolAsync("tool", new Dictionary<string, object?> { ["keep"] = 2, ["drop"] = 1 }, deadline.Token);
        var arguments = rpc.Calls.Last().Args!.Value.GetProperty("arguments");
        Assert.Single(arguments.EnumerateObject());
        Assert.Equal(2, arguments.GetProperty("keep").GetInt32());
        Assert.Equal(2, schemaRequests);
    }

    [Fact]
    public async Task Reply_SendsThreadAndPromptToTheReplyTool()
    {
        var rpc = new RpcStub { Request = (method, _) => JsonSerializer.SerializeToElement(method == "tools/list"
            ? (object)new { tools = Array.Empty<object>() } : new { threadId = "thread", content = new[] { new { text = "answer" } } }) };
        await using var client = Create(rpc);
        var result = await client.ReplyAsync("thread", "followup prompt");
        var call = rpc.Calls.Last();
        Assert.Equal("tools/call", call.Method);
        Assert.Equal("codex-reply", call.Args!.Value.GetProperty("name").GetString());
        var arguments = call.Args.Value.GetProperty("arguments");
        Assert.Equal("thread", arguments.GetProperty("threadId").GetString());
        Assert.Equal("followup prompt", arguments.GetProperty("prompt").GetString());
        Assert.Equal("answer", result.Text);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("1")]
    [InlineData("\"custom\"")]
    public async Task SchemasWithoutExplicitFalseAdditionalProperties_PreserveArguments(string additionalProperties)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>("{\"additionalProperties\":" + additionalProperties + ",\"properties\":{}}");
        var rpc = new RpcStub { Request = (_, _) => JsonSerializer.SerializeToElement(new { tools = new[] { new { name = "tool", inputSchema = schema } } }) };
        await using var client = Create(rpc);
        await client.CallToolAsync("tool", new Dictionary<string, object?> { ["custom"] = 42 });
        Assert.Equal(42, rpc.Calls.Last().Args!.Value.GetProperty("arguments").GetProperty("custom").GetInt32());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    public async Task ClosedSchemaWithNonObjectProperties_AllowsNoArguments(string properties)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>("{\"additionalProperties\":false,\"properties\":" + properties + "}");
        var rpc = new RpcStub { Request = (_, _) => JsonSerializer.SerializeToElement(new { tools = new[] { new { name = "tool", inputSchema = schema } } }) };
        await using var client = Create(rpc);
        await client.CallToolAsync("tool", new Dictionary<string, object?> { ["custom"] = 42 });
        Assert.Empty(rpc.Calls.Last().Args!.Value.GetProperty("arguments").EnumerateObject());
    }

    [Fact]
    public async Task StaticStart_RejectsNullOptions_AndDefaultLaunchUsesMcpServerCommand()
    {
        var failure = await Assert.ThrowsAsync<ArgumentNullException>(() => CodexMcpServerClient.StartAsync(null!));
        Assert.Equal("options", failure.ParamName);
        Assert.Equal(["mcp-server"], new CodexMcpServerClientOptions().Launch.Arguments);
    }

    private sealed class HandlerStub(bool fails) : IMcpElicitationHandler
    {
        public string? Method;
        public JsonElement? Params;
        public ValueTask<JsonElement> HandleAsync(string method, JsonElement? parameters, CancellationToken ct)
        {
            Assert.False(ct.CanBeCanceled);
            Method = method;
            Params = parameters;
            if (fails) throw new InvalidOperationException("handler failed");
            return ValueTask.FromResult(JsonSerializer.SerializeToElement(new { action = "accepted" }));
        }
    }

    private static CodexMcpServerClient Create(RpcStub rpc, ProcessStub? process = null, CodexMcpServerClientOptions? options = null) =>
        new(options ?? new(), process ?? new(), rpc, NullLogger<CodexMcpServerClient>.Instance);
    private sealed class ProcessStub : IStdioProcess
    {
        public int DisposeCount;
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => 0;
        public IReadOnlyList<string> StderrTail => [];
        public ValueTask DisposeAsync() { DisposeCount++; return ValueTask.CompletedTask; }
    }
    private sealed class RpcStub : IJsonRpcConnection
    {
        public List<(string Method, JsonElement? Args)> Calls { get; } = [];
        public List<string> Notifications { get; } = [];
        public Func<string, object?, JsonElement> Request { get; init; } = (_, _) => JsonSerializer.SerializeToElement(new { });
        public Exception? DisposeFailure { get; init; }
        public int DisposeCount;
        public event Func<JsonRpcNotification, ValueTask>? OnNotification { add { } remove { } }
        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }
        public Task<JsonElement> SendRequestAsync(string method, object? args, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add((method, args is null ? null : JsonSerializer.SerializeToElement(args)));
            return Task.FromResult(Request(method, args));
        }
        public Task SendNotificationAsync(string method, object? args, CancellationToken ct) { Notifications.Add(method); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { DisposeCount++; return DisposeFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(DisposeFailure); }
    }
}
