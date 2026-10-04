using System.Runtime.CompilerServices;
using System.Text.Json;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.StructuredOutputs;
using JKToolKit.CodexSDK.StructuredOutputs.Internal;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StructuredLiveLifecycleTests
{
    private sealed record Result(string Value);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resume_WaitsForExitAndReadsTheFinalLogSnapshot(bool retry)
    {
        await using var client = new LiveClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var progress = new List<CodexEvent>();
        var options = new CodexSessionOptions(Path.GetTempPath(), "resume");
        var run = retry
            ? client.RunStructuredWithRetryAsync<Result>(SessionId.Parse("session"), options, new CodexStructuredRunProgress { EventReceived = progress.Add }, ct: timeout.Token)
            : client.RunStructuredAsync<Result>(SessionId.Parse("session"), options, ct: timeout.Token);
        await client.Handle.ProgressRead.Task.WaitAsync(timeout.Token);
        Assert.False(run.IsCompleted);
        client.Handle.ExitAllowed.SetResult();
        var result = await run.WaitAsync(timeout.Token);
        Assert.Equal("final", result.Value.Value);
        Assert.True(client.Handle.ReaderStopped);
        Assert.True(client.Handle.Disposed);
        if (retry) Assert.Equal("draft", Assert.IsType<AgentMessageEvent>(Assert.Single(progress)).Text);
        Assert.Equal(1, client.Handle.ExitWaits);
    }

    [Fact]
    public async Task Capture_DoesNotReturnWhileItsProgressReaderIsStillDisposing()
    {
        await using var handle = new LiveHandle { HoldReaderDisposal = true };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = StructuredOutputExecCapture.CaptureExecFinalTextAsync(handle, EventStreamOptions.Default, timeout.Token);
        await handle.ProgressRead.Task.WaitAsync(timeout.Token);
        handle.ExitAllowed.SetResult();
        await handle.ReaderStopping.Task.WaitAsync(timeout.Token);
        try
        {
            // Disposal deliberately remains blocked: returning here would leave a reader using a disposed session.
            var completed = await Task.WhenAny(run, Task.Delay(100, timeout.Token));
            Assert.NotSame(run, completed);
        }
        finally { handle.ReaderDisposalAllowed.TrySetResult(); }
        Assert.Equal("{\"value\":\"final\"}", await run.WaitAsync(timeout.Token));
        Assert.True(handle.ReaderStopped);
    }

    [Fact]
    public async Task HistoricalCapture_CompletesAtEndOfLogEvenWithDefaultFollowOptions()
    {
        await using var handle = new LiveHandle { IsLive = false };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var result = await StructuredOutputExecCapture.CaptureExecFinalTextAsync(handle, EventStreamOptions.Default, timeout.Token);
        Assert.Equal("{\"value\":\"final\"}", result);
        Assert.Equal(0, handle.ExitWaits);
    }

    private sealed class LiveClient : ICodexClient, IAsyncDisposable
    {
        public LiveHandle Handle { get; } = new();
        public Task<ICodexSessionHandle> StartSessionAsync(CodexSessionOptions options, CancellationToken cancellationToken = default) => Task.FromResult<ICodexSessionHandle>(Handle);
        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CodexSessionOptions options, CancellationToken cancellationToken = default) => Task.FromResult<ICodexSessionHandle>(Handle);
        public Task<ICodexSessionHandle> ResumeSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default) => Task.FromResult<ICodexSessionHandle>(new LiveHandle { IsLive = false });
        public Task<ICodexSessionHandle> AttachToLogAsync(string logFilePath, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(SessionFilter? filter = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RateLimits?> GetRateLimitsAsync(bool noCache = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CodexReviewResult> ReviewAsync(CodexReviewOptions options, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() { }
        public ValueTask DisposeAsync() => Handle.DisposeAsync();
    }

    private sealed class LiveHandle : ICodexSessionHandle
    {
        public TaskCompletionSource ProgressRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ExitAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReaderStopping { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReaderDisposalAllowed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HoldReaderDisposal { get; init; }
        public bool ReaderStopped { get; private set; }
        public bool Disposed { get; private set; }
        public int ExitWaits { get; private set; }
        private bool _exited;
        public CodexSessionInfo Info { get; } = new(SessionId.Parse("session"), "missing-test-log", DateTimeOffset.UtcNow, Path.GetTempPath(), null);
        public bool IsLive { get; init; } = true;
        public SessionExitReason ExitReason => SessionExitReason.Unknown;
        public async IAsyncEnumerable<CodexEvent> GetEventsAsync(EventStreamOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (options?.Follow == true)
            {
                try
                {
                    yield return Message("draft");
                    ProgressRead.TrySetResult();
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                finally
                {
                    ReaderStopping.TrySetResult();
                    if (HoldReaderDisposal) await ReaderDisposalAllowed.Task;
                    ReaderStopped = true;
                }
            }
            else yield return Message(!IsLive || _exited ? "{\"value\":\"final\"}" : "draft");
        }
        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            ExitWaits++;
            await ExitAllowed.Task.WaitAsync(cancellationToken);
            _exited = true;
            return 0;
        }
        public Task<int> ExitAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public IDisposable OnExit(Action<int> callback) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposed = true; ReaderDisposalAllowed.TrySetResult(); ExitAllowed.TrySetResult(); return ValueTask.CompletedTask; }
        private static AgentMessageEvent Message(string text) => new() { Type = "agent_message", Timestamp = DateTimeOffset.UtcNow, RawPayload = JsonSerializer.SerializeToElement(new { }), Text = text };
    }
}
