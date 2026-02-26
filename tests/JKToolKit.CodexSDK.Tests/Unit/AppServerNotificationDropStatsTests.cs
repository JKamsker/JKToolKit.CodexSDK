using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerNotificationDropStatsTests
{
    [Fact]
    public async Task NotificationDropStats_WhenGlobalBuffersOverflow_IncrementsDropCounters()
    {
        var options = new CodexAppServerClientOptions
        {
            NotificationBufferCapacity = 1
        };

        var rpc = new FakeJsonRpcConnection();
        await using var client = new CodexAppServerClient(
            options,
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        client.NotificationDropStats.GlobalNotificationsDropped.Should().Be(0);
        client.NotificationDropStats.GlobalRawNotificationsDropped.Should().Be(0);

        using var p = JsonDocument.Parse("{}");

        await rpc.EmitNotificationAsync("note", p.RootElement);
        await rpc.EmitNotificationAsync("note", p.RootElement);

        var stats = client.NotificationDropStats;
        stats.GlobalNotificationsDropped.Should().Be(1);
        stats.GlobalRawNotificationsDropped.Should().Be(1);
    }

    private sealed class FakeJsonRpcConnection : IJsonRpcConnection
    {
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            Task.FromResult(default(JsonElement));

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public async ValueTask EmitNotificationAsync(string method, JsonElement @params)
        {
            var handler = OnNotification;
            if (handler is null)
            {
                return;
            }

            await handler(new JsonRpcNotification(method, @params));
        }

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

