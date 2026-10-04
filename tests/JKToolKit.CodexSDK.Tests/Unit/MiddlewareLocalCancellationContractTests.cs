using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Facade;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class MiddlewareLocalCancellationContractTests
{
    [Fact]
    public async Task LocallyCanceledNext_ReconcilesAcceptedTurnBeforeMiddlewarePropagatesFailure()
    {
        using var middleware = new DelayedCancellationPropagation();
        await using var fixture = new Fixture(middleware: [middleware]);
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Rpc.CompleteOnInterrupt = false;
        var thread = await fixture.Threads.StartAsync();
        var startup = thread.RunStreamedAsync("start");
        try
        {
            await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            middleware.Cancellation.Cancel();
            await middleware.Caught.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(startup.IsCompleted);
            fixture.Rpc.StartResponseGate.SetResult();
            await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, fixture.Rpc.Interrupts);
            Assert.False(startup.IsCompleted, "middleware still owns propagation of its startup failure");
            await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("overlap"));
        }
        finally
        {
            fixture.Rpc.StartResponseGate.TrySetResult();
            middleware.Propagate.TrySetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.Equal(1, fixture.Rpc.Interrupts);
    }

    private sealed class DelayedCancellationPropagation : ICodexTurnMiddleware, IDisposable
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public TaskCompletionSource Caught { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Propagate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CodexTurnHandle> StartAsync(CodexTurnContext context,
            Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct)
        {
            try { return await next(Cancellation.Token); }
            catch (OperationCanceledException)
            {
                Caught.TrySetResult();
                await Propagate.Task;
                throw;
            }
        }

        public void Dispose() => Cancellation.Dispose();
    }
}
