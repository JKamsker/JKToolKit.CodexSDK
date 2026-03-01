using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

public sealed partial class CodexAppServerClient
{
    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server.
    /// </summary>
    public Task<JsonElement> CallAsync(string method, object? @params, CancellationToken ct = default) =>
        _core.SendRequestAsync(method, @params, ct);

    /// <summary>
    /// Sends an arbitrary JSON-RPC request to the app server and deserializes the <c>result</c> payload.
    /// </summary>
    public async Task<TResult?> CallAsync<TResult>(
        string method,
        object? @params,
        JsonSerializerOptions? serializerOptions = null,
        CancellationToken ct = default)
    {
        var result = await _core.SendRequestAsync(method, @params, ct);
        return result.Deserialize<TResult>(serializerOptions ?? _serializerOptions);
    }
}

