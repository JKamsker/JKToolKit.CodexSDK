using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Overrides;
using Xunit.Abstractions;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class TurnOwnershipFuzzTests(ITestOutputHelper output)
{
    [Fact]
    public async Task NotificationAlreadyInFlightAtShutdown_PreservesSealedGlobalQueues()
    {
        using var transformer = new BlockingTransformer();
        await using var fixture = new Fixture(configure: options => options.NotificationTransformers = [transformer]);
        await using var client = await fixture.StartAsync();
        for (var i = 0; i < 4; i++) await fixture.Rpc.Emit("fuzz/event", JsonSerializer.Serialize(new { value = i }));
        transformer.Enabled = true;
        var late = Task.Run(async () => await fixture.Rpc.Emit("fuzz/event", "{\"value\":99}"));
        try
        {
            await transformer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await client.DisposeAsync();
        }
        finally { transformer.Release.Set(); }
        await late.WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var typed = new List<int>();
        await foreach (var note in client.Notifications(timeout.Token)) typed.Add(note.Params.GetProperty("value").GetInt32());
        var raw = new List<int>();
        await foreach (var note in client.NotificationsRaw(timeout.Token)) raw.Add(note.Params.GetProperty("value").GetInt32());
        Assert.Equal(Enumerable.Range(0, 4), typed);
        Assert.Equal(typed, raw);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(43121)]
    public async Task DisposedTurn_LateNotificationsCannotDrainSealedQueues(int seed)
    {
        var random = new Random(seed);
        for (var iteration = 0; iteration < TurnStateFuzzTests.Cases; iteration++)
        {
            var capacity = random.Next(1, 9);
            var count = random.Next(1, 20);
            await using var fixture = new Fixture(capacity: capacity);
            var thread = await fixture.Threads.StartAsync();
            await using var handle = await thread.RunStreamedAsync("start");
            for (var i = 0; i < count; i++) await fixture.Rpc.Item($"i{i}", "commentary", $"text{i}");
            await handle.DisposeAsync();
            for (var i = 0; i < random.Next(1, 10); i++) await fixture.Rpc.Item($"late{i}", "commentary", "late");
            await fixture.Rpc.Complete("completed");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var actual = new List<string>();
            await foreach (var note in handle.Events(timeout.Token)) actual.Add(((ItemCompletedNotification)note).Item.GetProperty("id").GetString()!);
            var raw = new List<string>();
            await foreach (var note in handle.EventsRaw(timeout.Token)) raw.Add(note.Params.GetProperty("item").GetProperty("id").GetString()!);
            var expected = Enumerable.Range(Math.Max(0, count - capacity), Math.Min(count, capacity)).Select(i => $"i{i}");
            Assert.True(actual.SequenceEqual(expected), $"seed={seed}, case={iteration}, capacity={capacity}, count={count}: sealed typed queue was drained");
            Assert.Equal(actual, raw);
            await using var next = await thread.RunStreamedAsync("after actual terminal");
        }
    }

    [Theory]
    [InlineData(19)]
    [InlineData(97)]
    [InlineData(43121)]
    public async Task CancellationSchedules_RetainOwnershipUntilRealTerminal(int seed)
    {
        var random = new Random(seed);
        for (var iteration = 0; iteration < TurnStateFuzzTests.Cases; iteration++)
        {
            await using var fixture = new Fixture();
            fixture.Rpc.CompleteOnInterrupt = false;
            fixture.Rpc.FailInterrupt = random.Next(2) == 0;
            fixture.Rpc.StartResponseGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = await fixture.Threads.StartAsync();
            var resumed = await fixture.Threads.ResumeAsync(thread.Id);
            using var cancellation = new CancellationTokenSource();
            var run = thread.RunAsync("start", cancellation.Token);
            await fixture.Rpc.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var responseFirst = random.Next(2) == 0;
            if (responseFirst) fixture.Rpc.StartResponseGate.SetResult();
            await cancellation.CancelAsync();
            if (!responseFirst && random.Next(2) == 0) await Task.Yield();
            if (!responseFirst) fixture.Rpc.StartResponseGate.SetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
            await fixture.Rpc.InterruptRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<InvalidOperationException>(() => resumed.RunStreamedAsync("overlap"));
            for (var i = 0; i < random.Next(0, 8); i++) await fixture.Rpc.Item($"i{i}", "commentary", "late");
            await fixture.Rpc.Complete(random.Next(2) == 0 ? "completed" : "interrupted");
            await using var next = await resumed.RunStreamedAsync("after terminal");
            Assert.Equal(2, fixture.Rpc.TurnStartCalls);
        }
        output.WriteLine($"seed={seed}; canceled-start schedules={TurnStateFuzzTests.Cases}");
    }

    [Fact]
    public async Task ConcurrentResumedSessions_AdmitExactlyOneTurnPerGeneration()
    {
        await using var fixture = new Fixture();
        var sessions = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => fixture.Threads.ResumeAsync("t")));
        CodexTurnHandle? prior = null;
        for (var iteration = 0; iteration < TurnStateFuzzTests.Cases; iteration++)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = sessions.Select(session => Task.Run(async () =>
            {
                await gate.Task;
                try { return await session.RunStreamedAsync("contender"); }
                catch (InvalidOperationException) { return null; }
            })).ToArray();
            gate.SetResult();
            if (prior is not null) await prior.DisposeAsync();
            var handles = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(5));
            var winner = Assert.Single(handles, handle => handle is not null)!;
            Assert.Equal(iteration + 1, fixture.Rpc.TurnStartCalls);
            await fixture.Rpc.Complete("completed");
            await winner.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
            prior = winner;
        }
        if (prior is not null) await prior.DisposeAsync();
    }

    private sealed class BlockingTransformer : IAppServerNotificationTransformer, IDisposable
    {
        public bool Enabled { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public (string Method, JsonElement Params) Transform(string method, JsonElement @params)
        {
            if (Enabled)
            {
                Entered.TrySetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("test did not release notification");
            }
            return (method, @params);
        }
        public void Dispose() => Release.Dispose();
    }
}
