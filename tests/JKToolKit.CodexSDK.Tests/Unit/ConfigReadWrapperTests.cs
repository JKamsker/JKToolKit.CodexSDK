using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ConfigReadWrapperTests
{
    [Fact]
    public async Task ReadConfigAsync_CallsExpectedMethod_AndParsesMcpServers()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            config = new
            {
                mcp_servers = new
                {
                    docs = new
                    {
                        url = "https://example.com/mcp",
                        bearer_token_env_var = "MCP_TOKEN",
                        enabled = true,
                        required = false
                    }
                }
            },
            origins = new
            {
                model = new
                {
                    name = new { type = "user", file = "C:/Users/me/.codex/config.toml" },
                    version = "1"
                }
            },
            layers = new object[]
            {
                new
                {
                    name = new { type = "user", file = "C:/Users/me/.codex/config.toml" },
                    version = "1",
                    config = new { model = "gpt-5.2-codex" },
                    disabledReason = (string?)null
                }
            }
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "config/read",
            AssertParams = p =>
            {
                p.Should().BeOfType<ConfigReadParams>();
                var typed = (ConfigReadParams)p!;
                typed.IncludeLayers.Should().BeTrue();
                typed.Cwd.Should().Be("C:/repo");
            },
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.ReadConfigAsync(new ConfigReadOptions
        {
            IncludeLayers = true,
            Cwd = "C:/repo"
        });

        result.Config.ValueKind.Should().Be(JsonValueKind.Object);
        result.Origins.Should().NotBeNull();
        result.Layers.Should().NotBeNull();
        result.McpServers.Should().NotBeNull();

        result.McpServers!.Should().ContainKey("docs");
        result.McpServers["docs"].Transport.Should().Be("streamableHttp");
        result.McpServers["docs"].Url.Should().Be("https://example.com/mcp");
        result.McpServers["docs"].BearerTokenEnvVar.Should().Be("MCP_TOKEN");
        result.McpServers["docs"].Enabled.Should().BeTrue();
    }

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => _tcs.Task;
        public int? ProcessId => 1;
        public int? ExitCode => null;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync()
        {
            _tcs.TrySetCanceled();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public string AssertMethod { get; init; } = string.Empty;
        public Action<object?>? AssertParams { get; init; }
        public JsonElement Result { get; init; }

#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            method.Should().Be(AssertMethod);
            AssertParams?.Invoke(@params);
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

