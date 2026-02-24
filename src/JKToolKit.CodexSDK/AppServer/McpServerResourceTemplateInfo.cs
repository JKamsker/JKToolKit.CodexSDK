using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a resource template entry reported for an MCP server.
/// </summary>
public sealed record class McpServerResourceTemplateInfo
{
    /// <summary>
    /// Gets the template name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the template URI pattern.
    /// </summary>
    public required string UriTemplate { get; init; }

    /// <summary>
    /// Gets an optional template title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets an optional template description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets an optional template MIME type.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the template.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
