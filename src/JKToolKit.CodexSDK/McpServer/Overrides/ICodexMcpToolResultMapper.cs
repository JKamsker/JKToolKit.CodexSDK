using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Overrides;

/// <summary>
/// Maps Codex MCP tool results to a parsed representation.
/// </summary>
public interface ICodexMcpToolResultMapper
{
    /// <summary>
    /// Attempts to map a tool result payload.
    /// </summary>
    /// <param name="toolName">The tool name (for example, <c>codex</c> or <c>codex-reply</c>).</param>
    /// <param name="raw">The raw tool result payload (never null).</param>
    /// <returns>A mapped result, or null if this mapper does not handle the payload.</returns>
    CodexMcpToolParsedResult? TryMap(string toolName, JsonElement raw);
}

