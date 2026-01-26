using System.Text.Json;

namespace NCodexSDK.McpServer;

public sealed record CodexMcpSessionStartResult(
    string ThreadId,
    string? Text,
    JsonElement StructuredContent,
    JsonElement Raw);

