using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Notifications.V2AdditionalNotifications;
using JKToolKit.CodexSDK.AppServer.Overrides;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    public static IEnumerable<object[]> TypedTurnNotifications()
    {
        var empty = JsonSerializer.SerializeToElement(new { });
        yield return [new TurnStartedNotification("thread-1", JsonSerializer.SerializeToElement(new { id = "turn-1" }), empty)];
        yield return [new TurnDiffUpdatedNotification("thread-1", "turn-1", "patch", empty)];
        yield return [new TurnPlanUpdatedNotification("thread-1", "turn-1", "plan", [], empty)];
        yield return [new ThreadTokenUsageUpdatedNotification("thread-1", "turn-1", empty, empty)];
        yield return [new PlanDeltaNotification("thread-1", "turn-1", "item-1", "plan", empty)];
        yield return [new RawResponseItemCompletedNotification("thread-1", "turn-1", empty, empty)];
        yield return [new CommandExecutionOutputDeltaNotification("thread-1", "turn-1", "item-1", "output", empty)];
        yield return [new TerminalInteractionNotification("thread-1", "turn-1", "item-1", "process-1", "input", empty)];
        yield return [new FileChangeOutputDeltaNotification("thread-1", "turn-1", "item-1", "patch", empty)];
        yield return [new McpToolCallProgressNotification("thread-1", "turn-1", "item-1", "progress", empty)];
        yield return [new ReasoningSummaryTextDeltaNotification("thread-1", "turn-1", "item-1", "summary", 1, empty)];
        yield return [new ReasoningSummaryPartAddedNotification("thread-1", "turn-1", "item-1", 1, empty)];
        yield return [new ReasoningTextDeltaNotification("thread-1", "turn-1", "item-1", "reasoning", 1, empty)];
        yield return [new ContextCompactedNotification("thread-1", "turn-1", empty)];
        yield return [new ErrorNotification("thread-1", "turn-1", JsonSerializer.SerializeToElement(new { message = "retry" }), true, empty)];
    }

    [Theory]
    [MemberData(nameof(TypedTurnNotifications))]
    public async Task CustomTypedNotification_RoutesByTypedTurnIdentifierEvenWithoutRawIdentifier(AppServerNotification notification)
    {
        var rpc = new RecordingRpc("""{"turn":{"id":"turn-1"}}""");
        await using var client = new CodexAppServerClient(new()
        {
            NotificationMappers = [new FixedMapper(notification)]
        }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await using var turn = await client.StartTurnAsync("thread-1", new());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = turn.Events(cts.Token).GetAsyncEnumerator();
        var next = stream.MoveNextAsync().AsTask();
        await rpc.EmitAsync(notification.Method, JsonSerializer.SerializeToElement(new { }));
        (await next.WaitAsync(cts.Token)).Should().BeTrue();
        stream.Current.Should().BeSameAs(notification);
        turn.Completion.IsCompleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("{\"turn\":\"turn-1\"}")]
    [InlineData("{\"turn\":{\"id\":\"turn-1\"}}")]
    public async Task UnknownNotifications_RouteUsingLegacyTurnIdentifierShapes(string parameters)
    {
        var rpc = new RecordingRpc("""{"turn":{"id":"turn-1"}}""");
        await using var client = new CodexAppServerClient(new(), new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await using var turn = await client.StartTurnAsync("thread-1", new());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = turn.Events(cts.Token).GetAsyncEnumerator();
        var next = stream.MoveNextAsync().AsTask();
        await rpc.EmitAsync("future/event", JsonDocument.Parse(parameters).RootElement);
        (await next.WaitAsync(cts.Token)).Should().BeTrue();
        stream.Current.Should().BeOfType<UnknownNotification>().Which.Method.Should().Be("future/event");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"turn\":\" \"}")]
    [InlineData("{\"turn\":{\"id\":\" \"}}")]
    public async Task NotificationWithoutTurnIdentifier_RemainsOnGlobalStream(string parameters)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = new CodexAppServerClient(new(), new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = client.Notifications(cts.Token).GetAsyncEnumerator();
        await rpc.EmitAsync("future/event", JsonDocument.Parse(parameters).RootElement);
        (await stream.MoveNextAsync()).Should().BeTrue();
        stream.Current.Method.Should().Be("future/event");
        client.NotificationDropStats.BufferedTurnNotificationsDroppedCapacity.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TurnCompletion_RemainsTerminalWhenCustomMapperDeclinesOrReturnsUnknown(bool returnsUnknown)
    {
        var parameters = JsonSerializer.SerializeToElement(new { threadId = "thread-1", turn = new { id = "turn-1", status = "completed" } });
        var rpc = new RecordingRpc("""{"turn":{"id":"turn-1"}}""");
        await using var client = new CodexAppServerClient(new()
        {
            NotificationMappers = [new FixedMapper(returnsUnknown ? new UnknownNotification("turn/completed", parameters) : null)]
        }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await using var turn = await client.StartTurnAsync("thread-1", new());
        await rpc.EmitAsync("turn/completed", parameters);
        var completed = await turn.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        completed.TurnId.Should().Be("turn-1");
        completed.Status.Should().Be("completed");
    }

    private sealed class FixedMapper(AppServerNotification? notification) : IAppServerNotificationMapper
    {
        public AppServerNotification? TryMap(string method, JsonElement parameters) => notification;
    }
}
