using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class AppServerClientGuardrailSeamTests
{
    [Fact]
    public async Task ResumeThread_WithExperimentalPath_ThrowsBeforeSendingRequest()
    {
        var rpc = new FakeRpc();
        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var act = async () => await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            ThreadId = "thr_123",
            Path = "/tmp/rollout"
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
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

        public event Func<JsonRpcNotification, ValueTask>? OnNotification;

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            throw new InvalidOperationException("SendRequestAsync should not be called for guardrail failures.");
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        // ReSharper disable once UnusedMember.Local
        public ValueTask RaiseAsync(JsonRpcNotification notification) =>
            OnNotification?.Invoke(notification) ?? ValueTask.CompletedTask;
    }
}
