using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;
using JKToolKit.CodexSDK.AppServer.Protocol.V2;

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
            Path = "/tmp/rollout"
        });

        await act.Should().ThrowAsync<CodexExperimentalApiRequiredException>();
        rpc.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task ResumeThread_WithPathOnly_AllowsNullThreadId_WhenExperimentalEnabled()
    {
        using var doc = JsonDocument.Parse("""{"threadId":"thr_1"}""");
        var rpc = new RecordingRpc { Result = doc.RootElement };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions { ExperimentalApi = true },
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var thread = await client.ResumeThreadAsync(new ThreadResumeOptions
        {
            Path = "/tmp/rollout"
        });

        thread.Id.Should().Be("thr_1");
        rpc.RequestCount.Should().Be(1);
        rpc.LastMethod.Should().Be("thread/resume");
        rpc.LastParams.Should().BeOfType<ThreadResumeParams>();
        ((ThreadResumeParams)rpc.LastParams!).ThreadId.Should().BeNull();
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
            throw new InvalidOperationException("SendRequestAsync should not be called for guardrail failures.");
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        // ReSharper disable once UnusedMember.Local
        public ValueTask RaiseAsync(JsonRpcNotification notification) =>
            OnNotification?.Invoke(notification) ?? ValueTask.CompletedTask;
    }

    private sealed class RecordingRpc : IJsonRpcConnection
    {
        public int RequestCount { get; private set; }
        public string? LastMethod { get; private set; }
        public object? LastParams { get; private set; }

#pragma warning disable CS0067 // Event is part of the IJsonRpcConnection contract; tests don't need to raise it.
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public required JsonElement Result { get; init; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            RequestCount++;
            LastMethod = method;
            LastParams = @params;
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
