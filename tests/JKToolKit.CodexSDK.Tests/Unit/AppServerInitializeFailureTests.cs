using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerInitializeFailureTests
{
    [Fact]
    public async Task InitializeAsync_WhenRemoteError_ThrowsCodexAppServerInitializeException_WithHelp()
    {
        using var doc = JsonDocument.Parse("{\"details\":123}");

        var options = new CodexAppServerClientOptions
        {
            ExperimentalApi = true
        };

        await using var client = new CodexAppServerClient(
            options,
            new FakeProcess(["oops"]),
            new FakeRpc(new JsonRpcRemoteException(new JsonRpcError(-32602, "invalid params", doc.RootElement))),
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.InitializeAsync(new AppServerClientInfo("id", "name", "1"));

        var ex = await act.Should().ThrowAsync<CodexAppServerInitializeException>();
        ex.Which.Code.Should().Be(-32602);
        ex.Which.RemoteMessage.Should().Be("invalid params");
        ex.Which.DataJson.Should().Contain("\"details\":123");
        ex.Which.Help.Should().NotBeNullOrWhiteSpace();
        ex.Which.StderrTail.Should().Contain("oops");
    }

    private sealed class FakeProcess : IStdioProcess
    {
        public FakeProcess(IReadOnlyList<string> stderrTail) => StderrTail = stderrTail;

        public Task Completion { get; } = Task.CompletedTask;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail { get; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        private readonly Exception _exception;

        public FakeRpc(Exception exception) => _exception = exception;

        public event Func<JsonRpcNotification, ValueTask>? OnNotification
        {
            add { }
            remove { }
        }

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct) =>
            Task.FromException<JsonElement>(_exception);

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
