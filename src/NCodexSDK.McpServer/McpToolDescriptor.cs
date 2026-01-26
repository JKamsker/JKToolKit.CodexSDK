using System.Text.Json;

namespace NCodexSDK.McpServer;

public sealed record McpToolDescriptor(string Name, string? Description, JsonElement? InputSchema);

