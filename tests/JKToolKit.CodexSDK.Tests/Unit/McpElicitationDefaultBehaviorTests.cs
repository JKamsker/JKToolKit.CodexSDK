using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpElicitationDefaultBehaviorTests
{
    [Fact]
    public async Task CallToolAsync_WhenElicitationArrivesWithoutHandler_RespondsDenied_AndCompletes()
    {
        JsonRpcResponse? capturedResponse = null;

        var rpc = new FakeJsonRpcConnection();
        rpc.SendRequestAsyncImpl = async (method, _, ct) =>
        {
            if (method == "tools/call")
            {
                var handler = rpc.OnServerRequest;
                handler.Should().NotBeNull("client should register an OnServerRequest handler");

                using var p = JsonDocument.Parse("{\"message\":\"x\",\"requestedSchema\":{}}");
                var req = new JsonRpcRequest(JsonRpcId.FromNumber(123), "elicitation/create", p.RootElement);
                capturedResponse = await handler!(req);

                return Parse("""{"structuredContent":{"threadId":"t1"},"content":[{"text":"ok"}]}""");
            }

            return Parse("{}");
        };

        await using var client = new CodexMcpServerClient(
            new CodexMcpServerClientOptions { ElicitationHandler = null },
            process: new FakeProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        var call = await client.CallToolAsync("codex", new { prompt = "hi" });
        call.Raw.ValueKind.Should().Be(JsonValueKind.Object);

        capturedResponse.Should().NotBeNull();
        capturedResponse!.Error.Should().BeNull();
        capturedResponse.Result.Should().NotBeNull();
        capturedResponse.Result!.Value.GetProperty("decision").GetString().Should().Be("denied");
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

        public Func<string, object?, CancellationToken, Task<JsonElement>>? SendRequestAsyncImpl { get; set; }

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
