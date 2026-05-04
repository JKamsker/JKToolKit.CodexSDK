using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed partial class JsonRpcConnection : IJsonRpcConnection
{
    private readonly IJsonRpcMessageTransport _transport;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _readLoop;
    private Exception? _fault;
    private int _faulted;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    private long _nextId;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new();

    private readonly Channel<JsonRpcNotification> _notifications;

    public bool IncludeJsonRpcHeader { get; }

    public event Func<JsonRpcNotification, ValueTask>? OnNotification;

    public Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

    public JsonRpcConnection(
        StreamReader reader,
        StreamWriter writer,
        bool includeJsonRpcHeader,
        int notificationBufferCapacity,
        JsonSerializerOptions? serializerOptions,
        ILogger logger)
        : this(
            new LineJsonRpcMessageTransport(reader, writer),
            includeJsonRpcHeader,
            notificationBufferCapacity,
            serializerOptions,
            logger)
    {
    }

    public JsonRpcConnection(
        IJsonRpcMessageTransport transport,
        bool includeJsonRpcHeader,
        int notificationBufferCapacity,
        JsonSerializerOptions? serializerOptions,
        ILogger logger)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        IncludeJsonRpcHeader = includeJsonRpcHeader;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serializerOptions = serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

        _notifications = Channel.CreateBounded<JsonRpcNotification>(new BoundedChannelOptions(notificationBufferCapacity)
        {
            SingleReader = false,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _readLoop = Task.Run(ReadLoopAsync);
    }

    public async Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct)
    {
        ThrowIfFaulted();
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be empty or whitespace.", nameof(method));

        var id = Interlocked.Increment(ref _nextId);
        var idElement = JsonRpcId.FromNumber(id);
        var key = JsonRpcId.ToKey(idElement.Value);

        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(key, tcs))
        {
            throw new InvalidOperationException($"Duplicate JSON-RPC id '{key}'.");
        }

        var requestWritten = false;
        try
        {
            await WriteAsync(CreateRequestObject(id, method, @params), ct);
            requestWritten = true;
            return await tcs.Task.WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (requestWritten && IncludeJsonRpcHeader)
            {
                try
                {
                    await WriteAsync(
                        CreateNotificationObject("notifications/cancelled", new { requestId = id }),
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // best-effort
                }
            }

            _pending.TryRemove(key, out _);
            throw;
        }
        catch
        {
            _pending.TryRemove(key, out _);
            throw;
        }
    }

    public Task SendNotificationAsync(string method, object? @params, CancellationToken ct)
    {
        ThrowIfFaulted();
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be empty or whitespace.", nameof(method));

        return WriteAsync(CreateNotificationObject(method, @params), ct);
    }

    public IAsyncEnumerable<JsonRpcNotification> Notifications(CancellationToken ct) =>
        _notifications.Reader.ReadAllAsync(ct);

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();

        try
        {
            await _readLoop;
        }
        catch
        {
            // ignore
        }

        try
        {
            await _transport.DisposeAsync();
        }
        catch
        {
            // ignore
        }

        _notifications.Writer.TryComplete();

        foreach (var (_, tcs) in _pending)
        {
            tcs.TrySetCanceled();
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            await foreach (var line in _transport.ReceiveAsync(_disposeCts.Token).ConfigureAwait(false))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                JsonDocument? doc = null;
                try
                {
                    doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        LogBogus($"Dropping non-object JSON-RPC message. Line: '{line}'.");
                        continue;
                    }

                    var hasId = root.TryGetProperty("id", out var idProp);
                    var hasMethod = root.TryGetProperty("method", out var methodProp);

                    if (hasId && hasMethod)
                    {
                        HandleServerRequest(idProp, methodProp, root);
                        continue;
                    }

                    if (hasId)
                    {
                        try
                        {
                            HandleResponse(idProp, root);
                        }
                        catch (Exception ex)
                        {
                            LogBogus($"Dropping malformed JSON-RPC response. Line: '{line}'.", ex);
                        }
                        continue;
                    }

                    if (hasMethod)
                    {
                        HandleNotification(methodProp, root);
                        continue;
                    }

                    LogBogus($"Dropping unknown JSON-RPC message shape. Line: '{line}'.");
                }
                catch (JsonException ex)
                {
                    LogBogus($"Dropping invalid JSON from server. Line: '{line}'.", ex);
                }
                finally
                {
                    doc?.Dispose();
                }
            }

            if (!_disposeCts.IsCancellationRequested)
            {
                Fault(new JsonRpcConnectionClosedException("JSON-RPC stream closed by remote endpoint."));
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            Fault(ex);
            _logger.LogWarning(ex, "JSON-RPC read loop terminated with error.");
        }
        finally
        {
            _notifications.Writer.TryComplete(_fault);
        }
    }

    private void Fault(Exception ex)
    {
        if (Interlocked.Exchange(ref _faulted, 1) != 0)
        {
            return;
        }

        _fault = ex;

        foreach (var (key, _) in _pending)
        {
            if (_pending.TryRemove(key, out var tcs))
            {
                tcs.TrySetException(ex);
            }
        }
    }

    private void HandleResponse(JsonElement idProp, JsonElement root)
    {
        var key = JsonRpcId.ToKey(idProp);
        if (!_pending.TryRemove(key, out var tcs))
        {
            _logger.LogTrace("Dropping JSON-RPC response for unknown id '{Id}'.", key);
            return;
        }

        if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.Object)
        {
            var error = ParseError(errorProp);
            tcs.TrySetException(new JsonRpcRemoteException(error));
            return;
        }

        if (root.TryGetProperty("error", out errorProp) && errorProp.ValueKind != JsonValueKind.Undefined &&
            errorProp.ValueKind != JsonValueKind.Null)
        {
            var error = ParseError(errorProp);
            tcs.TrySetException(new JsonRpcRemoteException(error));
            return;
        }

        if (!root.TryGetProperty("result", out var resultProp))
        {
            tcs.TrySetException(new JsonRpcProtocolException("JSON-RPC response missing 'result'/'error'."));
            return;
        }

        tcs.TrySetResult(resultProp.Clone());
    }

    private void HandleNotification(JsonElement methodProp, JsonElement root)
    {
        if (methodProp.ValueKind != JsonValueKind.String)
        {
            LogBogus($"Dropping JSON-RPC notification with non-string method: {methodProp.GetRawText()}.");
            return;
        }

        var method = methodProp.GetString();
        if (string.IsNullOrWhiteSpace(method))
        {
            return;
        }

        var notification = new JsonRpcNotification(method, TryCloneParams(root));
        _notifications.Writer.TryWrite(notification);

        var handler = OnNotification;
        if (handler is not null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await handler(notification).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "JSON-RPC notification handler threw.");
                }
            }, CancellationToken.None);
        }
    }

    private void HandleServerRequest(JsonElement idProp, JsonElement methodProp, JsonElement root)
    {
        var id = new JsonRpcId(idProp.Clone());

        if (methodProp.ValueKind != JsonValueKind.String)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await WriteAsync(
                            CreateResponseObject(new JsonRpcResponse(
                                id,
                                Result: null,
                                Error: new JsonRpcError(-32600, "Invalid Request"))),
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Fault(ex);
                    }
                },
                CancellationToken.None);
            return;
        }

        var method = methodProp.GetString();
        if (string.IsNullOrWhiteSpace(method))
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await WriteAsync(
                            CreateResponseObject(new JsonRpcResponse(
                                id,
                                Result: null,
                                Error: new JsonRpcError(-32600, "Invalid Request"))),
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Fault(ex);
                    }
                },
                CancellationToken.None);
            return;
        }

        var request = new JsonRpcRequest(id, method, TryCloneParams(root));

        var handler = OnServerRequest;
        _ = Task.Run(async () =>
        {
            JsonRpcResponse response;
            if (handler is null)
            {
                response = new JsonRpcResponse(
                    id,
                    Result: null,
                    Error: new JsonRpcError(-32601, $"Unhandled server request '{method}'."));
            }
            else
            {
                try
                {
                    response = await handler(request).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    response = new JsonRpcResponse(
                        id,
                        Result: null,
                        Error: new JsonRpcError(-32000, ex.Message));
                }
            }

            try
            {
                await WriteAsync(CreateResponseObject(response), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Fault(ex);
                _logger.LogWarning(ex, "Failed to write JSON-RPC server request response.");
            }
        }, CancellationToken.None);
    }

}
