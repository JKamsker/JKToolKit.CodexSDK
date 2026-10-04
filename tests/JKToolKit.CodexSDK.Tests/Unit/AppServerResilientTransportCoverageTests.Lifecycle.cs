using System.Text.Json;
using FluentAssertions;
using JKToolKit.CodexSDK.AppServer;
using JKToolKit.CodexSDK.AppServer.Internal;
using JKToolKit.CodexSDK.AppServer.Notifications;
using JKToolKit.CodexSDK.AppServer.Overrides;
using JKToolKit.CodexSDK.AppServer.Resiliency;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed partial class AppServerResilientTransportCoverageTests
{
    [Fact]
    public async Task Dispose_ConcurrentAndRepeatedCallersWaitForOneCleanup()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rpc = new RecordingRpc("{}")
        {
            DisposeOverride = async () => { started.TrySetResult(); await release.Task; }
        };
        var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        var first = client.DisposeAsync().AsTask();
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = client.DisposeAsync().AsTask();
            first.IsCompleted.Should().BeFalse();
            second.IsCompleted.Should().BeFalse("all disposal callers must await the shared cleanup");
            release.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            await client.DisposeAsync();
            rpc.DisposeCount.Should().Be(1);
            client.State.Should().Be(CodexAppServerConnectionState.Disposed);
            var action = () => client.CallAsync("custom/read", null);
            await action.Should().ThrowAsync<ObjectDisposedException>();
            rpc.Requests.Should().BeEmpty();
        }
        finally
        {
            release.TrySetResult();
            await first.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationCancellation_StopsWaitingWithoutRestart(bool raw)
    {
        var rpc = new RecordingRpc("{}");
        await using var client = await ResilientCodexAppServerClient.StartAsync(new Factory(rpc));
        using var cts = new CancellationTokenSource();
        if (raw)
        {
            await using var enumerator = client.NotificationsRaw(cts.Token).GetAsyncEnumerator();
            var next = enumerator.MoveNextAsync().AsTask();
            await rpc.EmitAsync("custom/event", JsonDocument.Parse("""{"value":7}""").RootElement);
            (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            enumerator.Current.Method.Should().Be("custom/event");
            enumerator.Current.Params.GetProperty("value").GetInt32().Should().Be(7);
            next = enumerator.MoveNextAsync().AsTask();
            cts.Cancel();
            (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeFalse();
        }
        else
        {
            await using var enumerator = client.Notifications(cts.Token).GetAsyncEnumerator();
            var next = enumerator.MoveNextAsync().AsTask();
            await rpc.EmitAsync("custom/event", JsonDocument.Parse("{}").RootElement);
            (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
            enumerator.Current.Should().BeOfType<UnknownNotification>().Which.Method.Should().Be("custom/event");
            next = enumerator.MoveNextAsync().AsTask();
            cts.Cancel();
            (await next.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeFalse();
        }
        client.RestartCount.Should().Be(0);
        client.State.Should().Be(CodexAppServerConnectionState.Connected);
    }

    [Theory]
    [InlineData("object")]
    [InlineData("element")]
    [InlineData("document")]
    [InlineData("null")]
    public async Task Pipeline_ContinuesAfterFaultyExtensions_AndNormalizesInput(string inputKind)
    {
        using var document = JsonDocument.Parse("""{"value":7}""");
        var extension = new FaultyExtensions();
        var observer = new Observer();
        var rpc = new RecordingRpc("""{"answer":42}""");
        await using var client = new CodexAppServerClient(new()
        {
            RequestParamsTransformers = [null!, extension],
            ResponseTransformers = [null!, extension],
            NotificationTransformers = [null!, extension],
            NotificationMappers = [null!, extension],
            MessageObservers = [null!, extension, observer]
        }, new Process(), rpc, NullLogger.Instance, startExitWatcher: false);
        object? parameters = inputKind switch
        {
            "object" => new { Value = 7 },
            "element" => document.RootElement,
            "document" => document,
            _ => null
        };
        var result = await client.CallAsync("custom/read", parameters);
        result.GetProperty("answer").GetInt32().Should().Be(42);
        observer.Requests.Should().ContainSingle();
        observer.Responses.Should().ContainSingle();
        JsonElement.DeepEquals(observer.Requests[0], rpc.Requests[0].Parameters).Should().BeTrue();
        if (inputKind != "null") observer.Requests[0].GetProperty("value").GetInt32().Should().Be(7);
        else observer.Requests[0].EnumerateObject().Should().BeEmpty();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = client.Notifications(cts.Token).GetAsyncEnumerator();
        await rpc.EmitAsync("custom/event", JsonDocument.Parse("null").RootElement);
        (await stream.MoveNextAsync()).Should().BeTrue();
        stream.Current.Should().BeOfType<UnknownNotification>().Which.Params.EnumerateObject().Should().BeEmpty();
        observer.Notifications.Should().ContainSingle();
        extension.RequestCalls.Should().Be(1);
        extension.ResponseCalls.Should().Be(1);
        extension.NotificationCalls.Should().Be(1);
        extension.MappingCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessExit_ClosesNotificationStreams_WithDiagnosticFallback(bool diagnosticsThrow)
    {
        var lifetime = new ExitingLifetime(diagnosticsThrow);
        var rpc = new RecordingRpc("{}");
        await using var core = new CodexAppServerClientCore(new(), lifetime, rpc, NullLogger.Instance, startExitWatcher: true);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stream = core.Notifications(cts.Token).GetAsyncEnumerator();
        var next = stream.MoveNextAsync().AsTask();
        lifetime.Exit.TrySetException(new IOException("transport exited"));
        var action = async () => await next.WaitAsync(TimeSpan.FromSeconds(5));
        var failure = (await action.Should().ThrowAsync<CodexAppServerDisconnectedException>()).Which;
        if (diagnosticsThrow)
        {
            failure.ProcessId.Should().BeNull();
            failure.ExitCode.Should().BeNull();
            failure.StderrTail.Should().BeEmpty();
        }
        else
        {
            failure.ProcessId.Should().Be(123);
            failure.ExitCode.Should().Be(7);
            failure.StderrTail.Should().ContainSingle().Which.Should().Be("process failed");
        }
    }

    private sealed class ExitingLifetime(bool diagnosticsThrow) : IAppServerLifetime
    {
        public TaskCompletionSource Exit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Completion => Exit.Task;
        public int? ProcessId => diagnosticsThrow ? throw new IOException() : 123;
        public int? ExitCode => diagnosticsThrow ? throw new IOException() : 7;
        public IReadOnlyList<string> DiagnosticTail => diagnosticsThrow ? throw new IOException() : ["process failed"];
        public ValueTask DisposeAsync() { Exit.TrySetResult(); return ValueTask.CompletedTask; }
    }

    private sealed class FaultyExtensions : IAppServerRequestParamsTransformer, IAppServerResponseTransformer, IAppServerNotificationTransformer, IAppServerNotificationMapper, IAppServerMessageObserver
    {
        public int RequestCalls, ResponseCalls, NotificationCalls, MappingCalls;
        JsonElement IAppServerRequestParamsTransformer.Transform(string method, JsonElement parameters) { RequestCalls++; throw new InvalidOperationException(); }
        JsonElement IAppServerResponseTransformer.Transform(string method, JsonElement result) { ResponseCalls++; throw new InvalidOperationException(); }
        (string Method, JsonElement Params) IAppServerNotificationTransformer.Transform(string method, JsonElement parameters) { NotificationCalls++; throw new InvalidOperationException(); }
        public AppServerNotification? TryMap(string method, JsonElement parameters) { MappingCalls++; throw new InvalidOperationException(); }
        public void OnRequest(string method, JsonElement parameters) => throw new InvalidOperationException();
        public void OnResponse(string method, JsonElement result) => throw new InvalidOperationException();
        public void OnNotification(string method, JsonElement parameters) => throw new InvalidOperationException();
    }

    private sealed class Observer : IAppServerMessageObserver
    {
        public List<JsonElement> Requests { get; } = [];
        public List<JsonElement> Responses { get; } = [];
        public List<JsonElement> Notifications { get; } = [];
        public void OnRequest(string method, JsonElement parameters) => Requests.Add(parameters);
        public void OnResponse(string method, JsonElement result) => Responses.Add(result);
        public void OnNotification(string method, JsonElement parameters) => Notifications.Add(parameters);
    }
}
