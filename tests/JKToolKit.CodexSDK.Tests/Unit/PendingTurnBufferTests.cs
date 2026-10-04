using System.Reflection;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class PendingTurnBufferTests
{
    [Fact]
    public async Task PendingDetachedReview_PreservesServerChosenThreadNotifications()
    {
        await using var fixture = new Fixture();
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = await fixture.StartAsync();
        var startup = client.StartReviewAsync(new()
        {
            ThreadId = "t", Delivery = ReviewDelivery.Detached,
            Target = new ReviewTarget.UncommittedChanges()
        });
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Rpc.Emit("item/completed", """{"threadId":"review-t","turnId":"review-u","item":{"id":"i","type":"agentMessage","text":"review preserved"}}""");
        await fixture.Rpc.Emit("turn/completed", """{"threadId":"review-t","turn":{"id":"review-u","status":"completed"}}""");
        PruneAtFutureTime(client);
        fixture.Rpc.StartResponseGate.SetResult();
        var review = await startup.WaitAsync(TimeSpan.FromSeconds(5));
        await using var handle = review.Turn;
        var result = await handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
        result.FinalResponse.Should().Be("review preserved");
        result.IsPartial.Should().BeFalse();
        await client.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingStart_PreservesEarlyItemsAndTerminalBeyondTtl(bool missingThreadId)
    {
        await using var fixture = new Fixture();
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = await fixture.StartAsync();
        var thread = await fixture.Threads.StartAsync();
        var startup = thread.RunStreamedAsync("start");
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Rpc.Emit("item/completed", missingThreadId
            ? """{"turnId":"u","item":{"id":"i","type":"agentMessage","phase":"final_answer","text":"preserved"}}"""
            : """{"threadId":"t","turnId":"u","item":{"id":"i","type":"agentMessage","phase":"final_answer","text":"preserved"}}""");
        await fixture.Rpc.Complete("completed");
        await fixture.Rpc.Emit("turn/started", """{"threadId":"unrelated","turn":{"id":"unrelated-turn"}}""");

        PruneAtFutureTime(client);
        client.NotificationDropStats.BufferedTurnNotificationsDroppedTtl.Should().Be(1,
            "unrelated buffers should still expire while this startup is pending");
        fixture.Rpc.StartResponseGate.SetResult();
        await using var handle = await startup.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
        result.FinalResponse.Should().Be("preserved");
        result.IsPartial.Should().BeFalse();

        await fixture.Rpc.Emit("turn/started", """{"threadId":"t","turn":{"id":"orphan"}}""");
        PruneAtFutureTime(client);
        client.NotificationDropStats.BufferedTurnNotificationsDroppedTtl.Should().Be(2,
            "the startup reservation should be released after handle registration");
    }

    private static void PruneAtFutureTime(CodexAppServerClient client)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var core = typeof(CodexAppServerClient).GetField("_core", flags)!.GetValue(client)!;
        // Advance the pruning cutoff without wall-clock sleeps or changing production TTL policy.
        core.GetType().GetMethod("PruneStaleTurnBuffers", flags)!
            .Invoke(core, [DateTimeOffset.UtcNow.AddMinutes(1)]);
    }
}
