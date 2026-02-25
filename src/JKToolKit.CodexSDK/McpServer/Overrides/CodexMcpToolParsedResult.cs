using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer.Overrides;

/// <summary>
/// A parsed representation of a Codex MCP tool result.
/// </summary>
/// <param name="ThreadId">The thread identifier associated with the tool output.</param>
/// <param name="Text">Optional plain text produced by the tool.</param>
/// <param name="StructuredContent">Structured content payload (raw JSON).</param>
/// <param name="Raw">The raw tool result payload.</param>
public sealed record CodexMcpToolParsedResult(
    string ThreadId,
    string? Text,
    JsonElement StructuredContent,
    JsonElement Raw);

