using System.Linq;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpToolsListPaginationTests
{
    [Fact]
    public async Task ListToolsAsync_WhenNextCursor_PaginatesAndConcatenatesPages()
    {
        var callCount = 0;

        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                if (method != "tools/list")
                {
                    return Task.FromResult(Parse("{}"));
                }

                callCount++;

                if (@params is null)
                {
                    return Task.FromResult(Parse("""{"tools":[{"name":"t1"}],"nextCursor":"c1"}"""));
                }

                var cursor = Parse(JsonSerializer.Serialize(@params)).GetProperty("cursor").GetString();
                cursor.Should().Be("c1");
                return Task.FromResult(Parse("""{"tools":[{"name":"t2"}]}"""));
            }
        };

        await using var client = new CodexMcpServerClient(
            new CodexMcpServerClientOptions(),
            process: new FakeProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var tools = await client.ListToolsAsync();
        tools.Select(t => t.Name).Should().Equal("t1", "t2");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ListToolsAsync_WhenStrictParsing_ThrowsOnUnexpectedShape()
    {
        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, _, _) =>
                Task.FromResult(Parse("{}"))
        };

        await using var client = new CodexMcpServerClient(
            new CodexMcpServerClientOptions { StrictParsing = true },
            process: new FakeProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var act = async () => await client.ListToolsAsync();
        await act.Should().ThrowAsync<JsonException>();
    }

    private static JsonElement Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private sealed class FakeJsonRpcConnection : IJsonRpcConnection
    {
#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; init; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            SendRequestAsyncImpl is null ? Task.FromResult(Parse("{}")) : SendRequestAsyncImpl(method, @params, ct);

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;

        public int? ProcessId => 1;

        public int? ExitCode => 0;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
