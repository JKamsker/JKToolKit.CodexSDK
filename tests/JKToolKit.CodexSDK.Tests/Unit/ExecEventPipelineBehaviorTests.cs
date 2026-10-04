using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecEventPipelineBehaviorTests
{
    internal static CodexSessionInfo Info => new(SessionId.Parse("test-session"), "log.jsonl", DateTimeOffset.UtcNow);
    internal static CodexEvent Event(DateTimeOffset? timestamp = null) => new TokenCountEvent
    {
        Timestamp = timestamp ?? DateTimeOffset.UtcNow, Type = "token_count", RawPayload = JsonSerializer.SerializeToElement(new { })
    };

    [Fact]
    public async Task TimestampFilter_ExcludesBoundaryAndOlder_PreservesOrder()
    {
        var threshold = DateTimeOffset.UtcNow;
        var expected = Event(threshold.AddTicks(1));
        var pipeline = Create();
        var actual = await Collect(pipeline.ApplyTimestampFilter(Events([Event(threshold.AddTicks(-1)), Event(threshold), expected]),
            new EventStreamOptions(AfterTimestamp: threshold), default));
        Assert.Equal([expected], actual);
        Assert.Equal(3, (await Collect(pipeline.ApplyTimestampFilter(Events([expected, expected, expected]), new(), default))).Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(5000)]
    public async Task IdleTimeout_CompletedStream_DoesNotCancelPipeline(int milliseconds)
    {
        using var pipelineCts = new CancellationTokenSource();
        var evt = Event();
        Assert.Equal([evt], await Collect(Create().ApplyIdleTimeout(Events([evt]), TimeSpan.FromMilliseconds(milliseconds), pipelineCts, default)));
        Assert.False(pipelineCts.IsCancellationRequested);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    public async Task IdleTimeout_StopsStreamAndTerminatesOnlyOnce(bool hasProcess, bool acquireTermination, bool terminateThrows)
    {
        using var process = hasProcess ? new Process() : null;
        var launcher = new MockCodexProcessLauncher { TerminateExitCode = 73, SimulateTerminateFailure = terminateThrows };
        var exits = new List<(int, SessionExitReason)>();
        using var pipelineCts = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var disposed = false;
        var pipeline = new CodexSessionHandleEventPipeline(Info, process, launcher, TimeSpan.FromSeconds(7), NullLogger.Instance,
            (code, reason) => exits.Add((code, reason)), () => acquireTermination);
        var actual = await Collect(pipeline.ApplyIdleTimeout(WaitAfterEvent(() => disposed = true), TimeSpan.FromMilliseconds(120), pipelineCts, deadline.Token));
        Assert.Single(actual);
        Assert.True(disposed);
        Assert.True(pipelineCts.IsCancellationRequested);
        Assert.Equal(hasProcess && acquireTermination ? 1 : 0, launcher.CapturedTerminations.Count);
        if (hasProcess && acquireTermination && !terminateThrows)
        {
            Assert.Equal([(73, SessionExitReason.Timeout)], exits);
            Assert.Equal(TimeSpan.FromSeconds(7), launcher.CapturedTerminations[0].Timeout);
        }
        else Assert.Empty(exits);
    }

    [Fact]
    public async Task IdleTimeout_UserCancellationBeforeFirstEvent_PropagatesAndDisposes()
    {
        using var pipelineCts = new CancellationTokenSource();
        using var userCts = new CancellationTokenSource();
        var disposed = false;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reading = Collect(Create().ApplyIdleTimeout(WaitWithoutEvent(started, () => disposed = true), TimeSpan.FromMilliseconds(1), pipelineCts, userCts.Token));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        userCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reading);
        Assert.True(disposed);
        Assert.False(pipelineCts.IsCancellationRequested);
    }

    [Fact]
    public async Task IdleTimeout_CancellationAfterEvent_RemainsUserCancellation()
    {
        using var pipelineCts = new CancellationTokenSource();
        using var userCts = new CancellationTokenSource();
        await using var enumerator = Create().ApplyIdleTimeout(WaitAfterEvent(() => { }), TimeSpan.FromSeconds(5), pipelineCts, userCts.Token).GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        userCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
        Assert.False(pipelineCts.IsCancellationRequested);
    }

    private static CodexSessionHandleEventPipeline Create() => new(Info, null, null, TimeSpan.Zero, NullLogger.Instance, (_, _) => { }, () => true);
    internal static async Task<List<CodexEvent>> Collect(IAsyncEnumerable<CodexEvent> stream)
    {
        var result = new List<CodexEvent>();
        await foreach (var evt in stream) result.Add(evt);
        return result;
    }
    internal static async IAsyncEnumerable<CodexEvent> Events(IEnumerable<CodexEvent> events)
    {
        await Task.CompletedTask;
        foreach (var evt in events) yield return evt;
    }
    private static async IAsyncEnumerable<CodexEvent> WaitAfterEvent(Action disposed, [EnumeratorCancellation] CancellationToken ct = default)
    {
        try { yield return Event(); await Task.Delay(Timeout.Infinite, ct); }
        finally { disposed(); }
    }
    private static async IAsyncEnumerable<CodexEvent> WaitWithoutEvent(TaskCompletionSource started, Action disposed, [EnumeratorCancellation] CancellationToken ct = default)
    {
        try { started.SetResult(); await Task.Delay(Timeout.Infinite, ct); yield break; }
        finally { disposed(); }
    }
}
