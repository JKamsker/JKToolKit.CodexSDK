using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using JKToolKit.CodexSDK.McpServer.Overrides;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpServerOverridePipelineTests
{
    [Fact]
    public async Task ListToolsAsync_AppliesResponseTransformers()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, _, _) =>
                Task.FromResult(Parse("""{"tools":[{"name":"before","description":"x"}]}"""))
        };

        var options = new CodexMcpServerClientOptions
        {
            ResponseTransformers = new IMcpServerResponseTransformer[]
            {
                new ResponseTransformer((method, result) =>
                    method == "tools/list"
                        ? Parse("""{"tools":[{"name":"after","description":"x"}]}""")
                        : result)
            }
        };

        await using var client = new CodexMcpServerClient(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var tools = await client.ListToolsAsync();
        tools.Should().ContainSingle();
        tools[0].Name.Should().Be("after");
    }

    [Fact]
    public async Task ListToolsAsync_CustomMappersTakePrecedence()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, _, _) =>
                Task.FromResult(Parse("""{"tools":[{"name":"before","description":"x"}]}"""))
        };

        var options = new CodexMcpServerClientOptions
        {
            ToolsListMappers = new IMcpToolsListMapper[]
            {
                new ToolsListMapper(_ => new[] { new McpToolDescriptor("custom", "y", InputSchema: null) })
            }
        };

        await using var client = new CodexMcpServerClient(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var tools = await client.ListToolsAsync();
        tools.Should().ContainSingle();
        tools[0].Name.Should().Be("custom");
    }

    [Fact]
    public async Task ReplyAsync_AppliesToolResultTransformers()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, _, _) =>
                Task.FromResult(Parse("""{"structuredContent":{"threadId":"t1"},"content":[{"text":"hi"}]}"""))
        };

        var options = new CodexMcpServerClientOptions
        {
            CodexToolResultTransformers = new ICodexMcpToolResultTransformer[]
            {
                new ToolResultTransformer((tool, raw) =>
                    tool == "codex-reply"
                        ? Parse("""{"structuredContent":{"threadId":"t2"},"content":[{"text":"hi"}]}""")
                        : raw)
            }
        };

        await using var client = new CodexMcpServerClient(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var reply = await client.ReplyAsync(threadId: "ignored", prompt: "hello");
        reply.ThreadId.Should().Be("t2");
        reply.Text.Should().Be("hi");
    }

    [Fact]
    public async Task ReplyAsync_SwallowsMapperExceptions_AndCustomMapperTakesPrecedence()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, _, _) =>
                Task.FromResult(Parse("""{"structuredContent":{"threadId":"t1"},"content":[{"text":"hi"}]}"""))
        };

        var options = new CodexMcpServerClientOptions
        {
            CodexToolResultMappers = new ICodexMcpToolResultMapper[]
            {
                new ThrowingToolResultMapper(),
                new ToolResultMapper((tool, raw) =>
                    tool == "codex-reply"
                        ? new CodexMcpToolParsedResult("custom", "mapped", Parse("""{"x":1}"""), raw)
                        : null)
            }
        };

        await using var client = new CodexMcpServerClient(
            options,
            process: new FakeStdioProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var reply = await client.ReplyAsync(threadId: "ignored", prompt: "hello");
        reply.ThreadId.Should().Be("custom");
        reply.Text.Should().Be("mapped");
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class ResponseTransformer : IMcpServerResponseTransformer
    {
        private readonly Func<string, JsonElement, JsonElement> _transform;

        public ResponseTransformer(Func<string, JsonElement, JsonElement> transform)
        {
            _transform = transform;
        }

        public JsonElement Transform(string method, JsonElement result) => _transform(method, result);
    }

    private sealed class ToolsListMapper : IMcpToolsListMapper
    {
        private readonly Func<JsonElement, IReadOnlyList<McpToolDescriptor>?> _map;

        public ToolsListMapper(Func<JsonElement, IReadOnlyList<McpToolDescriptor>?> map)
        {
            _map = map;
        }

        public IReadOnlyList<McpToolDescriptor>? TryMap(JsonElement result) => _map(result);
    }

    private sealed class ToolResultTransformer : ICodexMcpToolResultTransformer
    {
        private readonly Func<string, JsonElement, JsonElement> _transform;

        public ToolResultTransformer(Func<string, JsonElement, JsonElement> transform)
        {
            _transform = transform;
        }

        public JsonElement Transform(string toolName, JsonElement raw) => _transform(toolName, raw);
    }

    private sealed class ToolResultMapper : ICodexMcpToolResultMapper
    {
        private readonly Func<string, JsonElement, CodexMcpToolParsedResult?> _map;

        public ToolResultMapper(Func<string, JsonElement, CodexMcpToolParsedResult?> map)
        {
            _map = map;
        }

        public CodexMcpToolParsedResult? TryMap(string toolName, JsonElement raw) => _map(toolName, raw);
    }

    private sealed class ThrowingToolResultMapper : ICodexMcpToolResultMapper
    {
        public CodexMcpToolParsedResult? TryMap(string toolName, JsonElement raw) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class FakeJsonRpcConnection : IJsonRpcConnection
    {
#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; init; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            if (SendRequestAsyncImpl is null)
            {
                return Task.FromResult(Parse("{}"));
            }

            return SendRequestAsyncImpl(method, @params, ct);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStdioProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;

        public int? ProcessId => 1;

        public int? ExitCode => 0;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
