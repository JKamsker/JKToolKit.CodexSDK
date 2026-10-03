using System.Text.Json;
using JKToolKit.CodexSDK.Infrastructure.JsonRpc.Messages;

namespace JKToolKit.CodexSDK.Infrastructure.JsonRpc;

internal interface IJsonRpcConnection : IAsyncDisposable
{
    event Func<JsonRpcNotification, ValueTask>? OnNotification;

    Func<JsonRpcRequest, ValueTask<JsonRpcResponse>>? OnServerRequest { get; set; }

    Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct);

    // Custom connections without dispatch tracking are conservatively treated as possibly sent.
    Task<JsonElement> SendRequestAsync(string method, object? @params, CancellationToken ct, Action? onDispatch)
    {
        onDispatch?.Invoke();
        return SendRequestAsync(method, @params, ct);
    }

    Task SendNotificationAsync(string method, object? @params, CancellationToken ct);
}
