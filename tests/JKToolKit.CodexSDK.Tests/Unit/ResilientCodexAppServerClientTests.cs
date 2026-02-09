using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class ResilientCodexAppServerClientTests
{
    [Fact]
    public async Task CallAsync_WhenDisconnected_Restarts_AndCanRetry_WhenPolicyAllows()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 42)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                using var doc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(first, second);

        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            RetryPolicy = ctx => new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.Retry())
        };

        await using var client = await StartAsync(factory, options);

        var result = await client.CallAsync("ping", @params: null);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();
        factory.StartCount.Should().Be(2);
        client.RestartCount.Should().Be(1);
    }

    [Fact]
    public async Task CallAsync_WhenDisconnected_DoesNotRetryByDefault_ButRestartsForNextCall()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 1)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                using var doc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions { AutoRestart = true };

        await using var client = await StartAsync(factory, options);

        var act = async () => await client.CallAsync("ping", @params: null);
        await act.Should().ThrowAsync<CodexAppServerDisconnectedException>();

        var result = await client.CallAsync("ping", @params: null);
        result.GetProperty("ok").GetBoolean().Should().BeTrue();

        factory.StartCount.Should().Be(2);
        client.RestartCount.Should().Be(1);
    }

    [Fact]
    public async Task Notifications_ContinueAcrossRestarts_AndEmitRestartMarker()
    {
        var first = new FakeAdapter
        {
            NotificationsImpl = FirstNotifications
        };

        var second = new FakeAdapter
        {
            NotificationsImpl = SecondNotifications
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            NotificationsContinueAcrossRestarts = true,
            EmitRestartMarkerNotifications = true
        };

        await using var client = await StartAsync(factory, options);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var seen = new List<string>();

        await foreach (var n in client.Notifications(cts.Token))
        {
            seen.Add(n.Method);
            if (seen.Count >= 3)
                break;
        }

        seen.Should().ContainInOrder("note/one", "client/restarted", "note/two");
        factory.StartCount.Should().Be(2);
    }

    private static async IAsyncEnumerable<AppServerNotification> FirstNotifications(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return new UnknownNotification("note/one", EmptyJson());
        throw Disconnect("nope", exitCode: 7);
    }

    private static async IAsyncEnumerable<AppServerNotification> SecondNotifications(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        yield return new UnknownNotification("note/two", EmptyJson());
    }

    [Fact]
    public async Task RestartLimit_IsEnforced_AndClientFaults()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 2)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom2", exitCode: 3)
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            RestartPolicy = new CodexAppServerRestartPolicy
            {
                MaxRestarts = 1,
                Window = TimeSpan.FromMinutes(1),
                InitialBackoff = TimeSpan.Zero,
                MaxBackoff = TimeSpan.Zero,
                JitterFraction = 0
            }
        };

        await using var client = await StartAsync(factory, options);

        var act1 = async () => await client.CallAsync("ping", @params: null);
        await act1.Should().ThrowAsync<CodexAppServerDisconnectedException>();

        var act2 = async () => await client.CallAsync("ping", @params: null);
        await act2.Should().ThrowAsync<CodexAppServerUnavailableException>();

        client.State.Should().Be(CodexAppServerConnectionState.Faulted);
    }

    [Fact]
    public async Task ConcurrentDisconnect_TriggersSingleRestart()
    {
        var first = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) => throw Disconnect("boom", exitCode: 1)
        };

        var second = new FakeAdapter
        {
            CallAsyncImpl = (_, _, _) =>
            {
                using var doc = JsonDocument.Parse("""{"ok":true}""");
                return Task.FromResult(doc.RootElement.Clone());
            }
        };

        var factory = new SequenceFactory(first, second);
        var options = new CodexAppServerResilienceOptions
        {
            AutoRestart = true,
            RetryPolicy = ctx => new ValueTask<CodexAppServerRetryDecision>(CodexAppServerRetryDecision.Retry())
        };

        await using var client = await StartAsync(factory, options);

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var el = await client.CallAsync("ping", @params: null);
            el.GetProperty("ok").GetBoolean().Should().BeTrue();
        });

        await Task.WhenAll(tasks);

        factory.StartCount.Should().Be(2);
        client.RestartCount.Should().Be(1);
    }

    private static async Task<ResilientCodexAppServerClient> StartAsync(
        SequenceFactory factory,
        CodexAppServerResilienceOptions options)
    {
        return await CreateResilientAsync(factory, options);
    }

    private static Task<ResilientCodexAppServerClient> CreateResilientAsync(
        SequenceFactory factory,
        CodexAppServerResilienceOptions options)
    {
        var logger = NullLogger.Instance;

        var resilient = new ResilientCodexAppServerClient(
            startInner: ct => Task.FromResult<ResilientCodexAppServerClient.ICodexAppServerClientAdapter>(factory.Start()),
            options: options,
            logger: logger);

        return Task.FromResult(resilient);
    }

    private static CodexAppServerDisconnectedException Disconnect(string message, int exitCode) =>
        new(
            message,
            processId: 123,
            exitCode: exitCode,
            stderrTail: new[] { "stderr: test" });

    private static JsonElement EmptyJson()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }

    private sealed class SequenceFactory
    {
        private readonly Queue<FakeAdapter> _queue;
        public int StartCount { get; private set; }

        public SequenceFactory(params FakeAdapter[] adapters)
        {
            _queue = new Queue<FakeAdapter>(adapters);
        }

        public FakeAdapter Start()
        {
            StartCount++;
            return _queue.Count > 0 ? _queue.Dequeue() : new FakeAdapter();
        }
    }

    private sealed class FakeAdapter : ResilientCodexAppServerClient.ICodexAppServerClientAdapter
    {
        private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Func<string, object?, CancellationToken, Task<JsonElement>>? CallAsyncImpl { get; init; }

        public Func<CancellationToken, IAsyncEnumerable<AppServerNotification>>? NotificationsImpl { get; init; }

        public Task ExitTask => _exit.Task;

        public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct)
        {
            if (CallAsyncImpl is null)
                throw new InvalidOperationException("CallAsyncImpl not configured.");
            return CallAsyncImpl(method, @params, ct);
        }

        public IAsyncEnumerable<AppServerNotification> Notifications(CancellationToken ct)
        {
            if (NotificationsImpl is null)
                return AsyncEnumerable.Empty<AppServerNotification>();
            return NotificationsImpl(ct);
        }

        public Task<CodexThread> StartThreadAsync(ThreadStartOptions options, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CodexThread> ResumeThreadAsync(string threadId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CodexThread> ResumeThreadAsync(ThreadResumeOptions options, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<CodexTurnHandle> StartTurnAsync(string threadId, TurnStartOptions options, CancellationToken ct) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            _exit.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private static class AsyncEnumerable
    {
        public static async IAsyncEnumerable<T> Empty<T>([EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
