using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a raw app-server JSON-RPC notification (method + params).
/// </summary>
public sealed record class AppServerRpcNotification
{
    /// <summary>
    /// Gets the JSON-RPC method name.
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// Gets the raw JSON-RPC params payload (never null; may be an empty object).
    /// </summary>
    public JsonElement Params { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="AppServerRpcNotification"/>.
    /// </summary>
    public AppServerRpcNotification(string method, JsonElement @params)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Params = @params;
    }
}

