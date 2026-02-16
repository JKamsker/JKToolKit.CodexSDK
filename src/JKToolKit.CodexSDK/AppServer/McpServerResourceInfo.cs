using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a resource entry reported for an MCP server.
/// </summary>
public sealed record class McpServerResourceInfo
{
    /// <summary>
    /// Gets the resource name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the resource URI.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets an optional resource title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional resource description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional resource MIME type.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets an optional resource size.
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the resource.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
