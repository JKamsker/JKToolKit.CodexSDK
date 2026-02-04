using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed class JsonRpcConnection : IAsyncDisposable
{
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;
    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _readLoop;
    private Exception? _fault;

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
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
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

        try
        {
            await WriteAsync(CreateRequestObject(id, method, @params), ct);
            return await tcs.Task.WaitAsync(ct);
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
            while (!_disposeCts.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(_disposeCts.Token);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    continue;
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(line);
                }
                catch (Exception ex)
                {
                    throw new JsonRpcProtocolException($"Failed to parse JSON-RPC message: '{line}'.", ex);
                }

                using (doc)
                {
                    var root = doc.RootElement;

                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var hasId = root.TryGetProperty("id", out var idProp);
                    var hasMethod = root.TryGetProperty("method", out var methodProp);

                    if (hasId && hasMethod)
                    {
                        await HandleServerRequestAsync(idProp, methodProp, root);
                        continue;
                    }

                    if (hasId)
                    {
                        HandleResponse(idProp, root);
                        continue;
                    }

                    if (hasMethod)
                    {
                        await HandleNotificationAsync(methodProp, root);
                        continue;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            _fault = ex;
            _logger.LogWarning(ex, "JSON-RPC read loop terminated with error.");
            foreach (var (_, tcs) in _pending)
            {
                tcs.TrySetException(ex);
            }
        }
        finally
        {
            _notifications.Writer.TryComplete(_fault);
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

        if (!root.TryGetProperty("result", out var resultProp))
        {
            tcs.TrySetException(new JsonRpcProtocolException("JSON-RPC response missing 'result'/'error'."));
            return;
        }

        tcs.TrySetResult(resultProp.Clone());
    }

    private async Task HandleNotificationAsync(JsonElement methodProp, JsonElement root)
    {
        var method = methodProp.GetString();
        if (string.IsNullOrWhiteSpace(method))
        {
            return;
        }

        JsonElement? @params = null;
        if (root.TryGetProperty("params", out var paramsProp))
        {
            @params = paramsProp.Clone();
        }

        var notification = new JsonRpcNotification(method, @params);

        _notifications.Writer.TryWrite(notification);

        var handler = OnNotification;
        if (handler is not null)
        {
            try
            {
                await handler(notification);
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "JSON-RPC notification handler threw.");
            }
        }
    }

    private async Task HandleServerRequestAsync(JsonElement idProp, JsonElement methodProp, JsonElement root)
    {
        var method = methodProp.GetString();
        if (string.IsNullOrWhiteSpace(method))
        {
            return;
        }

        var id = new JsonRpcId(idProp.Clone());

        JsonElement? @params = null;
        if (root.TryGetProperty("params", out var paramsProp))
        {
            @params = paramsProp.Clone();
        }

        var request = new JsonRpcRequest(id, method, @params);

        JsonRpcResponse response;
        var handler = OnServerRequest;
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
                response = await handler(request);
            }
            catch (Exception ex)
            {
                response = new JsonRpcResponse(
                    id,
                    Result: null,
                    Error: new JsonRpcError(-32000, ex.Message));
            }
        }

        await WriteAsync(CreateResponseObject(response), _disposeCts.Token);
    }

    private async Task WriteAsync(object payload, CancellationToken ct)
    {
        ThrowIfFaulted();
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(payload, _serializerOptions);
        await _writer.WriteLineAsync(json.AsMemory(), ct);
        await _writer.FlushAsync(ct);
    }

    private void ThrowIfFaulted()
    {
        if (_fault is not null)
        {
            throw new JsonRpcProtocolException("JSON-RPC connection is faulted.", _fault);
        }
    }

    private object CreateRequestObject(long id, string method, object? @params)
    {
        return new JsonRpcRequestWireMessage
        {
            Id = id,
            Method = method,
            Params = @params,
            JsonRpc = IncludeJsonRpcHeader ? "2.0" : null
        };
    }

    private object CreateNotificationObject(string method, object? @params)
    {
        return new JsonRpcNotificationWireMessage
        {
            Method = method,
            Params = @params,
            JsonRpc = IncludeJsonRpcHeader ? "2.0" : null
        };
    }

    private object CreateResponseObject(JsonRpcResponse response)
    {
        return new JsonRpcResponseWireMessage
        {
            Id = response.Id.Value,
            Result = response.Error is null ? response.Result : null,
            Error = response.Error,
            JsonRpc = IncludeJsonRpcHeader ? "2.0" : null
        };
    }

    private static JsonRpcError ParseError(JsonElement errorProp)
    {
        var code = errorProp.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out var c)
            ? c
            : -32000;

        var message = errorProp.TryGetProperty("message", out var messageProp)
            ? (messageProp.GetString() ?? "Remote error")
            : "Remote error";

        JsonElement? data = null;
        if (errorProp.TryGetProperty("data", out var dataProp))
        {
            data = dataProp.Clone();
        }

        return new JsonRpcError(code, message, data);
    }
}
