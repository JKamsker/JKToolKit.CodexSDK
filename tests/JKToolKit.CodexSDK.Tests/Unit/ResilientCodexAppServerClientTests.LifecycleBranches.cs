using System.Runtime.CompilerServices;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.AppServer.Resiliency.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class ResilientCodexAppServerClientTests
{
    [Fact]
    public async Task FailedInitialConnection_CanBeRetriedWithoutConsumingRestartBudget()
    {
        var attempts = 0;
        var failure = new IOException("startup failed");
        var adapter = new FakeAdapter { CallAsyncImpl = (_, _, _) => Task.FromResult(EmptyJson()) };
        await using var client = new ResilientCodexAppServerClient(_ =>
        {
            attempts++;
            return attempts == 1 ? Task.FromException<ICodexAppServerClientAdapter>(failure) : Task.FromResult<ICodexAppServerClientAdapter>(adapter);
        }, new(), NullLogger.Instance);
        var connect = () => client.EnsureConnectedAsync();
        (await connect.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(failure);
        client.State.Should().Be(CodexAppServerConnectionState.Restarting);
        client.RestartCount.Should().Be(0);
        await client.CallAsync("custom/read", null);
        client.State.Should().Be(CodexAppServerConnectionState.Connected);
        attempts.Should().Be(2);
        client.RestartCount.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.2)]
    [InlineData(2)]
    public async Task Restart_RetriesStartupFailures_AndIgnoresObserverFailure(double jitter)
    {
        var attempts = 0;
        var events = new List<CodexAppServerRestartEvent>();
        await using var client = new ResilientCodexAppServerClient(_ =>
        {
            attempts++;
            return attempts is 2 or 3
                ? Task.FromException<ICodexAppServerClientAdapter>(new IOException("temporary startup failure"))
                : Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter());
        }, new()
        {
            RestartPolicy = new() { MaxRestarts = 4, InitialBackoff = TimeSpan.FromMilliseconds(2), MaxBackoff = TimeSpan.FromMilliseconds(3), JitterFraction = jitter },
            OnRestart = evt => { events.Add(evt); throw new InvalidOperationException("observer failed"); }
        }, NullLogger.Instance);
        await client.EnsureConnectedAsync();
        await client.RestartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        attempts.Should().Be(4);
        client.RestartCount.Should().Be(1);
        client.State.Should().Be(CodexAppServerConnectionState.Connected);
        events.Should().ContainSingle().Which.Reason.Should().Be("manual-restart");
        client.LastRestart.Should().BeSameAs(events[0]);
    }

    [Fact]
    public async Task Restart_ConsecutiveStartupFailureLimit_PreservesLastFailure()
    {
        var attempts = 0;
        var failure = new IOException("cannot start");
        await using var client = new ResilientCodexAppServerClient(_ => ++attempts == 1
            ? Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter())
            : Task.FromException<ICodexAppServerClientAdapter>(failure), new()
        {
            RestartPolicy = new() { MaxRestarts = 2, InitialBackoff = TimeSpan.Zero, JitterFraction = 0 }
        }, NullLogger.Instance);
        await client.EnsureConnectedAsync();
        var restart = () => client.RestartAsync();
        var error = (await restart.Should().ThrowAsync<CodexAppServerUnavailableException>()).Which;
        error.InnerException.Should().BeSameAs(failure);
        error.Message.Should().Contain("2 consecutive attempts");
        attempts.Should().Be(3);
        client.State.Should().Be(CodexAppServerConnectionState.Faulted);
        client.RestartCount.Should().Be(0);
        var call = () => client.CallAsync("custom/read", null);
        (await call.Should().ThrowAsync<CodexAppServerUnavailableException>()).Which.Should().BeSameAs(error);
        var exit = async () => await client.ExitTask;
        (await exit.Should().ThrowAsync<CodexAppServerUnavailableException>()).Which.Should().BeSameAs(error);
    }

    [Fact]
    public async Task Restart_CanceledStartup_RetiresOldExitWatcherAndAllowsExplicitReconnect()
    {
        using var cts = new CancellationTokenSource();
        var attempts = 0;
        await using var connection = new ResilientAppServerConnection(token =>
        {
            attempts++;
            if (attempts == 2)
            {
                cts.Cancel();
                return Task.FromCanceled<ICodexAppServerClientAdapter>(token);
            }
            return Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter());
        }, new(), NullLogger.Instance);
        var (_, retiredVersion) = await connection.EnsureConnectedWithVersionAsync(CancellationToken.None);
        var restart = () => connection.RestartAsync(cts.Token);
        await restart.Should().ThrowAsync<OperationCanceledException>();
        // Simulate the retired process watcher acquiring the restart lock after cancellation.
        await connection.EnsureRestartedAsync(retiredVersion, null, "process-exited", CancellationToken.None);
        attempts.Should().Be(2);
        connection.ExitTask.IsCompleted.Should().BeFalse();
        connection.RestartCount.Should().Be(0);
        await connection.EnsureConnectedAsync(CancellationToken.None);
        connection.State.Should().Be(CodexAppServerConnectionState.Connected);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task Restart_ExpiredWindowEntries_DoNotConsumeCurrentBudget()
    {
        var starts = 0;
        await using var client = new ResilientCodexAppServerClient(_ =>
        {
            starts++;
            return Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter());
        }, new() { RestartPolicy = new() { MaxRestarts = 1, Window = TimeSpan.FromMilliseconds(1) } }, NullLogger.Instance);
        await client.EnsureConnectedAsync();
        await client.RestartAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(10));
        await client.RestartAsync();
        client.RestartCount.Should().Be(2);
        starts.Should().Be(3);
        client.State.Should().Be(CodexAppServerConnectionState.Connected);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NotificationStreamEnd_ThrowsWhenContinuationDisabled(bool autoRestart, bool continueAcrossRestarts)
    {
        var factory = new SequenceFactory(new FakeAdapter());
        await using var client = await StartAsync(factory, new() { AutoRestart = autoRestart, NotificationsContinueAcrossRestarts = continueAcrossRestarts });
        await using var stream = client.Notifications().GetAsyncEnumerator();
        var next = async () => await stream.MoveNextAsync();
        await next.Should().ThrowAsync<CodexAppServerDisconnectedException>().WithMessage("*ended unexpectedly*");
        factory.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task NotificationStream_PropagatesApplicationFailureWithoutRestart()
    {
        var failure = new FormatException("invalid notification");
        var adapter = new FakeAdapter { NotificationsImpl = token => ThrowNotifications(failure, token) };
        var factory = new SequenceFactory(adapter);
        await using var client = await StartAsync(factory, new());
        await using var stream = client.Notifications().GetAsyncEnumerator();
        var next = async () => await stream.MoveNextAsync();
        (await next.Should().ThrowAsync<FormatException>()).Which.Should().BeSameAs(failure);
        factory.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task Notifications_RestartMarkerCanBeDisabled()
    {
        var factory = new SequenceFactory(
            new FakeAdapter { NotificationsImpl = FirstNotifications },
            new FakeAdapter { NotificationsImpl = SecondNotifications });
        await using var client = await StartAsync(factory, new() { EmitRestartMarkerNotifications = false });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var seen = new List<string>();
        await foreach (var notification in client.Notifications(cts.Token))
        {
            seen.Add(notification.Method);
            if (seen.Count == 2) break;
        }
        seen.Should().Equal("note/one", "note/two");
        factory.StartCount.Should().Be(2);
    }

    [Fact]
    public async Task AlreadyCanceledOperation_DoesNotReachAdapter()
    {
        var dispatched = false;
        var adapter = new FakeAdapter { CallAsyncImpl = (_, _, _) => { dispatched = true; return Task.FromResult(EmptyJson()); } };
        await using var client = await StartAsync(new SequenceFactory(adapter), new());
        var action = () => client.CallAsync("custom/read", null, new CancellationToken(canceled: true));
        await action.Should().ThrowAsync<OperationCanceledException>();
        dispatched.Should().BeFalse();
    }

    private static async IAsyncEnumerable<AppServerNotification> ThrowNotifications(Exception failure, [EnumeratorCancellation] CancellationToken token)
    {
        await Task.CompletedTask;
        token.ThrowIfCancellationRequested();
        if (failure is not null) throw failure;
        yield break;
    }
}
