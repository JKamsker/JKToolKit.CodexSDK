using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Protocol.SandboxPolicy;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ReadOnlyAccessOverrideGatingTests
{
    [Fact]
    public async Task StartTurnAsync_CachesReadOnlyAccessRejection_AndThrowsBeforeSendingNextTime()
    {
        var rpc = new FakeRpc();

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var options = new TurnStartOptions
        {
            Input = [TurnInputItem.Text("hi")],
            SandboxPolicy = new SandboxPolicy.ReadOnly
            {
                Access = new ReadOnlyAccess.FullAccess()
            }
        };

        var first = async () => await client.StartTurnAsync("thr_1", options);
        await first.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*rejected sandboxPolicy parameters*");

        rpc.RequestCount.Should().Be(1);

        var second = async () => await client.StartTurnAsync("thr_1", options);
        await second.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*previously rejected*");

        rpc.RequestCount.Should().Be(1, "second call should be gated before sending JSON-RPC request");
    }

    private sealed class FakeProcess : IStdioProcess
    {
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _tcs.Task;

        public int? ProcessId => 1;

        public int? ExitCode => null;

        public IReadOnlyList<string> StderrTail => Array.Empty<string>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeRpc : IJsonRpcConnection
    {
        public int RequestCount { get; private set; }

#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            method.Should().Be("turn/start");

            throw new JsonRpcRemoteException(new JsonRpcError(
                -32602,
                "Invalid params",
                Data: null));
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
