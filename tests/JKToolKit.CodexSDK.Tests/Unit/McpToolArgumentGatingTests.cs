using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpToolArgumentGatingTests
{
    [Fact]
    public async Task StartSessionAsync_FiltersUnknownArguments_NotInServerSchema()
    {
        JsonElement? capturedCallParams = null;

        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                if (method == "tools/list")
                {
                    return Task.FromResult(Parse(
                        """
                        {
                          "tools": [
                            {
                              "name": "codex",
                              "inputSchema": { "type": "object", "additionalProperties": false, "properties": { "prompt": {}, "cwd": {} } }
                            }
                          ]
                        }
                        """));
                }

                if (method == "tools/call")
                {
                    capturedCallParams = Parse(JsonSerializer.Serialize(@params));
                    return Task.FromResult(Parse("""{"structuredContent":{"threadId":"t1"},"content":[{"text":"ok"}]}"""));
                }

                return Task.FromResult(Parse("{}"));
            }
        };

        await using var client = new CodexMcpServerClient(
            new CodexMcpServerClientOptions(),
            process: new FakeProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        await client.StartSessionAsync(new CodexMcpStartOptions
        {
            Prompt = "hello",
            Cwd = "c:\\repo",
            IncludePlanTool = true
        });

        capturedCallParams.Should().NotBeNull();
        var args = capturedCallParams!.Value.GetProperty("arguments");
        args.TryGetProperty("prompt", out _).Should().BeTrue();
        args.TryGetProperty("cwd", out _).Should().BeTrue();
        args.TryGetProperty("include-plan-tool", out _).Should().BeFalse();
    }

    [Fact]
    public async Task StartSessionAsync_DoesNotFilterUnknownArguments_WhenSchemaAllowsAdditionalProperties()
    {
        JsonElement? capturedCallParams = null;

        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                if (method == "tools/list")
                {
                    return Task.FromResult(Parse(
                        """
                        {
                          "tools": [
                            {
                              "name": "codex",
                              "inputSchema": { "type": "object", "properties": { "prompt": {}, "cwd": {} } }
                            }
                          ]
                        }
                        """));
                }

                if (method == "tools/call")
                {
                    capturedCallParams = Parse(JsonSerializer.Serialize(@params));
                    return Task.FromResult(Parse("""{"structuredContent":{"threadId":"t1"},"content":[{"text":"ok"}]}"""));
                }

                return Task.FromResult(Parse("{}"));
            }
        };

        await using var client = new CodexMcpServerClient(
            new CodexMcpServerClientOptions(),
            process: new FakeProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        await client.StartSessionAsync(new CodexMcpStartOptions
        {
            Prompt = "hello",
            Cwd = "c:\\repo",
            IncludePlanTool = true
        });

        capturedCallParams.Should().NotBeNull();
        var args = capturedCallParams!.Value.GetProperty("arguments");
        args.TryGetProperty("prompt", out _).Should().BeTrue();
        args.TryGetProperty("cwd", out _).Should().BeTrue();
        args.TryGetProperty("include-plan-tool", out _).Should().BeTrue();
    }

    [Fact]
    public async Task StartSessionAsync_AllowsArguments_DeclaredInServerSchema()
    {
        JsonElement? capturedCallParams = null;

        var rpc = new FakeJsonRpcConnection
        {
            SendRequestAsyncImpl = (method, @params, _) =>
            {
                if (method == "tools/list")
                {
                    return Task.FromResult(Parse(
                        """
                        {
                          "tools": [
                            {
                              "name": "codex",
                              "inputSchema": { "type": "object", "properties": { "prompt": {}, "include-plan-tool": {} } }
                            }
                          ]
                        }
                        """));
                }

                if (method == "tools/call")
                {
                    capturedCallParams = Parse(JsonSerializer.Serialize(@params));
                    return Task.FromResult(Parse("""{"structuredContent":{"threadId":"t1"},"content":[{"text":"ok"}]}"""));
                }

                return Task.FromResult(Parse("{}"));
            }
        };

        await using var client = new CodexMcpServerClient(
            new CodexMcpServerClientOptions(),
            process: new FakeProcess(),
            rpc: rpc,
            logger: NullLogger<CodexMcpServerClient>.Instance);

        await client.StartSessionAsync(new CodexMcpStartOptions
        {
            Prompt = "hello",
            IncludePlanTool = true
        });

        capturedCallParams.Should().NotBeNull();
        var args = capturedCallParams!.Value.GetProperty("arguments");
        args.TryGetProperty("include-plan-tool", out _).Should().BeTrue();
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
