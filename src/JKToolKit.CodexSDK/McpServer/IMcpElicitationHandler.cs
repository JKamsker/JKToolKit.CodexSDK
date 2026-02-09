using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Handles server-initiated MCP elicitation requests.
/// </summary>
public interface IMcpElicitationHandler
{
    /// <summary>
    /// Handles a server request and returns a JSON result payload.
    /// </summary>
    /// <param name="method">The request method name.</param>
    /// <param name="params">Optional request parameters payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A JSON element representing the response payload.</returns>
    ValueTask<JsonElement> HandleAsync(string method, JsonElement? @params, CancellationToken ct);
}

