using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.Diagnostics;
using Fixture = JKToolKit.CodexSDK.Tests.Unit.HighLevelTurnTests.Fixture;

namespace JKToolKit.CodexSDK.Tests.Unit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryContractCollection
{
    public const string Name = "Telemetry outcome contracts";
}

[Collection(TelemetryContractCollection.Name)]
public sealed class TelemetryOutcomeContractTests
{
    [Theory]
    [InlineData("completed", false, "completed")]
    [InlineData("failed", true, "failed")]
    [InlineData("interrupted", false, "interrupted")]
    [InlineData("canceled", false, "canceled")]
    [InlineData("error", true, "error")]
    [InlineData("future-status-with-private-detail", false, "unknown")]
    [InlineData(null, false, "unknown")]
    public async Task TerminalOutcome_IsRecordedOnceWithBoundedMetricTags(string? status, bool failed, string metricStatus)
    {
        using var capture = new Capture();
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync(new TurnStartOptions
        {
            Input = [TurnInputItem.Text("private prompt")], Model = "model-contract"
        });
        Assert.Empty(capture.Counts);
        Assert.Empty(capture.Durations);
        Assert.False(capture.Stopped.Task.IsCompleted);

        var terminal = JsonSerializer.SerializeToElement(new
        {
            id = turn.TurnId, status, error = failed ? new { message = "private error" } : null,
            items = new[] { new { id = "answer", type = "agentMessage", text = "private response" } }
        });
        turn.Complete(new TurnCompletedNotification(thread.Id, terminal, default));
        var result = await turn.RunAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(status, result.Status);
        var activity = await capture.WaitAsync();
        Assert.Equal("invoke_agent", activity.GetTagItem("gen_ai.operation.name"));
        Assert.Equal("openai", activity.GetTagItem("gen_ai.provider.name"));
        Assert.Equal(thread.Id, activity.GetTagItem("gen_ai.conversation.id"));
        Assert.Equal("model-contract", activity.GetTagItem("gen_ai.request.model"));
        Assert.Equal(turn.TurnId, activity.GetTagItem("codex.turn.id"));
        Assert.Equal(status ?? "unknown", activity.GetTagItem("codex.turn.status"));
        Assert.Equal(failed ? "turn_failed" : null, activity.GetTagItem("error.type"));
        Assert.Equal(failed ? ActivityStatusCode.Error : ActivityStatusCode.Unset, activity.Status);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        var recorded = string.Join(" ", activity.TagObjects);
        Assert.DoesNotContain("private prompt", recorded);
        Assert.DoesNotContain("private response", recorded);
        Assert.DoesNotContain("private error", recorded);

        // Duplicate terminal signals and observation disposal must not count the same turn twice.
        turn.Complete(new TurnCompletedNotification(thread.Id, terminal, default));
        await turn.DisposeAsync();
        Assert.Same(result, await turn.RunAsync());
        capture.AssertSingleOutcome(metricStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedStartup_RecordsOriginalFailureKindAndDoesNotInventTurnId(bool canceled)
    {
        using var capture = new Capture();
        await using var fixture = new Fixture();
        var failure = canceled ? (Exception)new OperationCanceledException("private cancellation") : new IOException("private connection detail");
        fixture.Rpc.StartFailure = failure;
        var client = await fixture.StartAsync();
        var observed = await Record.ExceptionAsync(() => client.StartTurnAsync("t", new TurnStartOptions
        {
            Input = [TurnInputItem.Text("private prompt")]
        }));
        Assert.Same(failure, observed);
        var activity = await capture.WaitAsync();
        Assert.Equal(canceled ? "canceled" : "error", activity.GetTagItem("codex.turn.status"));
        Assert.Equal(failure.GetType().Name, activity.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.GetTagItem("codex.turn.id"));
        Assert.Null(activity.GetTagItem("gen_ai.request.model"));
        Assert.DoesNotContain(failure.Message, string.Join(" ", activity.TagObjects));
        capture.AssertSingleOutcome(canceled ? "canceled" : "error");
    }

    [Fact]
    public async Task TransportFailureAfterStartup_RecordsFailureOnceAndPreservesResultException()
    {
        using var capture = new Capture();
        await using var fixture = new Fixture();
        var thread = await fixture.Threads.StartAsync();
        await using var turn = await thread.RunStreamedAsync("private prompt");
        var failure = new IOException("private transport detail");
        turn.Terminate(failure);
        Assert.Same(failure, await Record.ExceptionAsync(() => turn.RunAsync().WaitAsync(TimeSpan.FromSeconds(5))));
        var activity = await capture.WaitAsync();
        Assert.Equal("error", activity.GetTagItem("codex.turn.status"));
        Assert.Equal(nameof(IOException), activity.GetTagItem("error.type"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        turn.Terminate(new IOException("later failure"));
        await turn.DisposeAsync();
        capture.AssertSingleOutcome("error");
    }

    private sealed class Capture : IDisposable
    {
        private readonly MeterListener _metrics = new();
        private readonly ActivityListener _activities;
        private readonly TaskCompletionSource _measured = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<(long Value, KeyValuePair<string, object?>[] Tags)> Counts { get; } = new();
        public ConcurrentQueue<(double Value, KeyValuePair<string, object?>[] Tags)> Durations { get; } = new();
        public TaskCompletionSource<Activity> Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Capture()
        {
            _activities = new ActivityListener
            {
                ShouldListenTo = source => source.Name == CodexTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => Stopped.TrySetResult(activity)
            };
            ActivitySource.AddActivityListener(_activities);
            _metrics.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == CodexTelemetry.Name) listener.EnableMeasurementEvents(instrument);
            };
            _metrics.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                if (instrument.Name != "codex.turn.count") return;
                Counts.Enqueue((value, tags.ToArray()));
                if (!Durations.IsEmpty) _measured.TrySetResult();
            });
            _metrics.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            {
                if (instrument.Name != "codex.turn.duration") return;
                Durations.Enqueue((value, tags.ToArray()));
                if (!Counts.IsEmpty) _measured.TrySetResult();
            });
            _metrics.Start();
        }

        public async Task<Activity> WaitAsync()
        {
            var activity = await Stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await _measured.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return activity;
        }

        public void AssertSingleOutcome(string status)
        {
            var count = Assert.Single(Counts);
            Assert.Equal(1, count.Value);
            var duration = Assert.Single(Durations);
            Assert.True(double.IsFinite(duration.Value) && duration.Value >= 0);
            Assert.Equal(new KeyValuePair<string, object?>("codex.turn.status", status), Assert.Single(count.Tags));
            Assert.Equal(count.Tags, duration.Tags);
        }

        public void Dispose() { _metrics.Dispose(); _activities.Dispose(); }
    }
}
