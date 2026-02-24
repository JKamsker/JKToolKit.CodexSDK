using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Overrides;

/// <summary>
/// Transforms inbound MCP JSON-RPC response results (method-based) before they are parsed.
/// </summary>
public interface IMcpServerResponseTransformer
{
    /// <summary>
    /// Transforms a JSON-RPC response result payload.
    /// </summary>
    /// <param name="method">The JSON-RPC method name.</param>
    /// <param name="result">The raw result payload.</param>
    /// <returns>The transformed result payload.</returns>
    JsonElement Transform(string method, JsonElement result);
}

