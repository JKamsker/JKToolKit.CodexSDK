using System.Text.Json;

namespace JKToolKit.CodexSDK.McpServer;

/// <summary>
/// Describes an MCP tool returned by <c>tools/list</c>.
/// </summary>
/// <param name="Name">The tool name.</param>
/// <param name="Description">Optional tool description.</param>
/// <param name="InputSchema">Optional JSON schema describing input arguments.</param>
public sealed record McpToolDescriptor(string Name, string? Description, JsonElement? InputSchema);

