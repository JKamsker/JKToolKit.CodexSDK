using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a tool entry reported for an MCP server.
/// </summary>
public sealed record class McpServerToolInfo
{
    /// <summary>
    /// Gets the tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets an optional tool title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional tool description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the tool input schema, when present.
    /// </summary>
    public JsonElement? InputSchema { get; init; }

    /// <summary>
    /// Gets the tool output schema, when present.
    /// </summary>
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the tool.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
