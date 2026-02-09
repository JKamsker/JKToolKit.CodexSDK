using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// Base type for JSON-RPC notifications emitted by the Codex app server.
/// </summary>
public abstract record class AppServerNotification
{
    /// <summary>
    /// Gets the JSON-RPC notification method name.
    /// </summary>
    public string Method { get; }

    /// <summary>
    /// Gets the raw JSON-RPC notification parameters payload.
    /// </summary>
    public JsonElement Params { get; }

    /// <summary>
    /// Initializes a new notification instance.
    /// </summary>
    /// <param name="method">The JSON-RPC method name.</param>
    /// <param name="params">The raw parameters payload.</param>
    protected AppServerNotification(string method, JsonElement @params)
    {
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Params = @params;
    }
}

