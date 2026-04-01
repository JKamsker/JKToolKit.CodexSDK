using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class TurnAndReviewResultValidationTests
{
    [Fact]
    public async Task SteerTurnRawAsync_Throws_WhenResponseOmitsTurnId()
    {
        var rpc = new SequencedRpc();
        rpc.EnqueueResult("turn/steer", JsonSerializer.SerializeToElement(new { ok = true }));

        await using var client = CreateClient(rpc);

        var act = async () => await client.SteerTurnRawAsync(new TurnSteerOptions
        {
            ThreadId = "thr_1",
            ExpectedTurnId = "turn_expected",
            Input = [TurnInputItem.Text("continue")]
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*turn/steer returned no turn id*");
    }

    [Fact]
    public async Task StartReviewAsync_Throws_WhenResponseOmitsReviewThreadId()
    {
        var rpc = new SequencedRpc();
        rpc.EnqueueResult("review/start", JsonSerializer.SerializeToElement(new
        {
            turn = new { id = "turn_1", threadId = "thr_1" }
        }));

        await using var client = CreateClient(rpc);

        var act = async () => await client.StartReviewAsync(new ReviewStartOptions
        {
            ThreadId = "thr_1",
            Target = new ReviewTarget.UncommittedChanges()
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*review/start returned no review thread id*");
    }

    private static CodexAppServerClient CreateClient(IJsonRpcConnection rpc) =>
        new(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

    private sealed class FakeProcess : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => null;
        public IReadOnlyList<string> StderrTail => Array.Empty<string>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SequencedRpc : IJsonRpcConnection
    {
        private readonly Queue<(string Method, JsonElement Result)> _results = new();

#pragma warning disable CS0067
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
#pragma warning restore CS0067

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public void EnqueueResult(string method, JsonElement result) => _results.Enqueue((method, result));

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            _results.Should().NotBeEmpty();
            var (expectedMethod, result) = _results.Dequeue();
            method.Should().Be(expectedMethod);
            return Task.FromResult(result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
