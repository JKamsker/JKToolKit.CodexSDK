using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Wire;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal sealed partial class JsonRpcConnection
{
    private async Task WriteAsync(object payload, CancellationToken ct)
    {
        ThrowIfFaulted();
        ct.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(payload, _serializerOptions);

        // Serialize all outbound writes to prevent concurrent calls from interleaving/corrupting JSONL.
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfFaulted();

            // Don't cancel mid-write. Callers can cancel waiting for responses, but the wire must remain well-formed.
            await _transport.SendAsync(json, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
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
        if (errorProp.ValueKind != JsonValueKind.Object)
        {
            return new JsonRpcError(-32000, "Remote error", Data: errorProp.Clone());
        }

        var code = errorProp.TryGetProperty("code", out var codeProp) && codeProp.TryGetInt32(out var c)
            ? c
            : -32000;

        var message = errorProp.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String
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
