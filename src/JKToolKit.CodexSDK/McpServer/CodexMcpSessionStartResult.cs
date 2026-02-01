using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

public sealed record CodexMcpSessionStartResult(
    string ThreadId,
    string? Text,
    JsonElement StructuredContent,
    JsonElement Raw);

