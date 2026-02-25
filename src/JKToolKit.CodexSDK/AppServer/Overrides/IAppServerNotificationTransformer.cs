using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Overrides;

/// <summary>
/// Transforms incoming JSON-RPC notifications (method + params) before mapping.
/// </summary>
public interface IAppServerNotificationTransformer
{
    /// <summary>
    /// Transforms the notification method name and params payload.
    /// </summary>
    /// <param name="method">The JSON-RPC method name.</param>
    /// <param name="params">The raw JSON-RPC params payload (never null).</param>
    /// <returns>The transformed method and params.</returns>
    (string Method, JsonElement Params) Transform(string method, JsonElement @params);
}

