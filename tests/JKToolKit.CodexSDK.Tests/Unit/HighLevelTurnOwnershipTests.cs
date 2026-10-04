using System.Runtime.CompilerServices;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Facade;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class HighLevelTurnOwnershipTests
{
    [Fact]
    public async Task MiddlewareCancellation_CancelsItsWait_AndReconcilesAcceptedTurn()
    {
        using var middleware = new CancelableNext();
        await using var fixture = new Fixture(middleware: [middleware]);
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Rpc.CompleteOnInterrupt = false;
        var thread = await fixture.Threads.StartAsync();
        var run = thread.RunStreamedAsync("start");
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await middleware.Cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        fixture.Rpc.LastStartToken.IsCancellationRequested.Should().BeFalse();
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("while starting"));
        fixture.Rpc.StartResponseGate.SetResult();
        await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("while stopping"));
        await fixture.Rpc.Complete("interrupted");
        middleware.UseCancellation = false;
        await using var next = await thread.RunStreamedAsync("next");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Fact]
    public async Task StartupResponseAfterSdkDisposal_TerminatesLateHandle()
    {
        await using var fixture = new Fixture();
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = await fixture.Threads.StartAsync();
        var startup = thread.RunStreamedAsync("start");
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Threads.DisposeAsync();
        fixture.Rpc.StartResponseGate.SetResult();
        await using var handle = await startup.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        handle.ServerCompletion.IsCompleted.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledStart_ReconcilesDelayedResponse_AndKeepsGuardUntilTerminal(bool failInterrupt)
    {
        await using var fixture = new Fixture();
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Rpc.CompleteOnInterrupt = false;
        fixture.Rpc.FailInterrupt = failInterrupt;
        var thread = await fixture.Threads.StartAsync();
        using var ct = new CancellationTokenSource();
        var run = thread.RunAsync("start", ct.Token);
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await ct.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        fixture.Rpc.LastStartToken.IsCancellationRequested.Should().BeFalse();
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("while starting"));

        fixture.Rpc.StartResponseGate.SetResult();
        await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("while stopping"));
        await fixture.Rpc.Complete("interrupted");
        await using var next = await thread.RunStreamedAsync("after terminal");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Fact]
    public async Task MiddlewareFailureAfterStart_InterruptsAndRetainsOwnership()
    {
        var middleware = new ThrowAfterNext();
        await using var fixture = new Fixture(middleware: [middleware]);
        fixture.Rpc.CompleteOnInterrupt = false;
        var thread = await fixture.Threads.StartAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunAsync("start"));
        failure.Message.Should().Be("middleware failed");
        await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Rpc.Interrupts.Should().Be(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("overlap"));
        fixture.Rpc.TurnStartCalls.Should().Be(1);
        await fixture.Rpc.Complete("interrupted");
        middleware.Fail = false;
        await using var next = await thread.RunStreamedAsync("after terminal");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Fact]
    public async Task CancellationBeforeMiddlewareCallsNext_PreventsLateDispatch()
    {
        var middleware = new DelayedBeforeNext();
        await using var fixture = new Fixture(middleware: [middleware]);
        var thread = await fixture.Threads.StartAsync();
        using var ct = new CancellationTokenSource();
        var run = thread.RunStreamedAsync("start", ct.Token);
        await middleware.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await ct.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        middleware.Proceed.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => middleware.LateDispatch.Task);
        fixture.Rpc.TurnStartCalls.Should().Be(0);
    }

    [Fact]
    public async Task DisposingStreamingHandle_DoesNotReleaseServerTurnGuard()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        var handle = await thread.RunStreamedAsync("start");
        await handle.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("overlap"));
        await fixture.Rpc.Complete("completed");
        await using var next = await thread.RunStreamedAsync("next");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Fact]
    public async Task CompletedHandles_AreCollectibleWhileSdkAndSessionStayAlive()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        var weak = await RunAndReleaseAsync(thread, fixture);
        for (var attempt = 0; attempt < 50 && weak.IsAlive; attempt++)
        {
            await Task.Delay(10);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        weak.IsAlive.Should().BeFalse("the shared SDK must not retain completed turn payloads");
        GC.KeepAlive(thread);
        GC.KeepAlive(fixture);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> RunAndReleaseAsync(CodexThreadSession thread, Fixture fixture)
    {
        await using var handle = await thread.RunStreamedAsync("start");
        await fixture.Rpc.Item("i", "final_answer", new string('x', 100_000));
        await fixture.Rpc.Complete("completed");
        await handle.RunAsync();
        return new WeakReference(handle);
    }

    private sealed class ThrowAfterNext : ICodexTurnMiddleware
    {
        public bool Fail { get; set; } = true;
        public async Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct)
        {
            var handle = await next(ct);
            if (Fail) throw new InvalidOperationException("middleware failed");
            return handle;
        }
    }

    private sealed class CancelableNext : ICodexTurnMiddleware, IDisposable
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public bool UseCancellation { get; set; } = true;
        public Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct) =>
            next(UseCancellation ? Cancellation.Token : ct);
        public void Dispose() => Cancellation.Dispose();
    }

    private sealed class DelayedBeforeNext : ICodexTurnMiddleware
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Proceed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource LateDispatch { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct)
        {
            Entered.SetResult();
            await Proceed.Task;
            try
            {
                var handle = await next(CancellationToken.None);
                LateDispatch.SetResult();
                return handle;
            }
            catch (Exception ex) { LateDispatch.SetException(ex); throw; }
        }
    }
}
