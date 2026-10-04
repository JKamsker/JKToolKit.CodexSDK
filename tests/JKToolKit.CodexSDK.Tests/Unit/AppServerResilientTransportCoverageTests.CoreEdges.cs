using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Overrides;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyTransformedNotification_IsDroppedBeforeObservationAndDelivery(string method)
    {
        var observer = new Observer();
        var rpc = new RecordingRpc("{}");
        await using var client = new CodexAppServerClient(new()
        {
            NotificationTransformers = [new DropSelectedNotification(method)],
            MessageObservers = [observer]
        }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await rpc.EmitAsync("drop/event", JsonSerializer.SerializeToElement(new { }));
        await rpc.EmitAsync("keep/event", JsonSerializer.SerializeToElement(new { value = 42 }));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = client.Notifications(deadline.Token).GetAsyncEnumerator();
        (await stream.MoveNextAsync()).Should().BeTrue();
        stream.Current.Method.Should().Be("keep/event");
        observer.Notifications.Should().ContainSingle().Which.GetProperty("value").GetInt32().Should().Be(42);
        client.NotificationDropStats.GlobalNotificationsDropped.Should().Be(0);
    }

    [Fact]
    public async Task CustomNotificationWithNonObjectParameters_RemainsOnGlobalStream()
    {
        var notification = new UnknownNotification("future/event", JsonSerializer.SerializeToElement(42));
        var rpc = new RecordingRpc("{}");
        await using var client = new CodexAppServerClient(new() { NotificationMappers = [new FixedMapper(notification)] }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        await rpc.EmitAsync("future/event", JsonSerializer.SerializeToElement(new { }));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = client.Notifications(deadline.Token).GetAsyncEnumerator();
        (await stream.MoveNextAsync()).Should().BeTrue();
        stream.Current.Should().BeSameAs(notification);
    }

    [Fact]
    public async Task TransportExit_TerminatesActiveTurnAndBothGlobalStreams()
    {
        var lifetime = new ExitingLifetime(false);
        var rpc = new RecordingRpc("""{"turn":{"id":"turn-1"}}""");
        await using var client = new CodexAppServerClient(new(), lifetime, rpc, NullLogger.Instance, CodexAppServerClient.CreateDefaultSerializerOptions());
        await using var turn = await client.StartTurnAsync("thread-1", new());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var typed = client.Notifications(deadline.Token).GetAsyncEnumerator();
        await using var raw = client.NotificationsRaw(deadline.Token).GetAsyncEnumerator();
        var nextTyped = typed.MoveNextAsync().AsTask();
        var nextRaw = raw.MoveNextAsync().AsTask();
        lifetime.Exit.TrySetResult();
        Func<Task> completion = () => turn.Completion.WaitAsync(deadline.Token);
        var failure = (await completion.Should().ThrowAsync<CodexAppServerDisconnectedException>()).Which;
        failure.ExitCode.Should().Be(7);
        Func<Task> typedRead = () => nextTyped.WaitAsync(deadline.Token);
        Func<Task> rawRead = () => nextRaw.WaitAsync(deadline.Token);
        (await typedRead.Should().ThrowAsync<CodexAppServerDisconnectedException>()).Which.Should().BeSameAs(failure);
        (await rawRead.Should().ThrowAsync<CodexAppServerDisconnectedException>()).Which.Should().BeSameAs(failure);
    }

    [Fact]
    public void DropOldest_CompletedChannelReturnsWithoutIncreasingLossCount()
    {
        var channel = Channel.CreateBounded<int>(1);
        channel.Writer.Complete();
        long drops = 0;
        CodexAppServerClientCore.TryWriteDroppingOldest(channel, 42, ref drops);
        drops.Should().Be(0);
        channel.Reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Core_RejectsMissingDependenciesBeforeRegisteringCallbacks()
    {
        var rpc = new RecordingRpc("{}");
        var lifetime = new ExitingLifetime(false);
        Action missingOptions = () => new CodexAppServerClientCore(null!, lifetime, rpc, NullLogger.Instance, false);
        missingOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
        Action missingLifetime = () => new CodexAppServerClientCore(new(), (IAppServerLifetime)null!, rpc, NullLogger.Instance, false);
        missingLifetime.Should().Throw<ArgumentNullException>().WithParameterName("lifetime");
        Action missingRpc = () => new CodexAppServerClientCore(new(), lifetime, null!, NullLogger.Instance, false);
        missingRpc.Should().Throw<ArgumentNullException>().WithParameterName("rpc");
        Action missingLogger = () => new CodexAppServerClientCore(new(), lifetime, rpc, null!, false);
        missingLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        rpc.OnServerRequest.Should().BeNull();
    }

    private sealed class DropSelectedNotification(string blankMethod) : IAppServerNotificationTransformer
    {
        public (string Method, JsonElement Params) Transform(string method, JsonElement parameters) =>
            (method == "drop/event" ? blankMethod : method, parameters);
    }
}
