using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using Xunit.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class TurnStateFuzzTests(ITestOutputHelper output)
{
    public static int Cases => int.TryParse(Environment.GetEnvironmentVariable("CODEX_STATE_FUZZ_CASES"), out var count)
        ? Math.Clamp(count, 1, 100_000) : 150;

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(97)]
    [InlineData(43121)]
    [InlineData(8675309)]
    [InlineData(2147483647)]
    public async Task TerminalHistories_MatchFirstTerminalWinsModel(int seed)
    {
        var random = new Random(seed);
        for (var iteration = 0; iteration < Cases; iteration++)
        {
            await using var handle = NewHandle();
            var models = new List<ObserverModel>();
            var history = new List<string>();
            var state = "open";
            Exception? terminalError = null;
            for (var step = 0; step < 32; step++)
            {
                var operation = random.Next(step < 8 ? 3 : 7);
                history.Add(operation.ToString());
                switch (operation)
                {
                    case 0:
                        var capacity = random.Next(1, 9);
                        models.Add(new(handle.Subscribe(capacity), capacity, state != "open", terminalError));
                        break;
                    case 1:
                        var note = AppServerNotificationMapper.Map("fuzz/event", JsonSerializer.SerializeToElement(new { step }));
                        handle.Observe(note);
                        if (state == "open")
                            foreach (var model in models.Where(model => !model.Closed)) model.Publish(note);
                        break;
                    case 2:
                        if (models.Count == 0) break;
                        var disposed = models[random.Next(models.Count)];
                        await disposed.Subscription.DisposeAsync();
                        disposed.Closed = true;
                        break;
                    case 3:
                        handle.Complete(Terminal());
                        if (state == "open") state = "completed";
                        break;
                    case 4:
                        var error = new IOException($"fault-{step}");
                        handle.Terminate(error);
                        if (state == "open") { state = "faulted"; terminalError = error; }
                        break;
                    case 5:
                        await handle.DisposeAsync();
                        if (state == "open") state = "canceled";
                        break;
                    case 6:
                        handle.Terminate();
                        if (state == "open") state = "canceled";
                        break;
                }
                if (state != "open")
                    foreach (var model in models.Where(model => !model.Closed))
                    { model.Closed = true; model.Error = terminalError; }
            }
            if (state == "open") { handle.Complete(Terminal()); state = "completed"; }
            // Always inspect a late subscriber after repeated terminal/dispose transitions.
            models.Add(new(handle.Subscribe(), 1024, true, terminalError));
            var context = $"seed={seed}, case={iteration}, operations={string.Join(',', history)}";
            var errorFromResult = await Record.ExceptionAsync(() => handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(2)));
            Assert.True(state switch
            {
                "completed" => errorFromResult is null,
                "faulted" => ReferenceEquals(errorFromResult, terminalError),
                _ => errorFromResult is OperationCanceledException
            }, context + $"; result={errorFromResult}");
            foreach (var model in models)
            {
                var actual = new List<AppServerNotification>();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var observedError = await Record.ExceptionAsync(async () =>
                {
                    await foreach (var note in model.Subscription.Events(timeout.Token)) actual.Add(note);
                });
                Assert.True(ReferenceEquals(observedError, model.Error), context + $"; observer expected={model.Error}, actual={observedError}");
                Assert.True(actual.SequenceEqual(model.Items), context + "; observer contents changed after terminal state");
                Assert.True(model.Subscription.DroppedEvents == model.Dropped, context + "; incorrect drop count");
                await model.Subscription.DisposeAsync();
            }
        }
        output.WriteLine($"seed={seed}; histories={Cases}; transitions={Cases * 32}");
    }

    [Fact]
    public async Task ConcurrentTerminalRaces_KeepResultAndLateObserversConsistent()
    {
        for (var iteration = 0; iteration < Cases; iteration++)
        {
            await using var handle = NewHandle();
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var failure = new IOException("racing disconnect");
            var complete = Task.Run(async () => { await gate.Task; handle.Complete(Terminal()); });
            var fault = Task.Run(async () => { await gate.Task; handle.Terminate(failure); });
            var dispose = Task.Run(async () => { await gate.Task; await handle.DisposeAsync(); });
            gate.SetResult();
            await Task.WhenAll(complete, fault, dispose).WaitAsync(TimeSpan.FromSeconds(5));
            var resultError = await Record.ExceptionAsync(() => handle.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            await using var late = handle.Subscribe();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var streamError = await Record.ExceptionAsync(async () => { await foreach (var _ in late.Events(timeout.Token)) { } });
            var expectedStreamError = resultError is OperationCanceledException ? null : resultError;
            Assert.True(ReferenceEquals(expectedStreamError, streamError), $"race={iteration}; result={resultError}; stream={streamError}");
        }
    }

    internal static CodexTurnHandle NewHandle() => new("t", "u", _ => Task.CompletedTask, null, null, () => { }, 8)
        { KeepRegisteredUntilTerminal = true };

    internal static TurnCompletedNotification Terminal() =>
        new("t", JsonSerializer.SerializeToElement(new { id = "u", status = "completed", items = Array.Empty<object>() }), default);

    private sealed class ObserverModel(CodexTurnSubscription subscription, int capacity, bool closed, Exception? error)
    {
        public CodexTurnSubscription Subscription { get; } = subscription;
        public bool Closed { get; set; } = closed;
        public Exception? Error { get; set; } = error;
        public Queue<AppServerNotification> Items { get; } = new();
        public long Dropped { get; private set; }
        public void Publish(AppServerNotification note)
        {
            if (Items.Count == capacity) { Items.Dequeue(); Dropped++; }
            Items.Enqueue(note);
        }
    }
}
