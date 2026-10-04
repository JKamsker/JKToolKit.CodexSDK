using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class DisconnectMutationRegressionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(137)]
    public async Task ProcessExit_FaultsAllObserversAndPreservesQueuedNotifications(int? exitCode)
    {
        var process = new ControlledProcess(exitCode);
        var rpc = new HighLevelTurnTests.Rpc();
        await using var client = new CodexAppServerClient(new(), process, rpc, NullLogger.Instance, startExitWatcher: true);
        await using var handle = await client.StartTurnAsync("t", new() { Input = [] });
        await using var subscriber = handle.Subscribe();
        Assert.False(handle.Completion.IsCompleted);
        await rpc.Item("i", "commentary", "queued");
        process.Exit.TrySetResult();
        var failure = await Assert.ThrowsAsync<CodexAppServerDisconnectedException>(() => handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(42, failure.ProcessId);
        Assert.Equal(exitCode, failure.ExitCode);
        Assert.Equal(new[] { "process diagnostic" }, failure.StderrTail);
        Assert.Contains("process diagnostic", failure.Message);
        if (exitCode is not null) Assert.Contains("137", failure.Message);
        Assert.Same(failure, await Record.ExceptionAsync(() => handle.Completion.WaitAsync(TimeSpan.FromSeconds(5))));
        await AssertQueuedThenFailed(handle.Events(), failure);
        await AssertQueuedThenFailed(handle.EventsRaw(), failure);
        await AssertQueuedThenFailed(subscriber.Events(), failure);
        await rpc.Item("late", "commentary", "must not drain or append");
        await AssertQueuedThenFailed(client.Notifications(), failure);
        await AssertQueuedThenFailed(client.NotificationsRaw(), failure);
        await handle.DisposeAsync();
        await using var late = handle.Subscribe();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var reader = late.Events(timeout.Token).GetAsyncEnumerator();
        Assert.Same(failure, await Record.ExceptionAsync(async () => { await reader.MoveNextAsync(); }));
    }

    private static async Task AssertQueuedThenFailed<T>(IAsyncEnumerable<T> source, Exception failure)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var count = 0;
        var error = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in source.WithCancellation(timeout.Token)) count++;
        });
        Assert.Equal(1, count);
        Assert.Same(failure, error);
    }

    private sealed class ControlledProcess(int? exitCode) : IStdioProcess
    {
        public TaskCompletionSource Exit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => Exit.Task;
        public int? ProcessId => 42;
        public int? ExitCode => exitCode;
        public IReadOnlyList<string> StderrTail => ["process diagnostic"];
        public ValueTask DisposeAsync() { Exit.TrySetResult(); return ValueTask.CompletedTask; }
    }
}
