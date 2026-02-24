using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents the result of listing MCP server status entries via the app-server.
/// </summary>
public sealed record class McpServerStatusListPage
{
    /// <summary>
    /// Gets the returned server status entries.
    /// </summary>
    public required IReadOnlyList<McpServerStatusInfo> Servers { get; init; }

    /// <summary>
    /// Gets the next cursor, when present.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

