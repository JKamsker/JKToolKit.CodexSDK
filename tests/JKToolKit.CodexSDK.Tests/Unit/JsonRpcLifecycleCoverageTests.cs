using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public class JsonRpcLifecycleCoverageTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private static JsonRpcConnection Connect(Wire wire, bool header = true, ILogger? logger = null) => new(wire, header, 8, null, logger ?? NullLogger.Instance);

    [Fact]
    public void Construction_RejectsMissingDependenciesAndInvalidCapacity()
    {
        Action transport = () => new JsonRpcConnection(null!, true, 8, null, NullLogger.Instance);
        Action logger = () => new JsonRpcConnection(new Wire(), true, 8, null, null!);
        Action capacity = () => new JsonRpcConnection(new Wire(), true, -1, null, NullLogger.Instance);
        transport.Should().Throw<ArgumentNullException>().WithParameterName("transport");
        logger.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        capacity.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task InvalidMethod_IsRejectedBeforeDispatch(string? method)
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        var dispatched = false;
        var request = () => rpc.SendRequestAsync(method!, null, default, () => dispatched = true);
        var notification = () => rpc.SendNotificationAsync(method!, null, default);
        await request.Should().ThrowAsync<ArgumentException>().WithParameterName("method");
        await notification.Should().ThrowAsync<ArgumentException>().WithParameterName("method");
        dispatched.Should().BeFalse(); wire.Outbound.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public async Task RequestWriteFailure_SettlesCallerAndReleasesWriteGateForRetry()
    {
        var wire = new Wire();
        var failure = new IOException("write failed");
        wire.OnSend = _ => Task.FromException(failure);
        await using var rpc = Connect(wire);
        var first = await Record.ExceptionAsync(() => rpc.SendRequestAsync("first", null, default).WaitAsync(Deadline));
        first.Should().BeSameAs(failure);
        wire.OnSend = null;
        var next = rpc.SendRequestAsync("next", null, default);
        using var request = JsonDocument.Parse(await wire.Read());
        var id = request.RootElement.GetProperty("id").GetInt64();
        wire.Inbound.Writer.TryWrite($"{{\"id\":{id},\"result\":\"ok\"}}");
        (await next.WaitAsync(Deadline)).GetString().Should().Be("ok");
    }

    [Fact]
    public async Task DispatchCallbackFailure_DoesNotWriteOrHoldTheGate()
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        var failure = new InvalidOperationException("dispatch callback failed");
        var request = () => rpc.SendRequestAsync("request", null, default, () => throw failure);
        (await Record.ExceptionAsync(request)).Should().BeSameAs(failure);
        wire.Outbound.Reader.TryRead(out _).Should().BeFalse();
        await rpc.SendNotificationAsync("next", null, default).WaitAsync(Deadline);
        using var sent = JsonDocument.Parse(await wire.Read());
        sent.RootElement.GetProperty("method").GetString().Should().Be("next");
    }

    [Fact]
    public async Task CancellationNotificationWriteFailure_PreservesCallerCancellation()
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        using var cancel = new CancellationTokenSource();
        var request = rpc.SendRequestAsync("request", null, cancel.Token);
        using var sent = JsonDocument.Parse(await wire.Read());
        var attempted = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        wire.OnSend = json => { attempted.TrySetResult(json); return Task.FromException(new IOException("cancel write failed")); };
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(Deadline));
        using var notification = JsonDocument.Parse(await attempted.Task.WaitAsync(Deadline));
        notification.RootElement.GetProperty("method").GetString().Should().Be("notifications/cancelled");
        notification.RootElement.GetProperty("params").GetProperty("requestId").GetInt64().Should().Be(sent.RootElement.GetProperty("id").GetInt64());
        wire.OnSend = null;
        await rpc.SendNotificationAsync("still usable", null, default).WaitAsync(Deadline);
    }

    [Fact]
    public async Task Dispose_CancelsPendingRequestsEvenWhenTransportDisposeThrows()
    {
        var wire = new Wire { DisposeFailure = new IOException("dispose failed") };
        var rpc = Connect(wire);
        var pending = rpc.SendRequestAsync("request", null, default);
        await wire.Read();
        await rpc.DisposeAsync().AsTask().WaitAsync(Deadline);
        wire.Disposed.Should().BeTrue();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Deadline));
        using var timeout = new CancellationTokenSource(Deadline);
        (await rpc.Notifications(timeout.Token).ToListAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\" \"")]
    [InlineData("\"server/request\"")]
    public async Task ServerResponseWriteFailure_FaultsPendingRequestsAndPreservesFirstFault(string method)
    {
        var wire = new Wire();
        var logger = new RecordingLogger();
        await using var rpc = Connect(wire, logger: logger);
        var pending = rpc.SendRequestAsync("pending", null, default);
        await wire.Read();
        var firstFailure = new IOException("response write failed");
        wire.OnSend = _ => Task.FromException(firstFailure);
        wire.Inbound.Writer.TryWrite("{\"id\":\"server\",\"method\":" + method + "}");
        (await Record.ExceptionAsync(() => pending.WaitAsync(Deadline))).Should().BeSameAs(firstFailure);
        // A subsequent read failure must not replace the first failure already delivered to callers.
        wire.Inbound.Writer.TryComplete(new IOException("later read failure"));
        using var timeout = new CancellationTokenSource(Deadline);
        var notes = async () => await rpc.Notifications(timeout.Token).ToListAsync();
        (await Record.ExceptionAsync(notes)).Should().BeSameAs(firstFailure);
        var late = () => rpc.SendRequestAsync("late", null, default);
        (await late.Should().ThrowAsync<JsonRpcProtocolException>()).Which.InnerException.Should().BeSameAs(firstFailure);
        if (method.Contains("server/request"))
            (await logger.WaitFor("Failed to write JSON-RPC server request response")).Exception.Should().BeSameAs(firstFailure);
    }

    [Fact]
    public async Task ThrowingNotificationHandler_IsDiagnosedWithoutLosingQueuedOrLaterNotifications()
    {
        var wire = new Wire();
        var logger = new RecordingLogger();
        await using var rpc = Connect(wire, logger: logger);
        var failure = new InvalidOperationException("consumer failed");
        rpc.OnNotification += _ => ValueTask.FromException(failure);
        wire.Inbound.Writer.TryWrite("{\"method\":\"first\"}");
        (await logger.WaitFor("notification handler threw")).Exception.Should().BeSameAs(failure);
        wire.Inbound.Writer.TryWrite("{\"method\":\"second\",\"params\":null}");
        using var timeout = new CancellationTokenSource(Deadline);
        var notes = await rpc.Notifications(timeout.Token).Take(2).ToListAsync();
        notes.Select(n => n.Method).Should().Equal("first", "second");
        notes.Should().OnlyContain(n => n.Params == null);
    }

    [Fact]
    public async Task ServerRequestHandlerFailure_ReturnsStructuredErrorAndKeepsConnectionAlive()
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        rpc.OnServerRequest = _ => ValueTask.FromException<JsonRpcResponse>(new InvalidOperationException("handler failed"));
        wire.Inbound.Writer.TryWrite("{\"id\":\"server\",\"method\":\"request\",\"params\":null}");
        using var response = JsonDocument.Parse(await wire.Read());
        response.RootElement.GetProperty("id").GetString().Should().Be("server");
        response.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be("handler failed");
        response.RootElement.GetProperty("error").GetProperty("code").GetInt32().Should().Be(-32000);
        await rpc.SendNotificationAsync("next", null, default);
        using var next = JsonDocument.Parse(await wire.Read());
        next.RootElement.GetProperty("method").GetString().Should().Be("next");
    }

    [Fact]
    public async Task DisposedConnection_RejectsRequestsAndNotificationsBeforeDispatch()
    {
        var wire = new Wire();
        var rpc = Connect(wire);
        await rpc.DisposeAsync();
        var dispatched = false;
        using var cancellation = new CancellationTokenSource();
        try
        {
            var request = () => rpc.SendRequestAsync("after-dispose", null, cancellation.Token, () => dispatched = true).WaitAsync(TimeSpan.FromSeconds(1));
            await request.Should().ThrowAsync<ObjectDisposedException>();
            var notification = () => rpc.SendNotificationAsync("after-dispose", null, default);
            await notification.Should().ThrowAsync<ObjectDisposedException>();
            dispatched.Should().BeFalse();
        }
        finally { cancellation.Cancel(); }
    }

    [Fact]
    public async Task DisposalWhileWriterIsBusy_RejectsQueuedRequestBeforeDispatch()
    {
        var wire = new Wire();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writes = 0;
        wire.OnSend = async _ => { Interlocked.Increment(ref writes); entered.TrySetResult(); await release.Task; };
        var rpc = Connect(wire);
        var first = rpc.SendNotificationAsync("in-flight", null, default);
        await entered.Task.WaitAsync(Deadline);
        var dispatched = false;
        var queued = rpc.SendRequestAsync("queued", null, default, () => dispatched = true);
        try
        {
            await rpc.DisposeAsync().AsTask().WaitAsync(Deadline);
        }
        finally { release.TrySetResult(); }
        await first.WaitAsync(Deadline);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued.WaitAsync(Deadline));
        dispatched.Should().BeFalse(); writes.Should().Be(1);
    }

    [Fact]
    public async Task UnexpectedTransportCancellation_FaultsPendingRequestsAndFutureWrites()
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        var pending = rpc.SendRequestAsync("pending", null, default);
        await wire.Read();
        var failure = new OperationCanceledException("transport canceled independently");
        wire.Inbound.Writer.TryComplete(failure);
        var actual = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(1)));
        actual.Should().BeSameAs(failure);
        var next = () => rpc.SendRequestAsync("next", null, default);
        (await next.Should().ThrowAsync<JsonRpcProtocolException>()).Which.InnerException.Should().BeSameAs(failure);
    }

    [Theory]
    [InlineData("not json", "invalid JSON")]
    [InlineData("[]", "non-object")]
    [InlineData("{}", "unknown JSON-RPC message shape")]
    [InlineData("{\"method\":false}", "non-string method")]
    [InlineData("{\"id\":12345,\"result\":true}", "unknown id")]
    public async Task DroppedFrames_EmitActionableDiagnostics(string frame, string message)
    {
        var wire = new Wire();
        var logger = new RecordingLogger();
        await using var rpc = Connect(wire, logger: logger);
        wire.Inbound.Writer.TryWrite(frame);
        (await logger.WaitFor(message)).Message.Should().Contain(message);
        await rpc.SendNotificationAsync("still usable", null, default);
    }

    [Theory]
    [InlineData("{\"value\":42}")]
    [InlineData("[1,2]")]
    [InlineData("\"text\"")]
    public async Task NotificationAndRequestParams_AreForwardedAndOutliveWireDocuments(string json)
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        var received = new TaskCompletionSource<JsonElement?>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.OnServerRequest = request =>
        {
            received.TrySetResult(request.Params);
            return ValueTask.FromResult(new JsonRpcResponse(request.Id, JsonSerializer.SerializeToElement(true), null));
        };
        wire.Inbound.Writer.TryWrite("{\"id\":\"server\",\"method\":\"request\",\"params\":" + json + "}");
        await wire.Read();
        wire.Inbound.Writer.TryWrite("{\"method\":\"notification\",\"params\":" + json + "}");
        using var timeout = new CancellationTokenSource(Deadline);
        var note = await rpc.Notifications(timeout.Token).FirstAsync();
        await rpc.DisposeAsync();
        (await received.Task.WaitAsync(Deadline))!.Value.GetRawText().Should().Be(json);
        note.Params!.Value.GetRawText().Should().Be(json);
    }

    [Fact]
    public async Task ServerResponseError_TakesPrecedenceOverConflictingResult()
    {
        var wire = new Wire();
        await using var rpc = Connect(wire);
        rpc.OnServerRequest = request => ValueTask.FromResult(new JsonRpcResponse(request.Id,
            JsonSerializer.SerializeToElement("ignored result"), new JsonRpcError(-32000, "error takes precedence")));
        wire.Inbound.Writer.TryWrite("{\"id\":1,\"method\":\"request\"}");
        using var response = JsonDocument.Parse(await wire.Read());
        response.RootElement.TryGetProperty("result", out _).Should().BeFalse();
        response.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be("error takes precedence");
    }

    [Fact]
    public void ClosedException_ConstructorsPreserveContext()
    {
        new JsonRpcConnectionClosedException().Should().BeAssignableTo<IOException>();
        var inner = new IOException("cause");
        var error = new JsonRpcConnectionClosedException("closed", inner);
        error.Message.Should().Be("closed"); error.InnerException.Should().BeSameAs(inner);
    }

    private sealed class Wire : IJsonRpcMessageTransport
    {
        public Channel<string> Inbound { get; } = Channel.CreateUnbounded<string>();
        public Channel<string> Outbound { get; } = Channel.CreateUnbounded<string>();
        public Func<string, Task>? OnSend { get; set; }
        public Exception? DisposeFailure { get; init; }
        public bool Disposed { get; private set; }
        public Task Completion => Inbound.Reader.Completion;
        public Task<string> Read() => Outbound.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
        public async Task SendAsync(string message, CancellationToken ct)
        {
            if (OnSend is { } send) await send(message);
            else Outbound.Writer.TryWrite(message);
        }
        public IAsyncEnumerable<string> ReceiveAsync(CancellationToken ct) => Inbound.Reader.ReadAllAsync(ct);
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            Inbound.Writer.TryComplete(); Outbound.Writer.TryComplete();
            return DisposeFailure is { } error ? ValueTask.FromException(error) : ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private readonly Channel<(string Message, Exception? Exception)> _messages = Channel.CreateUnbounded<(string, Exception?)>();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => _messages.Writer.TryWrite((formatter(state, exception), exception));
        public async Task<(string Message, Exception? Exception)> WaitFor(string text)
        {
            using var timeout = new CancellationTokenSource(Deadline);
            await foreach (var message in _messages.Reader.ReadAllAsync(timeout.Token))
                if (message.Message.Contains(text, StringComparison.Ordinal)) return message;
            throw new InvalidOperationException("Expected diagnostic was not emitted.");
        }
    }
}
