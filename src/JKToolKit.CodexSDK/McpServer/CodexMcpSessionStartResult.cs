using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Represents the parsed result of starting a Codex session via the MCP tool interface.
/// </summary>
/// <param name="ThreadId">The started thread identifier.</param>
/// <param name="Text">Optional plain text produced by the tool.</param>
/// <param name="StructuredContent">Structured content payload (raw JSON).</param>
/// <param name="Raw">The raw tool result payload.</param>
public sealed record CodexMcpSessionStartResult(
    string ThreadId,
    string? Text,
    JsonElement StructuredContent,
    JsonElement Raw);

