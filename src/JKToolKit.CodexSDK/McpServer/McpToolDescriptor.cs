using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

public sealed record McpToolDescriptor(string Name, string? Description, JsonElement? InputSchema);

