using System.Text.Json;

namespace NCodexSDK.McpServer;

public sealed record CodexMcpReplyResult(
    string ThreadId,
    string? Text,
    JsonElement StructuredContent,
    JsonElement Raw);

