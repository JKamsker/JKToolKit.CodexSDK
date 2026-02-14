using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a page of loaded thread identifiers as returned by the app-server.
/// </summary>
public sealed record class CodexLoadedThreadListPage
{
    /// <summary>
    /// Gets the loaded thread identifiers.
    /// </summary>
    public required IReadOnlyList<string> ThreadIds { get; init; }

    /// <summary>
    /// Gets the next cursor token when present.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the response.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

