using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Overrides;

/// <summary>
/// Transforms outbound JSON-RPC request params before they are sent to the app-server.
/// </summary>
public interface IAppServerRequestParamsTransformer
{
    /// <summary>
    /// Transforms request params.
    /// </summary>
    /// <param name="method">The JSON-RPC method name.</param>
    /// <param name="params">The request params payload (never null).</param>
    /// <returns>The transformed params payload.</returns>
    JsonElement Transform(string method, JsonElement @params);
}

