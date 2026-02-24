using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Overrides;

/// <summary>
/// Maps <c>tools/list</c> response payloads to tool descriptors.
/// </summary>
/// <remarks>
/// This enables consumers to override tools list parsing to stay compatible with upstream MCP changes.
/// </remarks>
public interface IMcpToolsListMapper
{
    /// <summary>
    /// Attempts to map a tools list response.
    /// </summary>
    /// <param name="result">The JSON-RPC result payload.</param>
    /// <returns>A mapped tools list, or null if this mapper does not handle the payload.</returns>
    IReadOnlyList<McpToolDescriptor>? TryMap(JsonElement result);
}

