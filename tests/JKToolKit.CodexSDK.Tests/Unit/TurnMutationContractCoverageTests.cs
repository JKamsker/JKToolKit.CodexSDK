using System.Text.Json;
using System.Runtime.CompilerServices;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.Facade.Internal;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class TurnMutationContractCoverageTests
{
    [Fact]
    public async Task TerminalCompletion_ClosesBothLegacyStreamsAndPreservesTheirQueuedEvents()
    {
        await using var handle = NewHandle();
        long typedDrops = 0, rawDrops = 0;
        var note = new ItemCompletedNotification("t", "u", Json("""{"id":"i","type":"agentMessage","text":"answer"}"""), default);
        var raw = new AppServerRpcNotification("item/completed", Json("{}"));
        handle.Observe(note, raw, ref typedDrops, ref rawDrops);
        handle.Complete(new TurnCompletedNotification("t", Json("""{"id":"u","status":"completed"}"""), default));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await handle.Events(timeout.Token).ToListAsync()).Should().ContainSingle().Which.Should().BeSameAs(note);
        (await handle.EventsRaw(timeout.Token).ToListAsync()).Should().ContainSingle().Which.Should().BeSameAs(raw);
    }

    [Fact]
    public async Task TerminalFailure_SettlesBothPublicCompletionTasksWithOriginalError()
    {
        await using var handle = NewHandle();
        var error = new IOException("terminal failure");
        handle.Terminate(error);
        (await Record.ExceptionAsync(() => handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)))).Should().BeSameAs(error);
        (await Record.ExceptionAsync(() => handle.Completion.WaitAsync(TimeSpan.FromSeconds(5)))).Should().BeSameAs(error);
    }

    [Fact]
    public async Task HandleDisposal_IsIdempotentAndSubscriptionDisposalCompletesItsStream()
    {
        var disposed = 0;
        var handle = NewHandle(dispose: () => disposed++);
        var observer = handle.Subscribe();
        await observer.DisposeAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        (await observer.Events(timeout.Token).ToListAsync()).Should().BeEmpty();
        await handle.DisposeAsync(); await handle.DisposeAsync();
        disposed.Should().Be(1);
    }

    [Fact]
    public async Task SubscriptionAndSteerArguments_AreValidatedBeforeDelegates()
    {
        var calls = 0;
        await using var handle = new CodexTurnHandle("t", "u", _ => Task.CompletedTask,
            (_, _) => { calls++; return Task.FromResult("u"); },
            (_, _) => { calls++; return Task.FromResult(new TurnSteerResult { TurnId = "u", Raw = default }); }, () => { }, 8);
        Action subscribe = () => handle.Subscribe(0);
        subscribe.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("capacity");
        var steer = () => handle.SteerAsync(null!);
        var steerRaw = () => handle.SteerRawAsync(null!);
        await steer.Should().ThrowAsync<ArgumentNullException>().WithParameterName("input");
        await steerRaw.Should().ThrowAsync<ArgumentNullException>().WithParameterName("input");
        calls.Should().Be(0);
        await using var unsupported = NewHandle();
        await ((Func<Task>)(() => unsupported.SteerAsync([]))).Should().ThrowAsync<NotSupportedException>().WithMessage("*does not support steering*");
        await ((Func<Task>)(() => unsupported.SteerRawAsync([]))).Should().ThrowAsync<NotSupportedException>().WithMessage("*does not support raw steering*");
    }

    [Fact]
    public void Collector_TerminalItemsAreAuthoritativeAndCanSupplyTheOnlyFinalAnswer()
    {
        var collector = new CodexTurnCollector();
        collector.Observe(new ItemCompletedNotification("t", "u", Json("""{"id":"i","type":"agentMessage","text":"draft"}"""), default));
        var result = collector.Complete("t", "u", new TurnCompletedNotification("t", Json("""{"id":"u","status":"completed","items":[{"id":"i","type":"agentMessage","phase":"final_answer","text":"final"},{"id":"j","type":"future"}]}"""), default));
        result.FinalResponse.Should().Be("final"); result.Items.Should().HaveCount(2);
        var empty = new CodexTurnCollector().Complete("t", "u", new TurnCompletedNotification("t", Json("""{"id":"u","items":[{"type":"agentMessage","text":"terminal only"}]}"""), default));
        empty.FinalResponse.Should().Be("terminal only");
    }

    [Fact]
    public async Task FacadeNullInputs_AreRejectedBeforeMiddleware()
    {
        var middleware = new CountingMiddleware();
        await using var fixture = new Fixture(middleware: [middleware]);
        var thread = await fixture.Threads.StartAsync();
        await ((Func<Task>)(() => thread.RunAsync((CodexInput)null!))).Should().ThrowAsync<ArgumentNullException>().WithParameterName("input");
        await ((Func<Task>)(() => thread.RunStreamedAsync((CodexInput)null!))).Should().ThrowAsync<ArgumentNullException>().WithParameterName("input");
        await ((Func<Task>)(() => thread.RunStreamedAsync((TurnStartOptions)null!))).Should().ThrowAsync<ArgumentNullException>().WithParameterName("options");
        middleware.Calls.Should().Be(0); fixture.Rpc.TurnStartCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Facade_RejectsForeignMiddlewareHandleAndCleansItsOwnedStartup(bool callNext)
    {
        await using var foreign = NewHandle();
        await using var fixture = new Fixture(middleware: [new ReplacingMiddleware(foreign, callNext)]);
        var thread = await fixture.Threads.StartAsync();
        await ((Func<Task>)(() => thread.RunStreamedAsync("start"))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*handle from next*");
        fixture.Rpc.TurnStartCalls.Should().Be(callNext ? 1 : 0);
        if (callNext) await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PrecanceledFacadeRun_PreservesCancellationInsteadOfReportingOverlap()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var active = await thread.RunStreamedAsync("active");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => thread.RunStreamedAsync("canceled", cancellation.Token));
        error.CancellationToken.Should().Be(cancellation.Token);
        fixture.Rpc.TurnStartCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbandonStartedTurn_IsIdempotentAndDisposesObservationEvenWhenInterruptFails(bool failInterrupt)
    {
        var state = new CodexThreadExecutionState();
        var owned = state.Reserve();
        var interrupts = 0;
        await using var handle = await owned.StartAsync(() => Task.FromResult(NewHandle(interrupt: _ =>
        {
            interrupts++;
            return failInterrupt ? Task.FromException(new IOException("interrupt failed")) : Task.CompletedTask;
        })), default);
        owned.Abandon(); owned.Abandon();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        interrupts.Should().Be(1);
        Action overlap = () => state.Reserve();
        overlap.Should().Throw<InvalidOperationException>().WithMessage("*active or starting turn*");
        handle.Terminate();
    }

    [Fact]
    public async Task PrecanceledOwnedStartup_DoesNotInvokeDelegateOrReplaceOverlapWithAnotherError()
    {
        var state = new CodexThreadExecutionState();
        var owned = state.Reserve();
        await using var handle = await owned.StartAsync(() => Task.FromResult(NewHandle()), default);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var calls = 0;
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owned.StartAsync(() => { calls++; return Task.FromResult(NewHandle()); }, cancellation.Token));
        error.CancellationToken.Should().Be(cancellation.Token); calls.Should().Be(0);
        handle.Terminate();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedOrUnsubscribedObservers_AreCollectibleWhileHandleStaysAlive(bool disposeObserver)
    {
        await using var handle = NewHandle();
        var observer = await CreateObserverReference(handle, disposeObserver);
        if (!disposeObserver) handle.Complete(new TurnCompletedNotification("t", Json("""{"id":"u","status":"completed"}"""), default));
        for (var attempt = 0; attempt < 10 && observer.IsAlive; attempt++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            await Task.Delay(10);
        }
        observer.IsAlive.Should().BeFalse("completed and disposed observers must not stay rooted by a retained handle");
        GC.KeepAlive(handle);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> CreateObserverReference(CodexTurnHandle handle, bool disposeObserver)
    {
        var observer = handle.Subscribe();
        if (disposeObserver) await observer.DisposeAsync();
        return new WeakReference(observer);
    }

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static CodexTurnHandle NewHandle(Action? dispose = null, Func<CancellationToken, Task>? interrupt = null) => new("t", "u", interrupt ?? (_ => Task.CompletedTask), null, null, dispose ?? (() => { }), 8);
    private sealed class CountingMiddleware : ICodexTurnMiddleware
    {
        public int Calls { get; private set; }
        public Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct) { Calls++; return next(ct); }
    }
    private sealed class ReplacingMiddleware(CodexTurnHandle foreign, bool callNext) : ICodexTurnMiddleware
    {
        public async Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct) { if (callNext) await next(ct); return foreign; }
    }
}
