using System.Linq;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpServerWrappersTests
{
    [Fact]
    public async Task ReloadMcpServersAsync_CallsExpectedMethod()
    {
        var rpc = new FakeRpc
        {
            AssertMethod = "config/mcpServer/reload",
            AssertParams = p => p.Should().BeNull(),
            Result = JsonSerializer.SerializeToElement(new { })
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        await client.ReloadMcpServersAsync();
    }

    [Fact]
    public async Task ListMcpServerStatusAsync_CallsExpectedMethod_AndParsesServers()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            data = new object[]
            {
                new
                {
                    name = "docs",
                    authStatus = "notLoggedIn",
                    tools = new
                    {
                        search = new { name = "search", description = "Search", inputSchema = new { type = "object" } }
                    },
                    resources = Array.Empty<object>(),
                    resourceTemplates = Array.Empty<object>()
                }
            },
            nextCursor = (string?)null
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "mcpServerStatus/list",
            AssertParams = p =>
            {
                p.Should().BeOfType<ListMcpServerStatusParams>();
                var typed = (ListMcpServerStatusParams)p!;
                typed.Cursor.Should().Be("0");
                typed.Limit.Should().Be(10);
            },
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var page = await client.ListMcpServerStatusAsync(new McpServerStatusListOptions { Cursor = "0", Limit = 10 });

        page.Servers.Should().HaveCount(1);
        page.Servers[0].Name.Should().Be("docs");
        page.Servers[0].AuthStatus.Should().Be(McpAuthStatus.NotLoggedIn);
        page.Servers[0].Tools.Should().ContainSingle(t => t.Name == "search");
    }

    [Fact]
    public async Task StartMcpServerOauthLoginAsync_CallsExpectedMethod_AndParsesAuthorizationUrl()
    {
        var rawResult = JsonSerializer.SerializeToElement(new
        {
            authorizationUrl = "https://example.com/oauth"
        });

        var rpc = new FakeRpc
        {
            AssertMethod = "mcpServer/oauth/login",
            AssertParams = p =>
            {
                p.Should().BeOfType<McpServerOauthLoginParams>();
                var typed = (McpServerOauthLoginParams)p!;
                typed.Name.Should().Be("my-server");
                typed.TimeoutSecs.Should().Be(30);
                typed.Scopes.Should().BeNull();
            },
            Result = rawResult
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var result = await client.StartMcpServerOauthLoginAsync(new McpServerOauthLoginOptions
        {
            Name = "my-server",
            TimeoutSeconds = 30
        });

        result.AuthorizationUrl.Should().Be("https://example.com/oauth");
    }

    [Fact]
    public void CodexConfigOverridesBuilder_SetsMcpServerKeys_AsDottedPaths()
    {
        var overrides = new CodexConfigOverridesBuilder()
            .SetMcpServerStdio(
                name: "shell-tool",
                command: "npx",
                args: ["-y", "@openai/codex-shell-tool-mcp"],
                enabled: true,
                required: false);

        var element = overrides.Build();

        element.ValueKind.Should().Be(JsonValueKind.Object);
        element.TryGetProperty("mcp_servers.shell-tool.command", out var cmd).Should().BeTrue();
        cmd.GetString().Should().Be("npx");

        element.TryGetProperty("mcp_servers.shell-tool.args", out var args).Should().BeTrue();
        args.ValueKind.Should().Be(JsonValueKind.Array);
        args.EnumerateArray().Select(a => a.GetString()).Should().Equal("-y", "@openai/codex-shell-tool-mcp");
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
