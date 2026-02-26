using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using JKToolKit.CodexSDK.McpServer;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class McpServerStartLifecycleTests
{
    [Fact]
    public async Task CreateInitializedAsync_WhenInitializeFails_DisposesProcessAndRpc()
    {
        using var doc = JsonDocument.Parse("{\"details\":123}");

        var options = new CodexMcpServerClientOptions
        {
            StartupTimeout = TimeSpan.FromSeconds(5)
        };

        var process = new TrackingProcess();
        var rpc = new TrackingRpc(new JsonRpcRemoteException(new JsonRpcError(-32602, "invalid params", doc.RootElement)));

        var act = async () => await CodexMcpServerClient.CreateInitializedAsync(
            options,
            process,
            rpc,
            NullLogger<CodexMcpServerClient>.Instance,
            CancellationToken.None);

        await act.Should().ThrowAsync<JsonRpcRemoteException>();
        process.IsDisposed.Should().BeTrue();
        rpc.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateInitializedAsync_WhenServerNeverResponds_TimesOutAndDisposes()
    {
        var options = new CodexMcpServerClientOptions
        {
            StartupTimeout = TimeSpan.FromMilliseconds(50)
        };

        var process = new TrackingProcess();
        var rpc = new TrackingRpc(exceptionToThrow: null, hangOnInitialize: true);

        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var act = async () => await CodexMcpServerClient.CreateInitializedAsync(
            options,
            process,
            rpc,
            NullLogger<CodexMcpServerClient>.Instance,
            testCts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        process.IsDisposed.Should().BeTrue();
        rpc.IsDisposed.Should().BeTrue();
    }

    private sealed class TrackingProcess : IStdioProcess
    {
        public Task Completion { get; } = Task.CompletedTask;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail { get; } = [];

        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingRpc : IJsonRpcConnection
    {
        private readonly Exception? _exceptionToThrow;
        private readonly bool _hangOnInitialize;

        public TrackingRpc(Exception? exceptionToThrow, bool hangOnInitialize = false)
        {
            _exceptionToThrow = exceptionToThrow;
            _hangOnInitialize = hangOnInitialize;
        }

        public bool IsDisposed { get; private set; }

        public event Func<JsonRpcNotification, ValueTask>? OnNotification
        {
            add { }
            remove { }
        }

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            if (_hangOnInitialize && string.Equals(method, "initialize", StringComparison.Ordinal))
            {
                return HangAsync(ct);
            }

            if (_exceptionToThrow is not null)
            {
                return Task.FromException<JsonElement>(_exceptionToThrow);
            }

            return Task.FromResult(default(JsonElement));
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }

        private static async Task<JsonElement> HangAsync(CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return default;
        }
    }
}

