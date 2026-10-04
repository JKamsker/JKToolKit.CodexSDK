using System.Reflection;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Facade;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

// Behavioral gaps identified by the first Stryker campaign; keep these independent of mutant IDs.
public sealed class StateMutationRegressionTests
{
    [Theory]
    [InlineData("final_answer")]
    [InlineData(null)]
    public async Task TerminalOnlyItems_ReplaceDraftAndPreserveOrder(string? phase)
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var handle = await thread.RunStreamedAsync("start");
        await fixture.Rpc.Item("same", "commentary", "draft");
        await fixture.Rpc.Emit("turn/completed", JsonSerializer.Serialize(new
        {
            threadId = "t", turn = new { id = "u", status = "completed", items = new[]
            {
                new { id = "same", type = "agentMessage", text = "terminal answer", phase },
                new { id = "later", type = "agentMessage", text = "commentary", phase = (string?)"commentary" }
            } }
        }));
        var result = await handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("terminal answer", result.FinalResponse);
        Assert.Equal(new[] { "same", "later" }, result.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"id\":\"i\",\"type\":\"agentMessage\",\"phase\":\"commentary\",\"text\":\"thinking\"}]")]
    public async Task NoFinalMessage_ReturnsEmptyResponse(string items)
    {
        await using var handle = TurnStateFuzzTests.NewHandle();
        using var document = JsonDocument.Parse($"{{\"id\":\"u\",\"status\":\"completed\",\"items\":{items}}}");
        handle.Complete(new("t", document.RootElement, default));
        Assert.Equal(string.Empty, (await handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5))).FinalResponse);
    }

    [Fact]
    public void Usage_PreservesEveryCounterAndFutureFieldsAfterSourceDisposal()
    {
        CodexTurnUsage usage;
        using (var doc = JsonDocument.Parse("""{"last":{"inputTokens":1,"cachedInputTokens":2,"cacheWriteInputTokens":3,"outputTokens":4,"reasoningOutputTokens":5,"totalTokens":6},"total":{"inputTokens":11,"cachedInputTokens":12,"cacheWriteInputTokens":13,"outputTokens":14,"reasoningOutputTokens":15,"totalTokens":16},"modelContextWindow":5000000000,"future":"preserved"}"""))
            usage = CodexTurnUsage.Parse(doc.RootElement);
        Assert.Equal(new long?[] { 1, 2, 3, 4, 5, 6 }, Counters(usage.Last!));
        Assert.Equal(new long?[] { 11, 12, 13, 14, 15, 16 }, Counters(usage.Total!));
        Assert.Equal(5000000000, usage.ModelContextWindow);
        Assert.Equal("preserved", usage.Raw.GetProperty("future").GetString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("false")]
    [InlineData("[]")]
    [InlineData("1")]
    [InlineData("\"text\"")]
    public void NonObjectUsageBreakdowns_RemainAbsent(string value)
    {
        using var doc = JsonDocument.Parse($"{{\"last\":{value},\"total\":{value}}}");
        var usage = CodexTurnUsage.Parse(doc.RootElement);
        Assert.Null(usage.Last);
        Assert.Null(usage.Total);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalOutcome_SettlesLegacyCompletionAndBothQueues(bool failed)
    {
        await using var handle = TurnStateFuzzTests.NewHandle();
        var failure = new IOException("disconnect");
        if (failed) handle.Terminate(failure); else handle.Complete(TurnStateFuzzTests.Terminal());
        var completionError = await Record.ExceptionAsync(() => handle.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Same(failed ? failure : null, completionError);
        Assert.Same(failed ? failure : null, await Record.ExceptionAsync(() => Drain(handle.Events())));
        Assert.Same(failed ? failure : null, await Record.ExceptionAsync(() => Drain(handle.EventsRaw())));
    }

    [Fact]
    public async Task DisposingRawHandle_ReleasesRegistrationExactlyOnce()
    {
        var removed = 0;
        var handle = new CodexTurnHandle("t", "u", _ => Task.CompletedTask, null, null, () => removed++, 8);
        await handle.DisposeAsync();
        await handle.DisposeAsync();
        Assert.Equal(1, removed);
        Assert.True(handle.ServerCompletion.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task EarlyQueueOverflow_PreservesNewestItemsAndExactDropCount()
    {
        await using var fixture = new Fixture(capacity: 3);
        var client = await fixture.StartAsync();
        fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = await fixture.Threads.StartAsync();
        var startup = thread.RunStreamedAsync("start");
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (var i = 0; i < 7; i++) await fixture.Rpc.Item($"i{i}", "final_answer", $"answer{i}");
        await fixture.Rpc.Complete("completed");
        fixture.Rpc.StartResponseGate.SetResult();
        await using var handle = await startup.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "i5", "i6" }, result.Items.Select(item => item.Id));
        Assert.Equal("answer6", result.FinalResponse);
        Assert.True(result.IsPartial);
        Assert.Equal(5, client.NotificationDropStats.BufferedTurnNotificationsDroppedCapacity);
    }

    [Fact]
    public async Task TtlPruning_RemovesEveryExpiredOrphanExactlyOnce()
    {
        await using var fixture = new Fixture();
        await using var client = await fixture.StartAsync();
        for (var i = 0; i < 4; i++)
            await fixture.Rpc.Emit("fuzz/event", JsonSerializer.Serialize(new { threadId = "orphan", turnId = $"orphan{i}" }));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var core = typeof(CodexAppServerClient).GetField("_core", flags)!.GetValue(client)!;
        var prune = core.GetType().GetMethod("PruneStaleTurnBuffers", flags)!;
        prune.Invoke(core, [DateTimeOffset.UtcNow.AddMinutes(1)]);
        Assert.Equal(4, client.NotificationDropStats.BufferedTurnNotificationsDroppedTtl);
        prune.Invoke(core, [DateTimeOffset.UtcNow.AddMinutes(1)]);
        Assert.Equal(4, client.NotificationDropStats.BufferedTurnNotificationsDroppedTtl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MiddlewareCannotSubstituteAnUnownedHandle(bool callNext)
    {
        await using var replacement = TurnStateFuzzTests.NewHandle();
        await using var fixture = new Fixture(middleware: [new SubstituteHandle(replacement, callNext)]);
        var thread = await fixture.Threads.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => thread.RunStreamedAsync("start").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(callNext ? 1 : 0, fixture.Rpc.TurnStartCalls);
        if (callNext) await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static long?[] Counters(CodexTokenUsage usage) => [usage.InputTokens, usage.CachedInputTokens,
        usage.CacheWriteInputTokens, usage.OutputTokens, usage.ReasoningOutputTokens, usage.TotalTokens];

    private static async Task Drain<T>(IAsyncEnumerable<T> stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var _ in stream.WithCancellation(timeout.Token)) { }
    }

    private sealed class SubstituteHandle(CodexTurnHandle replacement, bool callNext) : ICodexTurnMiddleware
    {
        public async Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct)
        {
            if (callNext) await next(ct);
            return replacement;
        }
    }
}
