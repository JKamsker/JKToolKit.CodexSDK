using System.Reflection;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using JKToolKit.CodexSDK.AppServer.Resiliency.Internal;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class ResilientCodexAppServerClientTests
{
    [Theory]
    [InlineData(1, 100, 500, 0)]
    [InlineData(2, 100, 500, 100)]
    [InlineData(3, 100, 500, 200)]
    [InlineData(4, 100, 500, 400)]
    [InlineData(5, 100, 500, 500)]
    [InlineData(20, 1, 100000, 1024)]
    public void RestartBackoff_UsesExponentialScheduleAndBothCaps(int attempt, int initialMs, int maximumMs, int expectedMs)
    {
        // The existing pure helper avoids scheduler timing assertions and production test seams.
        var compute = typeof(ResilientAppServerConnection).GetMethod("ComputeBackoff", BindingFlags.NonPublic | BindingFlags.Static)!;
        var policy = new CodexAppServerRestartPolicy
        {
            InitialBackoff = TimeSpan.FromMilliseconds(initialMs),
            MaxBackoff = TimeSpan.FromMilliseconds(maximumMs),
            JitterFraction = 0
        };
        var delay = (TimeSpan)compute.Invoke(null, [policy, attempt])!;
        delay.Should().Be(TimeSpan.FromMilliseconds(expectedMs));
    }

    [Fact]
    public async Task RestartBackoff_CancellationBeforeDelayPreventsAnotherStartup()
    {
        using var cancellation = new CancellationTokenSource();
        var logger = new CancelAtBackoffLogger(cancellation);
        var attempts = 0;
        await using var connection = new ResilientAppServerConnection(_ =>
        {
            attempts++;
            return attempts == 2
                ? Task.FromException<ICodexAppServerClientAdapter>(new IOException("retry startup"))
                : Task.FromResult<ICodexAppServerClientAdapter>(new FakeAdapter());
        }, new()
        {
            RestartPolicy = new() { MaxRestarts = 3, InitialBackoff = TimeSpan.FromMinutes(1), MaxBackoff = TimeSpan.FromMinutes(1), JitterFraction = 0 }
        }, logger);
        try
        {
            await connection.EnsureConnectedAsync(CancellationToken.None);
            var restart = () => connection.RestartAsync(cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
            await restart.Should().ThrowAsync<OperationCanceledException>();
            logger.BackoffObserved.Should().BeTrue();
            attempts.Should().Be(2, "cancellation at the backoff boundary must prevent the next factory invocation");
            connection.RestartCount.Should().Be(0);
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    private sealed class CancelAtBackoffLogger(CancellationTokenSource cancellation) : ILogger
    {
        public bool BackoffObserved { get; private set; }
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!formatter(state, exception).StartsWith("Delaying app-server restart", StringComparison.Ordinal)) return;
            BackoffObserved = true;
            cancellation.Cancel();
        }
    }
}
