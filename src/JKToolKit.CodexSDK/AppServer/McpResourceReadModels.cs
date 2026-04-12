using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for reading an MCP resource via the app-server.
/// </summary>
public sealed class McpResourceReadOptions
{
    /// <summary>
    /// Gets or sets the configured MCP server name.
    /// </summary>
    public required string Server { get; set; }

    /// <summary>
    /// Gets or sets the thread identifier that owns the MCP context.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the resource URI to read.
    /// </summary>
    public required string Uri { get; set; }
}

/// <summary>
/// Represents a single content item returned from <c>mcpResource/read</c>.
/// </summary>
public sealed record class McpResourceContent
{
    /// <summary>
    /// Gets the resource URI associated with this content.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets the optional MIME type for this content.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets the textual content when the resource returned text.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the base64-encoded blob payload when the resource returned binary content.
    /// </summary>
    public string? BlobBase64 { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for this content item.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>mcpResource/read</c>.
/// </summary>
public sealed record class McpResourceReadResult
{
    /// <summary>
    /// Gets the resource contents returned by the server.
    /// </summary>
    public required IReadOnlyList<McpResourceContent> Contents { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
