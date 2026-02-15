using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ReviewDetachedPlumbingTests
{
    [Fact]
    public async Task StartReviewAsync_Detached_CreatesTurnHandle_AndRoutesCompletionByTurnId()
    {
        var turnId = "turn_detached_1";
        var reviewThreadId = "thr_review_1";

        var rpc = new FakeRpc
        {
            Result = JsonSerializer.SerializeToElement(new
            {
                reviewThreadId,
                turn = new { id = turnId, threadId = reviewThreadId }
            })
        };

        await using var client = new CodexAppServerClient(
            new CodexAppServerClientOptions(),
            new FakeProcess(),
            rpc,
            NullLogger.Instance,
            startExitWatcher: false);

        var review = await client.StartReviewAsync(new ReviewStartOptions
        {
            ThreadId = "thr_original",
            Delivery = ReviewDelivery.Detached,
            Target = new ReviewTarget.UncommittedChanges()
        });

        review.ReviewThreadId.Should().Be(reviewThreadId);
        review.Turn.ThreadId.Should().Be(reviewThreadId);
        review.Turn.TurnId.Should().Be(turnId);

        var completionTask = review.Turn.Completion;

        await rpc.RaiseAsync(new JsonRpcNotification(
            "turn/completed",
            JsonSerializer.SerializeToElement(new
            {
                threadId = "some_other_thread",
                turn = new { id = turnId, status = "completed" }
            })));

        var completed = await completionTask;
        completed.TurnId.Should().Be(turnId);
        completed.Status.Should().Be("completed");
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
        public JsonElement Result { get; init; }

        public event Func<JsonRpcNotification, ValueTask>? OnNotification;

        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

        public Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            method.Should().Be("review/start");
            return Task.FromResult(Result);
        }

        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask RaiseAsync(JsonRpcNotification notification) =>
            OnNotification?.Invoke(notification) ?? ValueTask.CompletedTask;
    }
}
