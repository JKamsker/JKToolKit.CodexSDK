using System.Diagnostics;
using System.Runtime.CompilerServices;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Notifications;
using JKToolKit.CodexSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ExecSessionHandleLifecycleBehaviorTests
{
    [Fact]
    public async Task NonLiveHandle_RejectsProcessOperations_DeletesTemporaryFilesOnce()
    {
        var temp = Path.GetTempFileName();
        var stream = new StreamStub();
        var handle = Create(stream, tempFiles: [temp, temp + ".absent"]);
        Assert.False(handle.IsLive);
        Assert.Equal(SessionExitReason.Unknown, handle.ExitReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.ExitAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.WaitForExitAsync(default));
        Assert.Throws<InvalidOperationException>(() => handle.OnExit(_ => { }));
        Assert.Throws<ArgumentNullException>(() => handle.OnExit(null!));
        var events = await ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, default));
        Assert.Single(events);
        Assert.False(stream.Options!.Follow);
        await handle.DisposeAsync();
        Assert.False(File.Exists(temp));
        await handle.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => handle.ExitAsync(default));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => handle.WaitForExitAsync(default));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, default)));
        Assert.Throws<ObjectDisposedException>(() => handle.OnExit(_ => { }));
    }

    [Fact]
    public async Task ExitAsync_NotifiesSubscribersOnce_AndLateSubscribersReceiveCachedCode()
    {
        using var child = new Child();
        var launcher = new MockCodexProcessLauncher { TerminateExitCode = 23 };
        await using var handle = Create(new(), child.Process, launcher);
        Assert.True(handle.IsLive);
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var removedCalls = 0;
        var removed = handle.OnExit(_ => Interlocked.Increment(ref removedCalls));
        removed.Dispose();
        removed.Dispose();
        using var active = handle.OnExit(code => received.TrySetResult(code));
        using var throwing = handle.OnExit(_ => throw new InvalidOperationException("consumer failure"));
        Assert.Equal(23, await handle.ExitAsync(default));
        Assert.Equal(23, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(SessionExitReason.Custom, handle.ExitReason);
        var late = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var lateSubscription = handle.OnExit(code => late.SetResult(code));
        Assert.Equal(23, await late.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, removedCalls);
        using var lateThrowing = handle.OnExit(_ => throw new InvalidOperationException("late consumer failure"));
        await child.ExitAsync(7);
        Assert.Equal(7, await handle.WaitForExitAsync(default));
        Assert.Equal(7, await handle.ExitAsync(default));
        Assert.Equal(SessionExitReason.Custom, handle.ExitReason);
        Assert.Single(launcher.CapturedTerminations);
    }

    [Fact]
    public async Task LiveHandleWithoutLauncher_CannotExit_ButCanWaitForNaturalExit()
    {
        using var child = new Child();
        await using var handle = Create(new(), child.Process);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.ExitAsync(default));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.WaitForExitAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.ExitAsync(canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, canceled.Token)));
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callback = handle.OnExit(code => received.SetResult(code));
        await child.ExitAsync(9);
        Assert.Equal(9, await handle.WaitForExitAsync(default));
        Assert.Equal(9, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(SessionExitReason.Success, handle.ExitReason);
    }

    [Fact]
    public async Task LiveFollow_StopsAfterExit_AndDisposesTailEnumerator()
    {
        using var child = new Child();
        var stream = new StreamStub { FollowIndefinitely = true };
        await using var handle = Create(stream, child.Process);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var events = handle.GetEventsAsync(null, deadline.Token).GetAsyncEnumerator();
        Assert.True(await events.MoveNextAsync());
        Assert.True(stream.Options!.Follow);
        await child.ExitAsync(0);
        Assert.False(await events.MoveNextAsync());
        Assert.True(stream.Disposed);
    }

    [Fact]
    public async Task LiveIdleTimeout_CompletesNormally_AndReportsTimeout()
    {
        using var child = new Child();
        var launcher = new MockCodexProcessLauncher { TerminateExitCode = 31 };
        var stream = new StreamStub { FollowIndefinitely = true };
        await using var handle = new CodexSessionHandle(ExecEventPipelineBehaviorTests.Info, stream, stream,
            child.Process, launcher, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(120), NullLogger<CodexSessionHandle>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var events = await ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, deadline.Token));
        Assert.Single(events);
        Assert.True(stream.Disposed);
        Assert.Equal(SessionExitReason.Timeout, handle.ExitReason);
        Assert.Single(launcher.CapturedTerminations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_AttemptsTerminationAndDeletesFiles_EvenWhenTerminationFails(bool fails)
    {
        using var child = new Child();
        var launcher = new MockCodexProcessLauncher { SimulateTerminateFailure = fails };
        var temp = Path.GetTempFileName();
        var handle = Create(new(), child.Process, launcher, [temp]);
        await handle.DisposeAsync();
        Assert.Single(launcher.CapturedTerminations);
        Assert.False(handle.IsLive);
        Assert.False(File.Exists(temp));
    }

    [Fact]
    public async Task ResumeBootstrap_ObservesGrowthAndProcessExit_AndDistinguishesCancellationFromTimeout()
    {
        var temp = Path.GetTempFileName();
        try
        {
            using var child = new Child();
            Assert.Equal(0, CodexResumeBootstrapMonitor.TryGetFileLength(temp + ".absent"));
            await File.WriteAllTextAsync(temp, "first");
            Assert.Equal(5, CodexResumeBootstrapMonitor.TryGetFileLength(temp));
            await CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(child.Process, temp, 0, TimeSpan.FromSeconds(1), NullLogger.Instance, default);
            var waiting = CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(child.Process, temp, 5, TimeSpan.FromSeconds(5), NullLogger.Instance, default);
            await File.AppendAllTextAsync(temp, "more");
            await waiting;
            await Assert.ThrowsAsync<TimeoutException>(() => CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(child.Process, temp, 9, TimeSpan.FromMilliseconds(40), NullLogger.Instance, default));
            await Assert.ThrowsAsync<TimeoutException>(() => CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(child.Process, temp, 99, TimeSpan.FromMilliseconds(40), NullLogger.Instance, default));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(child.Process, temp, 9, TimeSpan.FromSeconds(5), NullLogger.Instance, canceled.Token));
            await child.ExitAsync(4);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(child.Process, temp, 9, TimeSpan.FromSeconds(1), NullLogger.Instance, default));
            Assert.Contains("code 4", failure.Message);
        }
        finally { File.Delete(temp); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RealTermination_PreservesRequestedExitReason_WhenExitedEventWins(int terminationKind)
    {
        var idle = terminationKind == 1;
        using var child = new Child();
        var launcher = new RealTerminatingLauncher();
        var stream = new StreamStub { FollowIndefinitely = idle };
        await using var handle = new CodexSessionHandle(ExecEventPipelineBehaviorTests.Info, stream, stream, child.Process, launcher,
            TimeSpan.FromSeconds(2), idle ? TimeSpan.FromMilliseconds(120) : null, NullLogger<CodexSessionHandle>.Instance);
        if (idle)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            Assert.Single(await ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, deadline.Token)));
        }
        else if (terminationKind == 2) await handle.DisposeAsync();
        else await handle.ExitAsync(default);
        Assert.Equal(idle ? SessionExitReason.Timeout : SessionExitReason.Custom, handle.ExitReason);
    }

    [Fact]
    public async Task FailedCustomTermination_DoesNotMislabelSubsequentNaturalExit()
    {
        using var child = new Child();
        var launcher = new MockCodexProcessLauncher { SimulateTerminateFailure = true };
        await using var handle = Create(new(), child.Process, launcher);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handle.ExitAsync(default));
        await child.ExitAsync(0);
        Assert.Equal(0, await handle.WaitForExitAsync(default));
        Assert.Equal(SessionExitReason.Success, handle.ExitReason);
    }

    [Fact]
    public async Task ConcurrentIdleReaders_TerminateTheSharedProcessOnlyOnce()
    {
        using var child = new Child();
        var launcher = new CountingLauncher();
        var stream = new StreamStub { FollowIndefinitely = true };
        await using var handle = new CodexSessionHandle(ExecEventPipelineBehaviorTests.Info, stream, stream, child.Process, launcher,
            TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(120), NullLogger<CodexSessionHandle>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var readers = await Task.WhenAll(
            ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, deadline.Token)),
            ExecEventPipelineBehaviorTests.Collect(handle.GetEventsAsync(null, deadline.Token)));
        Assert.All(readers, events => Assert.Single(events));
        Assert.Equal(1, launcher.TerminationCount);
        Assert.Equal(SessionExitReason.Timeout, handle.ExitReason);
    }

    [Theory]
    [InlineData("info")]
    [InlineData("tailer")]
    [InlineData("parser")]
    [InlineData("logger")]
    public void Handle_RejectsMissingDependencies(string dependency)
    {
        var stream = new StreamStub();
        var failure = Assert.Throws<ArgumentNullException>(() => new CodexSessionHandle(
            dependency == "info" ? null! : ExecEventPipelineBehaviorTests.Info,
            dependency == "tailer" ? null! : stream, dependency == "parser" ? null! : stream,
            null, null, TimeSpan.FromSeconds(1), null, dependency == "logger" ? null! : NullLogger<CodexSessionHandle>.Instance));
        Assert.Equal(dependency, failure.ParamName);
    }

    [Theory]
    [InlineData("process")]
    [InlineData("logPath")]
    [InlineData("logger")]
    public async Task Bootstrap_RejectsMissingDependencies(string dependency)
    {
        using var process = new Process();
        var failure = await Assert.ThrowsAsync<ArgumentNullException>(() => CodexResumeBootstrapMonitor.WaitForLogAdvanceAsync(
            dependency == "process" ? null! : process, dependency == "logPath" ? null! : "log", 0,
            TimeSpan.FromSeconds(1), dependency == "logger" ? null! : NullLogger.Instance, default));
        Assert.Equal(dependency, failure.ParamName);
    }

    private sealed class CountingLauncher : ICodexProcessLauncher
    {
        private int _terminationCount;
        public int TerminationCount => Volatile.Read(ref _terminationCount);
        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<Process> ResumeSessionAsync(JKToolKit.CodexSDK.Exec.Protocol.SessionId id, CodexSessionOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<Process> StartReviewAsync(CodexReviewOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> TerminateProcessAsync(Process process, TimeSpan timeout, CancellationToken ct)
        {
            Interlocked.Increment(ref _terminationCount);
            return Task.FromResult(0);
        }
    }

    private sealed class RealTerminatingLauncher : ICodexProcessLauncher
    {
        public Task<Process> StartSessionAsync(CodexSessionOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<Process> ResumeSessionAsync(JKToolKit.CodexSDK.Exec.Protocol.SessionId id, CodexSessionOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public Task<Process> StartReviewAsync(CodexReviewOptions options, CodexClientOptions client, CancellationToken ct) => throw new NotSupportedException();
        public async Task<int> TerminateProcessAsync(Process process, TimeSpan timeout, CancellationToken ct)
        {
            var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnExited(object? sender, EventArgs args) => exited.TrySetResult();
            process.Exited += OnExited;
            try
            {
                process.Kill(true);
                await exited.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
                return process.ExitCode;
            }
            finally { process.Exited -= OnExited; }
        }
    }

    private static CodexSessionHandle Create(StreamStub stream, Process? process = null, ICodexProcessLauncher? launcher = null, IReadOnlyList<string>? tempFiles = null) =>
        new(ExecEventPipelineBehaviorTests.Info, stream, stream, process, launcher, TimeSpan.FromSeconds(2), null, NullLogger<CodexSessionHandle>.Instance, tempFiles);

    private sealed class StreamStub : IJsonlTailer, IJsonlEventParser
    {
        public EventStreamOptions? Options;
        public bool FollowIndefinitely;
        public bool Disposed;
        public async IAsyncEnumerable<string> TailAsync(string path, EventStreamOptions options, [EnumeratorCancellation] CancellationToken ct)
        {
            Options = options;
            try { yield return "event"; if (FollowIndefinitely) await Task.Delay(Timeout.Infinite, ct); }
            finally { Disposed = true; }
        }
        public async IAsyncEnumerable<CodexEvent> ParseAsync(IAsyncEnumerable<string> lines, [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var _ in lines.WithCancellation(ct)) yield return ExecEventPipelineBehaviorTests.Event();
        }
    }

    private sealed class Child : IDisposable
    {
        public Process Process { get; }
        private readonly int _id;
        public Child()
        {
            var start = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "/bin/sh",
                UseShellExecute = false, RedirectStandardInput = true
            };
            start.ArgumentList.Add(OperatingSystem.IsWindows() ? "-Command" : "-c");
            start.ArgumentList.Add(OperatingSystem.IsWindows() ? "$code = [Console]::ReadLine(); exit ([int]$code)" : "read code; exit \"$code\"");
            Process = Process.Start(start)!;
            _id = Process.Id;
        }
        public async Task ExitAsync(int code)
        {
            await Process.StandardInput.WriteLineAsync(code.ToString());
            await Process.StandardInput.FlushAsync();
            await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        public void Dispose()
        {
            // The SDK owns the Process wrapper, so retain the id for cleanup after it disposes that wrapper.
            try { using var child = Process.GetProcessById(_id); if (!child.HasExited) { child.Kill(true); child.WaitForExit(5000); } }
            catch (ArgumentException) { }
            Process.Dispose();
        }
    }
}
