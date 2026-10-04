using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Facade.Internal;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class TurnLifecycleCoverageTests
{
    [Fact]
    public async Task AbandonedReservation_RejectsLateMiddlewareContinuationAndAllowsNewReservation()
    {
        var state = new CodexThreadExecutionState();
        var reservation = state.Reserve();
        reservation.Abandon();
        var calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reservation.StartAsync(() =>
        {
            calls++;
            return Task.FromResult(NewHandle());
        }, default));
        calls.Should().Be(0);
        var next = state.Reserve();
        next.Should().NotBeSameAs(reservation);
        next.Abandon();
    }

    [Fact]
    public async Task OwnedStartup_RejectsDuplicateNextAndForeignReturnedHandles()
    {
        var state = new CodexThreadExecutionState();
        var reservation = state.Reserve();
        await using var foreign = NewHandle();
        Action premature = () => reservation.ValidateReturnedHandle(foreign);
        premature.Should().Throw<InvalidOperationException>().WithMessage("*handle from next*");
        var calls = 0;
        await using var handle = await reservation.StartAsync(() => { calls++; return Task.FromResult(NewHandle()); }, default);
        reservation.ValidateReturnedHandle(handle);
        Action replaced = () => reservation.ValidateReturnedHandle(foreign);
        replaced.Should().Throw<InvalidOperationException>().WithMessage("*handle from next*");
        var duplicate = () => reservation.StartAsync(() => { calls++; return Task.FromResult(NewHandle()); }, default);
        await duplicate.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exactly once*");
        calls.Should().Be(1);
        handle.Complete(new TurnCompletedNotification("t", JsonSerializer.SerializeToElement(new { id = "u", status = "completed" }), default));
        state.Reserve().Abandon();
    }

    [Fact]
    public async Task FailedInterruptAfterCollectionCancellation_PreservesCallerCancellationAndOwnership()
    {
        await using var fixture = new Fixture();
        fixture.Rpc.FailInterrupt = true;
        fixture.Rpc.CompleteOnInterrupt = false;
        var thread = await fixture.Threads.StartAsync();
        using var cancellation = new CancellationTokenSource();
        var run = thread.RunAsync("hello", cancellation.Token);
        fixture.Rpc.Started.Task.IsCompleted.Should().BeTrue("the in-memory turn/start response completes synchronously");
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
        error.CancellationToken.Should().Be(cancellation.Token);
        fixture.Rpc.Interrupts.Should().Be(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("overlap"));
        await fixture.Rpc.Complete("interrupted");
        fixture.Rpc.FailInterrupt = false;
        await using var next = await thread.RunStreamedAsync("after terminal");
        fixture.Rpc.TurnStartCalls.Should().Be(2);
    }

    [Fact]
    public async Task RawStartResponse_PreservesFutureFieldsAndRemainsReadableAfterCompletion()
    {
        var raw = JsonSerializer.SerializeToElement(new { turn = new { id = "u", status = "inProgress", future = 17 } });
        await using var handle = NewHandle(raw);
        handle.RawStartResponse!.Value.GetProperty("turn").GetProperty("future").GetInt32().Should().Be(17);
        handle.Complete(new TurnCompletedNotification("t", JsonSerializer.SerializeToElement(new { id = "u", status = "completed" }), default));
        await handle.RunAsync();
        handle.RawStartResponse.Value.GetProperty("turn").GetProperty("status").GetString().Should().Be("inProgress");
    }

    [Fact]
    public void Collector_ItemsWithoutIdentifiersRemainDistinctAndExplicitFinalAnswerWins()
    {
        var collector = new CodexTurnCollector();
        collector.Observe(new ItemCompletedNotification("t", "u", JsonSerializer.SerializeToElement(new[] { 1 }), default));
        foreach (var item in new[]
        {
            """{"type":"agentMessage","text":"first"}""",
            """{"id":null,"type":"agentMessage","text":"second"}""",
            """{"id":"final","type":"agentMessage","phase":"final_answer","text":"explicit"}""",
            """{"id":"legacy","type":"agentMessage","text":"later legacy"}"""
        }) collector.Observe(new ItemCompletedNotification("t", "u", JsonSerializer.Deserialize<JsonElement>(item), default));
        var result = collector.Complete("t", "u", new TurnCompletedNotification("t", JsonSerializer.SerializeToElement(new { id = "u", status = "completed" }), default));
        result.Items.Should().HaveCount(4); result.FinalResponse.Should().Be("explicit");
    }

    private static CodexTurnHandle NewHandle(JsonElement? raw = null) => new("t", "u", _ => Task.CompletedTask, null, null, () => { }, 8, raw);
}
