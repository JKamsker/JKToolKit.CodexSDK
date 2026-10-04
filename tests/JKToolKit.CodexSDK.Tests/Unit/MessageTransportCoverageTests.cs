using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class MessageTransportCoverageTests
{
    [Fact]
    public async Task LineTransport_ReadFailureFaultsEnumerationAndCompletion()
    {
        var error = new IOException("broken pipe");
        await using var transport = new LineJsonRpcMessageTransport(new ThrowingReader(error), TextWriter.Null);
        await using var reader = transport.ReceiveAsync(default).GetAsyncEnumerator();
        (await Assert.ThrowsAsync<IOException>(() => reader.MoveNextAsync().AsTask())).Should().BeSameAs(error);
        (await Assert.ThrowsAsync<IOException>(() => transport.Completion)).Should().BeSameAs(error);
    }

    [Fact]
    public async Task WebSocketTransport_ReassemblesUtf8AcrossFragmentsAndCompletesOnClose()
    {
        var bytes = Encoding.UTF8.GetBytes("αβ");
        var socket = new ScriptedSocket();
        socket.Frames.Enqueue((bytes[..1], WebSocketMessageType.Text, false));
        socket.Frames.Enqueue((bytes[1..], WebSocketMessageType.Text, true));
        socket.Frames.Enqueue(([], WebSocketMessageType.Close, true));
        await using var transport = Transport(socket);
        await using var reader = transport.ReceiveAsync(default).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).Should().BeTrue();
        reader.Current.Should().Be("αβ");
        (await reader.MoveNextAsync()).Should().BeFalse();
        await transport.Completion;
        await transport.SendAsync("γ", default);
        socket.Sent.Should().Be("γ");
    }

    [Fact]
    public async Task WebSocketTransport_BinaryFrameFaultsCompletion()
    {
        var socket = new ScriptedSocket();
        socket.Frames.Enqueue(([1], WebSocketMessageType.Binary, true));
        await using var transport = Transport(socket);
        await using var reader = transport.ReceiveAsync(default).GetAsyncEnumerator();
        var ex = await Assert.ThrowsAsync<IOException>(() => reader.MoveNextAsync().AsTask());
        ex.Message.Should().Contain("Binary");
        (await Assert.ThrowsAsync<IOException>(() => transport.Completion)).Should().BeSameAs(ex);
    }

    [Theory]
    [InlineData(WebSocketState.Closed)]
    [InlineData(WebSocketState.Aborted)]
    public async Task WebSocketTransport_ClosedSocketCompletesWithoutReceiving(WebSocketState state)
    {
        var socket = new ScriptedSocket { CurrentState = state };
        await using var transport = Transport(socket);
        await using var reader = transport.ReceiveAsync(default).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).Should().BeFalse();
        await transport.Completion;
        socket.ReceiveCalls.Should().Be(0);
    }

    [Fact]
    public async Task WebSocketTransport_CallerCancellationCompletesNormally()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await using var transport = Transport(new ScriptedSocket());
        await using var reader = transport.ReceiveAsync(cts.Token).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).Should().BeFalse();
        await transport.Completion;
    }

    [Theory]
    [InlineData(WebSocketState.Open, false)]
    [InlineData(WebSocketState.CloseReceived, false)]
    [InlineData(WebSocketState.Open, true)]
    public async Task WebSocketTransport_DisposalClosesOrAbortsAndIsIdempotent(WebSocketState state, bool fail)
    {
        var socket = new ScriptedSocket { CurrentState = state, FailClose = fail };
        var transport = Transport(socket);
        await transport.DisposeAsync();
        await transport.DisposeAsync();
        await transport.Completion;
        socket.CloseCalls.Should().Be(1);
        socket.DisposeCalls.Should().Be(1);
        socket.AbortCalls.Should().Be(fail ? 1 : 0);
        await using var reader = transport.ReceiveAsync(default).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("https://localhost")]
    public async Task WebSocketTransport_RejectsInvalidUris(string uri)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => WebSocketJsonRpcMessageTransport.ConnectAsync(
            new Uri(uri, UriKind.RelativeOrAbsolute), null, TimeSpan.FromSeconds(1), NullLogger.Instance, default));
    }

    [Fact]
    public async Task WebSocketTransport_AlreadyCanceledConnectPreservesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebSocketJsonRpcMessageTransport.ConnectAsync(
            new Uri("ws://127.0.0.1:1"), "test-token", Timeout.InfiniteTimeSpan, NullLogger.Instance, cts.Token));
    }

    private static WebSocketJsonRpcMessageTransport Transport(WebSocket socket) => new(socket, new Uri("ws://localhost"), NullLogger.Instance);

    private sealed class ThrowingReader(Exception error) : TextReader
    {
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) => ValueTask.FromException<string?>(error);
    }

    private sealed class ScriptedSocket : WebSocket
    {
        public Queue<(byte[] Bytes, WebSocketMessageType Type, bool End)> Frames { get; } = new();
        public WebSocketState CurrentState { get; init; } = WebSocketState.Open;
        public bool FailClose { get; init; }
        public int CloseCalls { get; private set; }
        public int DisposeCalls { get; private set; }
        public int AbortCalls { get; private set; }
        public int ReceiveCalls { get; private set; }
        public string? Sent { get; private set; }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override WebSocketState State => CurrentState;
        public override void Abort() => AbortCalls++;
        public override void Dispose() => DisposeCalls++;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CloseCalls++;
            closeStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
            statusDescription.Should().Be("Disposed");
            return FailClose ? Task.FromException(new IOException("close failure")) : Task.CompletedTask;
        }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceiveCalls++;
            var frame = Frames.Dequeue();
            frame.Bytes.AsSpan().CopyTo(buffer.AsSpan());
            return Task.FromResult(new WebSocketReceiveResult(frame.Bytes.Length, frame.Type, frame.End));
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            messageType.Should().Be(WebSocketMessageType.Text);
            endOfMessage.Should().BeTrue();
            Sent = Encoding.UTF8.GetString(buffer);
            return Task.CompletedTask;
        }
    }
}
