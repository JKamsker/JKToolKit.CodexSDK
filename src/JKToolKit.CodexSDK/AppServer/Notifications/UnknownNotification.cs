using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Notifications;

/// <summary>
/// Fallback notification type used when the method is not recognized by the SDK.
/// </summary>
public sealed record class UnknownNotification : AppServerNotification
{
    /// <summary>
    /// Initializes a new instance of <see cref="UnknownNotification"/>.
    /// </summary>
    /// <param name="method">The JSON-RPC method name.</param>
    /// <param name="params">The raw parameters payload.</param>
    public UnknownNotification(string method, JsonElement @params)
        : base(method, @params)
    {
    }
}

