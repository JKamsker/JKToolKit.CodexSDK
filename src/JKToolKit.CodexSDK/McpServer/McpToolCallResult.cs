using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Represents the raw JSON result of an MCP tool call.
/// </summary>
/// <param name="Raw">The raw JSON payload.</param>
public sealed record McpToolCallResult(JsonElement Raw);

