using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
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

    [Theory]
    [InlineData(WebSocketState.Open)]
    [InlineData(WebSocketState.CloseReceived)]
    public async Task WebSocketTransport_ReassemblesUtf8AcrossFragmentsAndCompletesOnClose(WebSocketState state)
    {
        var bytes = Encoding.UTF8.GetBytes("αβ");
        var socket = new ScriptedSocket { CurrentState = state };
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
        socket.CloseCalls.Should().Be(1);
        socket.DisposeCalls.Should().Be(1);
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

    [Fact]
    public async Task WebSocketTransport_ConnectsAuthenticatesAndExchangesMessagesOverLoopback()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var serve = ServeAsync();
        await using (var transport = await WebSocketJsonRpcMessageTransport.ConnectAsync(
            new Uri($"ws://127.0.0.1:{endpoint.Port}/rpc"), "local-token", TimeSpan.FromSeconds(5), NullLogger.Instance, deadline.Token))
        {
            await transport.SendAsync("request α", deadline.Token);
            await using var messages = transport.ReceiveAsync(deadline.Token).GetAsyncEnumerator();
            (await messages.MoveNextAsync()).Should().BeTrue();
            messages.Current.Should().Be("response β");
        }
        await serve.WaitAsync(deadline.Token);

        async Task ServeAsync()
        {
            using var client = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = client.GetStream();
            using var headers = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
            (await headers.ReadLineAsync(deadline.Token)).Should().Be("GET /rpc HTTP/1.1");
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (await headers.ReadLineAsync(deadline.Token) is { Length: > 0 } line)
            {
                var split = line.IndexOf(':');
                values[line[..split]] = line[(split + 1)..].Trim();
            }
            values["Authorization"].Should().Be("Bearer local-token");
            var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(values["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n");
            await stream.WriteAsync(response, deadline.Token);
            using var socket = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: Timeout.InfiniteTimeSpan);
            var buffer = new byte[1024];
            var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token);
            Encoding.UTF8.GetString(buffer, 0, received.Count).Should().Be("request α");
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("response β")), WebSocketMessageType.Text, true, deadline.Token);
            (await socket.ReceiveAsync(new ArraySegment<byte>(buffer), deadline.Token)).MessageType.Should().Be(WebSocketMessageType.Close);
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", deadline.Token);
        }
    }

    [Fact]
    public async Task WebSocketTransport_ConnectTimeoutIsDistinctFromCallerCancellation()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connect = WebSocketJsonRpcMessageTransport.ConnectAsync(new Uri($"ws://127.0.0.1:{endpoint.Port}"),
            null, TimeSpan.FromMilliseconds(100), NullLogger.Instance, deadline.Token);
        using var client = await listener.AcceptTcpClientAsync(deadline.Token);
        await Assert.ThrowsAsync<TimeoutException>(() => connect);
        deadline.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task Transports_UnrelatedCancellationFaultsCompletion()
    {
        var error = new OperationCanceledException("unrelated cancellation");
        await using var line = new LineJsonRpcMessageTransport(new ThrowingReader(error), TextWriter.Null);
        await using var socket = Transport(new ScriptedSocket { ReceiveError = error });
        foreach (var transport in new IJsonRpcMessageTransport[] { line, socket })
        {
            await using var reader = transport.ReceiveAsync(default).GetAsyncEnumerator();
            (await Assert.ThrowsAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask())).Should().BeSameAs(error);
            (await Assert.ThrowsAsync<OperationCanceledException>(() => transport.Completion)).Should().BeSameAs(error);
        }
    }

    [Fact]
    public async Task Transports_RejectNullMessagesAndDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new LineJsonRpcMessageTransport(null!, TextWriter.Null));
        Assert.Throws<ArgumentNullException>(() => new LineJsonRpcMessageTransport(TextReader.Null, null!));
        Assert.Throws<ArgumentNullException>(() => new WebSocketJsonRpcMessageTransport(null!, new Uri("ws://localhost"), NullLogger.Instance));
        Assert.Throws<ArgumentNullException>(() => new WebSocketJsonRpcMessageTransport(new ScriptedSocket(), null!, NullLogger.Instance));
        Assert.Throws<ArgumentNullException>(() => new WebSocketJsonRpcMessageTransport(new ScriptedSocket(), new Uri("ws://localhost"), null!));
        await using var line = new LineJsonRpcMessageTransport(TextReader.Null, TextWriter.Null);
        await using var socket = Transport(new ScriptedSocket());
        await Assert.ThrowsAsync<ArgumentNullException>(() => line.SendAsync(null!, default));
        await Assert.ThrowsAsync<ArgumentNullException>(() => socket.SendAsync(null!, default));
    }

    [Fact]
    public async Task LineTransport_WritesLineFlushesAndCompletesOnDispose()
    {
        using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        var transport = new LineJsonRpcMessageTransport(new StringReader("first\nsecond\n"), writer);
        await transport.SendAsync("message α", default);
        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("message α" + Environment.NewLine);
        var lines = new List<string>();
        await foreach (var line in transport.ReceiveAsync(default)) lines.Add(line);
        lines.Should().Equal("first", "second");
        await transport.Completion;
        await transport.DisposeAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await using var canceled = new LineJsonRpcMessageTransport(new ThrowingReader(new OperationCanceledException(cts.Token)), TextWriter.Null);
        await using var reader = canceled.ReceiveAsync(cts.Token).GetAsyncEnumerator();
        (await reader.MoveNextAsync()).Should().BeFalse();
        await canceled.Completion;
        var disposedBeforeRead = new LineJsonRpcMessageTransport(TextReader.Null, TextWriter.Null);
        await disposedBeforeRead.DisposeAsync();
        disposedBeforeRead.Completion.IsCompletedSuccessfully.Should().BeTrue();
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
        public Exception? ReceiveError { get; init; }
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
            if (ReceiveError is not null) return Task.FromException<WebSocketReceiveResult>(ReceiveError);
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
