using System.Text.Json;
using JKToolKit.CodexSDK.McpServer.Internal;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Helpers for extracting common fields from raw Codex MCP tool result payloads.
/// </summary>
public static class CodexMcpToolResultParsers
{
    /// <summary>
    /// Tries to extract plain text content from a raw MCP tool result payload.
    /// </summary>
    /// <param name="raw">The raw JSON payload.</param>
    /// <returns>The extracted text, or <c>null</c> if not present.</returns>
    public static string? TryExtractText(JsonElement raw) =>
        CodexMcpResultParser.TryExtractText(raw);
}
