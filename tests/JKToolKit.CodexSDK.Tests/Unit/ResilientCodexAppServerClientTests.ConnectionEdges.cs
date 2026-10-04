using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.AppServer.Resiliency.Internal;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class ResilientCodexAppServerClientTests
{
    [Fact]
    public async Task ConcurrentInitialOperations_ShareOneConnectionAndVersion()
    {
        var startGate = new TaskCompletionSource<ICodexAppServerClientAdapter>(TaskCreationOptions.RunContinuationsAsynchronously);
        var adapter = new FakeAdapter();
        var starts = 0;
        await using var connection = new ResilientAppServerConnection(_ => { starts++; return startGate.Task; }, new(), NullLogger.Instance);
        connection.InitializeResult.Should().BeNull();
        connection.NotificationDropStats.Should().Be(new AppServerNotificationDropStats(0, 0, 0, 0, 0, 0));
        var first = connection.EnsureConnectedWithVersionAsync(CancellationToken.None);
        var second = connection.EnsureConnectedWithVersionAsync(CancellationToken.None);
        try
        {
            first.IsCompleted.Should().BeFalse();
            second.IsCompleted.Should().BeFalse();
            starts.Should().Be(1);
        }
        finally { startGate.TrySetResult(adapter); }
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        results.Should().OnlyContain(result => ReferenceEquals(result.Inner, adapter));
        results[0].Version.Should().Be(results[1].Version);
        starts.Should().Be(1);
        connection.State.Should().Be(CodexAppServerConnectionState.Connected);
    }

    [Fact]
    public async Task FaultedInnerExit_StillTransitionsToUnavailableWithoutRestart()
    {
        var adapter = new FakeAdapter();
        await using var client = await StartAsync(new SequenceFactory(adapter), new() { AutoRestart = false });
        adapter.SignalExit(new IOException("transport lifetime fault"));
        var exit = () => client.ExitTask.WaitAsync(TimeSpan.FromSeconds(5));
        await exit.Should().ThrowAsync<CodexAppServerDisconnectedException>();
        client.State.Should().Be(CodexAppServerConnectionState.Faulted);
        client.RestartCount.Should().Be(0);
    }

    [Fact]
    public async Task Restart_NullPolicyUsesDefaultAndKeepsConnectionUsable()
    {
        var starts = 0;
        await using var connection = new ResilientAppServerConnection(_ =>
        {
            starts++;
            return Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter());
        }, new() { RestartPolicy = null! }, NullLogger.Instance);
        await connection.EnsureConnectedAsync(CancellationToken.None);
        await connection.RestartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        connection.State.Should().Be(CodexAppServerConnectionState.Connected);
        connection.RestartCount.Should().Be(1);
        starts.Should().Be(2);
    }

    [Fact]
    public async Task Restart_WindowEntriesCanExpireWhileReplacementStarts()
    {
        var gate = new TaskCompletionSource<ICodexAppServerClientAdapter>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        await using var connection = new ResilientAppServerConnection(_ =>
        {
            if (++starts != 3) return Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter());
            reached.TrySetResult();
            return gate.Task;
        }, new() { RestartPolicy = new() { MaxRestarts = 2, Window = TimeSpan.FromSeconds(1), InitialBackoff = TimeSpan.Zero } }, NullLogger.Instance);
        await connection.EnsureConnectedAsync(CancellationToken.None);
        await connection.RestartAsync(CancellationToken.None);
        var replacement = connection.RestartAsync(CancellationToken.None);
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            connection.State.Should().Be(CodexAppServerConnectionState.Restarting);
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
        }
        finally { gate.TrySetResult(new FakeAdapter()); }
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        await connection.RestartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        connection.RestartCount.Should().Be(3);
        starts.Should().Be(4);
    }

    [Fact]
    public void Connection_RejectsMissingConstructionDependencies()
    {
        Action missingFactory = () => new ResilientAppServerConnection(null!, new(), NullLogger.Instance);
        missingFactory.Should().Throw<ArgumentNullException>().WithParameterName("startInner");
        Action missingOptions = () => new ResilientAppServerConnection(_ => Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter()), null!, NullLogger.Instance);
        missingOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
        Action missingLogger = () => new ResilientAppServerConnection(_ => Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter()), new(), null!);
        missingLogger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void DisconnectClassification_RecognizesWrappedClosedConnectionOnly()
    {
        ResilientAppServerConnection.IsDisconnect(new JsonRpcProtocolException("invalid protocol", new IOException("read failure"))).Should().BeFalse();
        ResilientAppServerConnection.IsDisconnect(new JsonRpcProtocolException("invalid protocol", new JsonRpcConnectionClosedException("connection closed"))).Should().BeTrue();
    }
}
