using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Represents the parsed result of a Codex reply via the MCP tool interface.
/// </summary>
/// <param name="ThreadId">The thread identifier associated with the reply.</param>
/// <param name="Text">Optional plain text produced by the tool.</param>
/// <param name="StructuredContent">Structured content payload (raw JSON).</param>
/// <param name="Raw">The raw tool result payload.</param>
public sealed record CodexMcpReplyResult(
    string ThreadId,
    string? Text,
    JsonElement StructuredContent,
    JsonElement Raw);

