using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.Diagnostics;
using JKToolKit.CodexSDK.Facade;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.Stdio;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class HighLevelTurnTests
{
    [Fact]
    public async Task CollectedResult_SurvivesQueueOverflow_AndBroadcastReaders()
    {
        await using var fixture = new Fixture(capacity: 1);
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        await using var ui = turn.Subscribe(10);
        await using var logger = turn.Subscribe(10);
        await using var slow = turn.Subscribe(1);
        await fixture.Rpc.Item("first", "commentary", "working");
        await fixture.Rpc.Item("last", "final_answer", "answer");
        await fixture.Rpc.Emit("thread/tokenUsage/updated", """{"threadId":"t","turnId":"u","tokenUsage":{"last":{"totalTokens":12}}}""");
        await fixture.Rpc.Emit("turn/diff/updated", """{"threadId":"t","turnId":"u","diff":"+hello"}""");
        await fixture.Rpc.Complete("completed");
        var result = await turn.RunAsync();
        result.FinalResponse.Should().Be("answer");
        result.Items.Should().HaveCount(2);
        result.Usage!.Last!.TotalTokens.Should().Be(12);
        result.Diff.Should().Be("+hello");
        result.IsPartial.Should().BeFalse();
        (await Read(ui)).Should().Equal(await Read(logger));
        slow.DroppedEvents.Should().Be(4);
        (await Read(slow)).Should().Equal("turn/completed");
        (await turn.RunAsync()).Should().BeSameAs(result);
        await using var late = turn.Subscribe();
        (await Read(late)).Should().BeEmpty();
    }

    [Fact]
    public async Task EarlyTerminalNotification_IsCollectedBeforeStartReturns()
    {
        await using var fixture = new Fixture();
        fixture.Rpc.EarlyCompletion = true;
        var thread = await fixture.Threads.StartAsync();
        var result = await thread.RunAsync("hello");
        result.Status.Should().Be("completed");
        result.FinalResponse.Should().Be("early");
    }

    [Fact]
    public async Task ExternalMessage_UsesToolOutput_AndResumeSharesConnection()
    {
        await using var fixture = new Fixture();
        fixture.Rpc.EarlyCompletion = true;
        var thread = await fixture.Threads.ResumeAsync("t");
        var result = await thread.RunAsync(CodexInput.ExternalMessage("github", "untrusted issue"));
        fixture.Rpc.LastTurn.GetProperty("input").GetArrayLength().Should().Be(0);
        fixture.Rpc.LastTurn.GetProperty("toolOutput").GetProperty("name").GetString().Should().Be("github");
        fixture.Rpc.LastTurn.GetProperty("toolOutput").GetProperty("output").GetString().Should().Be("untrusted issue");
        result.ThreadId.Should().Be("t");
        await fixture.Threads.StartAsync();
        fixture.Starts.Should().Be(1);
        await fixture.Threads.DisposeAsync();
        var act = () => thread.RunAsync("after dispose");
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("interrupted")]
    [InlineData("futureTerminal")]
    public async Task TerminalStatusAndError_ArePreserved(string status)
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        await fixture.Rpc.Complete(status, """{"message":"failure"}""");
        var result = await turn.RunAsync();
        result.Status.Should().Be(status);
        result.Error!.Value.GetProperty("message").GetString().Should().Be("failure");
    }

    [Fact]
    public async Task CancellationOfCollection_DoesNotCancelOtherReaders()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        using var cts = new CancellationTokenSource();
        var canceled = turn.RunAsync(cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        turn.Completion.IsCompleted.Should().BeFalse();
        await fixture.Rpc.Complete("completed");
        (await turn.RunAsync()).Status.Should().Be("completed");
    }

    [Fact]
    public async Task OverlappingRun_IsRejected_AndCollectedCancellationInterrupts()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        using var cts = new CancellationTokenSource();
        var run = thread.RunAsync("hello", cts.Token);
        await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var resumed = await fixture.Threads.ResumeAsync(thread.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.RunAsync("overlap"));
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        fixture.Rpc.Interrupts.Should().Be(1);
    }

    [Fact]
    public async Task DisposingConnection_TerminatesResultAndSubscribers()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        await using var observer = turn.Subscribe();
        await fixture.Threads.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => turn.RunAsync());
        (await Read(observer)).Should().BeEmpty();
    }

    [Fact]
    public async Task MiddlewareRunsInOrder_AndActivityRetainsParentWithoutContent()
    {
        var calls = new List<string>();
        var stopped = new TaskCompletionSource<Activity>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var parent = new Activity("parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CodexTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => { if (activity.ParentId == parent.Id) stopped.TrySetResult(activity); }
        };
        ActivitySource.AddActivityListener(listener);
        await using var fixture = new Fixture(middleware: [new Middleware("a", calls), new Middleware("b", calls)]);
        fixture.Rpc.EarlyCompletion = true;
        var thread = await fixture.Threads.StartAsync();
        await thread.RunAsync("private prompt");
        var activity = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        calls.Should().Equal("a.before", "b.before", "b.after", "a.after");
        activity.ParentId.Should().Be(parent.Id);
        string.Join(" ", activity.Tags).Should().NotContain("private prompt").And.NotContain("early");
        Activity.Current.Should().BeSameAs(parent);
    }

    [Fact]
    public async Task EarlyOverflow_IsReportedAsPartial()
    {
        await using var fixture = new Fixture(capacity: 1);
        fixture.Rpc.EarlyCompletion = true;
        var thread = await fixture.Threads.StartAsync();
        var result = await thread.RunAsync("hello");
        result.IsPartial.Should().BeTrue();
        result.Status.Should().Be("completed");
    }

    [Fact]
    public async Task UpdatedItems_AreDeduplicated_AndUsagePreservesLargeCounters()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        await fixture.Rpc.Item("same", "commentary", "draft");
        await fixture.Rpc.Item("same", "final_answer", "final");
        await fixture.Rpc.Emit("thread/tokenUsage/updated", """{"threadId":"t","turnId":"u","tokenUsage":{"total":{"totalTokens":5000000000},"last":{"inputTokens":12}}}""");
        await fixture.Rpc.Complete("completed");
        var result = await turn.RunAsync();
        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be("same");
        result.FinalResponse.Should().Be("final");
        result.Usage!.Total!.TotalTokens.Should().Be(5000000000L);
        result.Usage.Last!.OutputTokens.Should().BeNull();
    }

    [Fact]
    public async Task TransportFailure_ReachesResultAndObservers()
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        await using var observer = turn.Subscribe();
        turn.Terminate(new IOException("connection lost"));
        await Assert.ThrowsAsync<IOException>(() => turn.RunAsync());
        await Assert.ThrowsAsync<IOException>(() => Read(observer));
        await using var late = turn.Subscribe();
        await Assert.ThrowsAsync<IOException>(() => Read(late));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedOptionalPayloads_DoNotPreventTerminalCompletion(bool undefined)
    {
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("hello");
        if (undefined)
        {
            turn.Observe(new AppServer.Notifications.V2AdditionalNotifications.ThreadTokenUsageUpdatedNotification("t", "u", default, default));
            turn.Complete(new AppServer.Notifications.TurnCompletedNotification("t", default, default));
        }
        else
        {
            await fixture.Rpc.Emit("thread/tokenUsage/updated", """{"threadId":"t","turnId":"u"}""");
            await fixture.Rpc.Emit("turn/completed", """{"threadId":"t","turnId":"u"}""");
        }
        var result = await turn.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
        result.Status.Should().BeNull();
        result.Usage?.Last.Should().BeNull();
        result.TerminalTurn.ValueKind.Should().Be(undefined ? JsonValueKind.Undefined : JsonValueKind.Object);
    }

    private static async Task<List<string>> Read(CodexTurnSubscription subscription)
    {
        var result = new List<string>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var item in subscription.Events(timeout.Token)) result.Add(item.Method);
        return result;
    }

    private sealed class Middleware(string name, List<string> calls) : ICodexTurnMiddleware
    {
        public async Task<CodexTurnHandle> StartAsync(CodexTurnContext context, Func<CancellationToken, Task<CodexTurnHandle>> next, CancellationToken ct)
        {
            calls.Add(name + ".before");
            var result = await next(ct);
            calls.Add(name + ".after");
            return result;
        }
    }

    internal sealed class Fixture : IAsyncDisposable, ICodexAppServerClientFactory
    {
        public Rpc Rpc { get; } = new();
        public CodexThreads Threads { get; }
        public int Starts { get; private set; }
        private readonly CodexAppServerClient _client;
        public Fixture(int capacity = 10, IReadOnlyList<ICodexTurnMiddleware>? middleware = null)
        {
            _client = new(new() { NotificationBufferCapacity = capacity }, new Process(), Rpc, NullLogger.Instance, startExitWatcher: false);
            Threads = new(new CodexAppServerFacade(this), middleware);
        }
        public Task<CodexAppServerClient> StartAsync(CancellationToken ct = default) { Starts++; return Task.FromResult(_client); }
        public ValueTask DisposeAsync() => Threads.DisposeAsync();
    }

    internal sealed class Rpc : IJsonRpcConnection
    {
        public event Func<JsonRpcNotification, ValueTask>? OnNotification;
        public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }
        public bool EarlyCompletion { get; set; }
        public JsonElement LastTurn { get; private set; }
        public int Interrupts { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
        {
            if (method is "thread/start" or "thread/resume") return Json("""{"thread":{"id":"t"}}""");
            if (method == "turn/start")
            {
                LastTurn = JsonSerializer.SerializeToElement(@params, CodexAppServerClient.CreateDefaultSerializerOptions());
                if (EarlyCompletion) { await Item("i", "final_answer", "early"); await Complete("completed"); }
                Started.TrySetResult();
                return Json("""{"turn":{"id":"u","status":"inProgress"}}""");
            }
            if (method == "turn/interrupt") { Interrupts++; await Complete("interrupted"); }
            return Json("{}");
        }
        public Task SendNotificationAsync(string method, object? @params, CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask Emit(string method, string json) => OnNotification?.Invoke(new(method, Json(json))) ?? ValueTask.CompletedTask;
        public ValueTask Item(string id, string phase, string text) => Emit("item/completed", JsonSerializer.Serialize(new { threadId = "t", turnId = "u", item = new { id, type = "agentMessage", phase, text } }));
        public ValueTask Complete(string status, string error = "null") => Emit("turn/completed", JsonSerializer.Serialize(new { threadId = "t", turn = new { id = "u", status, error = Json(error), items = Array.Empty<object>() } }));
        private static JsonElement Json(string text) { using var doc = JsonDocument.Parse(text); return doc.RootElement.Clone(); }
    }

    private sealed class Process : IStdioProcess
    {
        public Task Completion => Task.CompletedTask;
        public int? ProcessId => 1;
        public int? ExitCode => null;
        public IReadOnlyList<string> StderrTail => [];
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
