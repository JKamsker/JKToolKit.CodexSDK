using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Overrides;

/// <summary>
/// Transforms Codex MCP tool results before parsing.
/// </summary>
public interface ICodexMcpToolResultTransformer
{
    /// <summary>
    /// Transforms a tool result payload.
    /// </summary>
    /// <param name="toolName">The tool name (for example, <c>codex</c> or <c>codex-reply</c>).</param>
    /// <param name="raw">The raw tool result payload (never null).</param>
    /// <returns>The transformed tool result payload.</returns>
    JsonElement Transform(string toolName, JsonElement raw);
}

