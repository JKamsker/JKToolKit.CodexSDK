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

    /// <summary>
    /// Parses the result of a <c>tools/list</c> call into tool descriptors.
    /// </summary>
    /// <param name="result">The raw JSON result payload.</param>
    /// <returns>The parsed tool list (empty if not present/invalid).</returns>
    public static IReadOnlyList<McpToolDescriptor> ParseToolsList(JsonElement result) =>
        McpToolsListParser.Parse(result);
}
