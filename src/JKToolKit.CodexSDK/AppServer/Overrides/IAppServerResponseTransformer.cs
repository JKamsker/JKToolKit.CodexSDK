using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer.Overrides;

/// <summary>
/// Transforms JSON-RPC response results before they are returned to higher layers.
/// </summary>
public interface IAppServerResponseTransformer
{
    /// <summary>
    /// Transforms a response result payload.
    /// </summary>
    /// <param name="method">The JSON-RPC method name of the originating request.</param>
    /// <param name="result">The raw result payload.</param>
    /// <returns>The transformed result payload.</returns>
    JsonElement Transform(string method, JsonElement result);
}

