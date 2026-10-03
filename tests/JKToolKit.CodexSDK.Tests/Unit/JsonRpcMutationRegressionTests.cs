using System.Text.Json;
using System.Threading.Channels;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class JsonRpcMutationRegressionTests
{
    [Theory]
    [InlineData("false")]
    [InlineData("[]")]
    [InlineData("\"failure\"")]
    public async Task NonObjectErrors_SettleRequestAndPreserveRawData(string errorJson)
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        var request = rpc.SendRequestAsync("start", null, default);
        using var sent = JsonDocument.Parse(await wire.Read());
        wire.Inbound.Writer.TryWrite($"{{\"id\":{sent.RootElement.GetProperty("id").GetInt64()},\"error\":{errorJson}}}");
        var error = await Assert.ThrowsAsync<JsonRpcRemoteException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(errorJson, error.Error.Data!.Value.GetRawText());
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"error\":null")]
    public async Task MissingResultAndError_FaultsCorrelatedRequest(string fields)
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        var request = rpc.SendRequestAsync("start", null, default);
        using var sent = JsonDocument.Parse(await wire.Read());
        wire.Inbound.Writer.TryWrite($"{{\"id\":{sent.RootElement.GetProperty("id").GetInt64()}{fields}}}");
        await Assert.ThrowsAsync<JsonRpcProtocolException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("null", -32600)]
    [InlineData("false", -32600)]
    [InlineData("\" \"", -32600)]
    [InlineData("\"unknown\"", -32601)]
    public async Task InvalidAndUnhandledServerRequests_ReturnErrorWithoutFaultingConnection(string method, int code)
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, false);
        wire.Inbound.Writer.TryWrite($"{{\"id\":99,\"method\":{method}}}");
        using var response = JsonDocument.Parse(await wire.Read());
        Assert.Equal(99, response.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(code, response.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.False(response.RootElement.TryGetProperty("jsonrpc", out _));
        Assert.False(response.RootElement.TryGetProperty("result", out _));
        await rpc.SendNotificationAsync("still alive", null, default);
        using var note = JsonDocument.Parse(await wire.Read());
        Assert.Equal("still alive", note.RootElement.GetProperty("method").GetString());
    }

    [Fact]
    public async Task TransportReadFailure_SettlesPendingRequestsAndObservers()
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        var request = rpc.SendRequestAsync("start", null, default);
        await wire.Read();
        var failure = new IOException("read failure");
        wire.Inbound.Writer.TryComplete(failure);
        var error = await Record.ExceptionAsync(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Same(failure, error);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var observerError = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in rpc.Notifications(timeout.Token)) { }
        });
        Assert.Same(failure, observerError);
    }

    [Fact]
    public async Task InvalidUncorrelatedFrames_DoNotPreventSubsequentReply()
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        var request = rpc.SendRequestAsync("start", null, default);
        using var sent = JsonDocument.Parse(await wire.Read());
        foreach (var frame in new[] { "", "null", "42", "[]", "{}", "{\"id\":{},\"result\":null}", "{\"method\":\" \"}" })
            wire.Inbound.Writer.TryWrite(frame);
        wire.Inbound.Writer.TryWrite($"{{\"id\":{sent.RootElement.GetProperty("id").GetInt64()},\"result\":42}}");
        Assert.Equal(42, (await request.WaitAsync(TimeSpan.FromSeconds(5))).GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatchNotificationAndCancellation_RespectWireMode(bool header)
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, header);
        using var cancellation = new CancellationTokenSource();
        var dispatched = 0;
        var request = rpc.SendRequestAsync("start", new { PascalCase = 7 }, cancellation.Token, () => dispatched++);
        using var sent = JsonDocument.Parse(await wire.Read());
        Assert.Equal(1, dispatched);
        Assert.Equal(header, sent.RootElement.TryGetProperty("jsonrpc", out _));
        Assert.Equal(7, sent.RootElement.GetProperty("params").GetProperty("pascal_case").GetInt32());
        Assert.False(wire.LastSendToken.CanBeCanceled);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        if (header)
        {
            using var canceled = JsonDocument.Parse(await wire.Read());
            Assert.Equal("notifications/cancelled", canceled.RootElement.GetProperty("method").GetString());
            Assert.Equal(sent.RootElement.GetProperty("id").GetInt64(), canceled.RootElement.GetProperty("params").GetProperty("request_id").GetInt64());
        }
        await rpc.SendNotificationAsync("notify", new { PascalCase = 8 }, default);
        using var note = JsonDocument.Parse(await wire.Read());
        Assert.Equal(header, note.RootElement.TryGetProperty("jsonrpc", out _));
        Assert.Equal("notify", note.RootElement.GetProperty("method").GetString());
        Assert.Equal(8, note.RootElement.GetProperty("params").GetProperty("pascal_case").GetInt32());
        Assert.False(wire.Outbound.Reader.TryRead(out _));
    }

    [Fact]
    public async Task CanceledBeforeDispatch_DoesNotWriteRequestOrCancellation()
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var dispatched = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rpc.SendRequestAsync("start", null, cancellation.Token, () => dispatched = true));
        Assert.False(dispatched);
        Assert.False(wire.Outbound.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Eof_ClosesNotificationStreamAndRejectsFurtherWrites()
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        wire.Inbound.Writer.TryWrite("{\"method\":\"one\",\"params\":{}}");
        wire.Inbound.Writer.TryComplete();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var notes = new List<string>();
        var error = await Record.ExceptionAsync(async () =>
        {
            await foreach (var note in rpc.Notifications(timeout.Token)) notes.Add(note.Method);
        });
        Assert.IsType<JsonRpcConnectionClosedException>(error);
        Assert.Equal(new[] { "one" }, notes);
        await Assert.ThrowsAsync<JsonRpcProtocolException>(() => rpc.SendNotificationAsync("late", null, default));
        Assert.False(wire.Outbound.Reader.TryRead(out _));
        await rpc.DisposeAsync();
        Assert.True(wire.Disposed);
    }

    [Fact]
    public async Task ErrorData_SurvivesResponseDocumentLifetime()
    {
        var wire = new Transport();
        await using var rpc = NewConnection(wire, true);
        var request = rpc.SendRequestAsync("start", null, default);
        using var sent = JsonDocument.Parse(await wire.Read());
        wire.Inbound.Writer.TryWrite($"{{\"id\":{sent.RootElement.GetProperty("id").GetInt64()},\"error\":{{\"code\":-32602,\"message\":\"bad input\",\"data\":{{\"field\":\"cwd\"}}}}}}");
        var error = await Assert.ThrowsAsync<JsonRpcRemoteException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        await rpc.DisposeAsync();
        Assert.Equal("cwd", error.Error.Data!.Value.GetProperty("field").GetString());
    }

    private static JsonRpcConnection NewConnection(Transport wire, bool header) => new(wire, header, 8,
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }, NullLogger.Instance);

    private sealed class Transport : IJsonRpcMessageTransport
    {
        public Channel<string> Inbound { get; } = Channel.CreateUnbounded<string>();
        public Channel<string> Outbound { get; } = Channel.CreateUnbounded<string>();
        public CancellationToken LastSendToken { get; private set; }
        public bool Disposed { get; private set; }
        public Task Completion => Inbound.Reader.Completion;
        public Task<string> Read() => Outbound.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        public Task SendAsync(string message, CancellationToken ct)
        { LastSendToken = ct; Outbound.Writer.TryWrite(message); return Task.CompletedTask; }
        public IAsyncEnumerable<string> ReceiveAsync(CancellationToken ct) => Inbound.Reader.ReadAllAsync(ct);
        public ValueTask DisposeAsync()
        { Disposed = true; Inbound.Writer.TryComplete(); Outbound.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
