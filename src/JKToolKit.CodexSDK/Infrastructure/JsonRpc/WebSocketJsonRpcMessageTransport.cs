using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed class WebSocketJsonRpcMessageTransport : IJsonRpcMessageTransport
{
    private const int ReceiveBufferSize = 16 * 1024;
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);
    private readonly WebSocket _socket;
    private readonly Uri _uri;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    internal WebSocketJsonRpcMessageTransport(WebSocket socket, Uri uri, ILogger logger)
    {
        _socket = socket ?? throw new ArgumentNullException(nameof(socket));
        _uri = uri ?? throw new ArgumentNullException(nameof(uri));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Completion => _completion.Task;

    public static async Task<WebSocketJsonRpcMessageTransport> ConnectAsync(
        Uri uri,
        string? bearerToken,
        TimeSpan connectTimeout,
        ILogger logger,
        CancellationToken ct)
    {
        ValidateUri(uri);
        ArgumentNullException.ThrowIfNull(logger);

        var socket = new ClientWebSocket();
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            socket.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
        }

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (connectTimeout != Timeout.InfiniteTimeSpan)
        {
            connectCts.CancelAfter(connectTimeout);
        }

        try
        {
            await socket.ConnectAsync(uri, connectCts.Token).ConfigureAwait(false);
            return new WebSocketJsonRpcMessageTransport(socket, uri, logger);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            socket.Dispose();
            throw new TimeoutException($"WebSocket app-server connect timed out after {connectTimeout}.");
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public Task SendAsync(string message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        var bytes = Encoding.UTF8.GetBytes(message);
        return _socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken: ct);
    }

    public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (Volatile.Read(ref _disposed) == 0)
        {
            string? message;
            try
            {
                message = await ReceiveOneAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _completion.TrySetResult();
                yield break;
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
                throw;
            }

            if (message is null)
            {
                _completion.TrySetResult();
                yield break;
            }

            yield return message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Disposed",
                        CancellationToken.None)
                    .WaitAsync(CloseTimeout)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error closing WebSocket JSON-RPC transport for {Uri}.", _uri);
            try { _socket.Abort(); } catch { /* ignore */ }
        }
        finally
        {
            _socket.Dispose();
            _completion.TrySetResult();
        }
    }

    private async Task<string?> ReceiveOneAsync(CancellationToken ct)
    {
        if (_socket.State is not WebSocketState.Open and not WebSocketState.CloseReceived)
        {
            return null;
        }

        var buffer = new byte[ReceiveBufferSize];
        using var message = new MemoryStream();

        while (true)
        {
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new IOException($"WebSocket app-server sent unsupported {result.MessageType} frame.");
            }

            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(message.ToArray());
            }
        }
    }

    private static void ValidateUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("WebSocket app-server URI must be absolute.", nameof(uri));
        }

        if (uri.Scheme is not "ws" and not "wss")
        {
            throw new ArgumentException("WebSocket app-server URI must use ws:// or wss://.", nameof(uri));
        }
    }
}
